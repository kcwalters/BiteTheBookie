using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.Models;
using OpenAI.Chat;
using System.Text.RegularExpressions;

namespace BiteTheBookie.Services.Implementations
{
    public class GameSimulationService : IGameSimulationService
    {
        private readonly ChatClient? _chatClient;
        private readonly ILogger<GameSimulationService> _logger;

        public GameSimulationService(ChatClient? chatClient, ILogger<GameSimulationService> logger)
        {
            _logger = logger;
            _chatClient = chatClient;

            if (_chatClient == null)
            {
                _logger.LogWarning("Azure OpenAI ChatClient is not configured. Will use mock simulation.");
            }
        }

        public async Task<string> GenerateGameSimulationAsync(
            string homeTeam,
            string awayTeam,
            string league,
            NBATeamRoster? homeRoster = null,
            NBATeamRoster? awayRoster = null,
            List<PlayerInjuryReport>? injuries = null,
            DateTime? gameTime = null,
            CancellationToken cancellationToken = default,
            string? homeProbablePitcher = null,
            string? awayProbablePitcher = null)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var simulationId = Guid.NewGuid().ToString("N")[..8];

            _logger.LogInformation("Generating simulation #{SimulationId} for {HomeTeam} vs {AwayTeam} ({League}) at {Timestamp}",
                simulationId, homeTeam, awayTeam, league, timestamp);

            if (_chatClient == null)
            {
                _logger.LogWarning("_chatClient is NULL — returning mock simulation for {HomeTeam} vs {AwayTeam}", homeTeam, awayTeam);

                if (league.Equals("MLB", StringComparison.OrdinalIgnoreCase))
                    return GetMlbMockSimulation(homeTeam, awayTeam, homeProbablePitcher, awayProbablePitcher);

                var injuredPlayers = injuries?
                    .Where(i => i.InjuryStatus.Equals("Out", StringComparison.OrdinalIgnoreCase))
                    .Select(i => i.PlayerName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
                return GetMockSimulation(homeTeam, awayTeam, homeRoster, awayRoster, injuredPlayers);
            }

            // ── MLB simulation ────────────────────────────────────────────────
            if (league.Equals("MLB", StringComparison.OrdinalIgnoreCase))
            {
                return await GenerateMlbSimulationAsync(homeTeam, awayTeam, simulationId, timestamp, gameTime,
                    cancellationToken, homeProbablePitcher, awayProbablePitcher);
            }

            // ── NBA simulation (unchanged from here down) ─────────────────────
            var injuredPlayersNba = injuries?
                .Where(i => i.InjuryStatus.Equals("Out", StringComparison.OrdinalIgnoreCase))
                .Select(i => i.PlayerName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            if (injuredPlayersNba.Any())
            {
                _logger.LogWarning("EXCLUDING {Count} injured players: {Players}",
                    injuredPlayersNba.Count, string.Join(", ", injuredPlayersNba));

                foreach (var injury in injuries!.Where(i => i.InjuryStatus.Equals("Out", StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("  {Player} ({Team}) - OUT: {Description}",
                        injury.PlayerName, injury.TeamCode, injury.InjuryDescription);
                }
            }

            // Build the set of valid player names from both rosters (excluding injured)
            var validPlayers = BuildValidPlayerSet(homeRoster, awayRoster, injuredPlayersNba);

            try
            {
                _logger.LogInformation("Calling Azure OpenAI for {HomeTeam} vs {AwayTeam}...", homeTeam, awayTeam);

                var awayStarters = awayRoster?.Players
                    .Where(p => p.IsStarter && !injuredPlayersNba.Contains(p.Name))
                    .Select(p => $"{p.Name} ({p.Position})")
                    .ToList() ?? new List<string>();
                var homeStarters = homeRoster?.Players
                    .Where(p => p.IsStarter && !injuredPlayersNba.Contains(p.Name))
                    .Select(p => $"{p.Name} ({p.Position})")
                    .ToList() ?? new List<string>();
                var awayBench = awayRoster?.Players
                    .Where(p => !p.IsStarter && !injuredPlayersNba.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList() ?? new List<string>();
                var homeBench = homeRoster?.Players
                    .Where(p => !p.IsStarter && !injuredPlayersNba.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList() ?? new List<string>();

                var injuryInfo = "";
                if (injuredPlayersNba.Any())
                {
                    var awayInjuries = injuries?.Where(i => i.TeamCode == awayRoster?.TeamCode && injuredPlayersNba.Contains(i.PlayerName)).ToList() ?? new();
                    var homeInjuries = injuries?.Where(i => i.TeamCode == homeRoster?.TeamCode && injuredPlayersNba.Contains(i.PlayerName)).ToList() ?? new();

                    if (awayInjuries.Any() || homeInjuries.Any())
                    {
                        injuryInfo = "\n\n**INJURY REPORT (Players OUT for this game):**\n";
                        if (awayInjuries.Any())
                            injuryInfo += $"{awayTeam}: {string.Join(", ", awayInjuries.Select(i => $"{i.PlayerName} ({i.InjuryDescription})"))}\n";
                        if (homeInjuries.Any())
                            injuryInfo += $"{homeTeam}: {string.Join(", ", homeInjuries.Select(i => $"{i.PlayerName} ({i.InjuryDescription})"))}\n";
                    }
                }

                // Build a strict player list to embed in the prompt
                var awayPlayerList = awayRoster?.Players
                    .Where(p => !injuredPlayersNba.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList() ?? new List<string>();
                var homePlayerList = homeRoster?.Players
                    .Where(p => !injuredPlayersNba.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList() ?? new List<string>();

                var rosterDataAvailable = (awayRoster?.Players.Count ?? 0) > 0 &&
                                          (homeRoster?.Players.Count ?? 0) > 0;

                string rosterInfo;
                string rosterSystemRule;

                if (rosterDataAvailable)
                {
                    rosterInfo = $@"
**{awayTeam} Available Roster:**
Starting 5: {string.Join(", ", awayStarters)}
Key Bench: {string.Join(", ", awayBench)}

**{homeTeam} Available Roster:**
Starting 5: {string.Join(", ", homeStarters)}
Key Bench: {string.Join(", ", homeBench)}
{injuryInfo}

**COMPLETE VALID PLAYER LIST (use ONLY these exact names):**
{awayTeam}: {string.Join(", ", awayPlayerList)}
{homeTeam}: {string.Join(", ", homePlayerList)}";

                    rosterSystemRule =
                        "ONLY use player names from the COMPLETE VALID PLAYER LIST provided. " +
                        "Do NOT invent, hallucinate, or reference any player not in that list.";
                }
                else
                {
                    // No live roster — instruct AI to use verified current knowledge only
                    rosterInfo = $@"
**ROSTER DATA UNAVAILABLE** — Live roster feed could not be reached.
Use ONLY players you are certain currently play for {awayTeam} and {homeTeam} as of today.
Do NOT include any player who has been traded, waived, or retired.
{injuryInfo}";

                    rosterSystemRule =
                        "Roster data is unavailable. Use ONLY players you are certain are on each team RIGHT NOW. " +
                        "If you are not 100% certain a player is still on the team, do NOT mention them. " +
                        "Do NOT include players who have been traded, released, or retired.";
                }

                var prompt = $@"Generate a FRESH, UNIQUE sports game simulation for an NBA game between {awayTeam} (away) and {homeTeam} (home). 

SIMULATION ID: {simulationId}
GENERATED AT: {timestamp}

{rosterInfo}

CRITICAL INSTRUCTIONS - ROSTER VALIDATION:
- You MUST ONLY use players from the COMPLETE VALID PLAYER LIST above
- DO NOT invent, fabricate, or hallucinate any player names
- DO NOT use players who have been traded away or are on other teams
- Every player name in your simulation MUST appear exactly as written in the valid player list
- If you are unsure whether a player is on the team, DO NOT include them

CRITICAL INSTRUCTIONS - INJURY RULES:
- **AT THE VERY TOP OF YOUR SIMULATION**, list all injured players under a heading ""## Injury Report""
- Format: ""**Player Name** - OUT (Injury Description)""
- DO NOT UNDER ANY CIRCUMSTANCES include injured players in the game simulation
- DO NOT mention injured players in game action, statistics, or narratives
- ONLY use players from the AVAILABLE ROSTER lists above
- If a key star player is injured, mention how the team is adjusting WITHOUT that player

SIMULATION REQUIREMENTS:
- This is simulation #{simulationId} - make it COMPLETELY DIFFERENT from any previous simulations
- Use ONLY the players listed in the AVAILABLE ROSTER above
- Base the key performers and statistics on these ACTUAL AVAILABLE PLAYERS
- Make this simulation UNIQUE and SPECIFIC to {awayTeam} vs {homeTeam}
- Consider the actual strengths and playing styles of these teams
- DO NOT generate a generic simulation - make it about THIS SPECIFIC matchup
- VARY the final score each time - don't always use the same score
- CHANGE which players are the top performers - simulate different game scenarios
- CREATE DIFFERENT game narratives - sometimes close games, sometimes blowouts, sometimes comeback wins

Include the following sections in your response using Markdown formatting:

1. **Final Score**: Provide a realistic but VARIED final score (don't repeat scores from other simulations)
2. **Game Summary**: Brief overview specific to how {awayTeam} vs {homeTeam} played out (2-3 sentences) mentioning specific players from the rosters above
3. **Key Performers**: List 3-5 ACTUAL players from the rosters above with realistic stats. VARY which players have big games each simulation.
4. **Quarter-by-Quarter Breakdown**: Describe key moments in each quarter with scores at each break, mentioning specific players by name and specific plays
5. **Team Statistics**: Create a comparison table with FG%, 3PT%, rebounds, turnovers, fast-break points
6. **Betting Analysis**: Mention how the game would affect spread, over/under, and moneyline bets

CRITICAL REQUIREMENTS:
- Use ONLY players from the rosters provided above
- Use realistic statistics appropriate for each player's actual skill level and position
- Make the narrative engaging and SPECIFIC to {awayTeam} vs {homeTeam} matchup
- Consider actual team dynamics (e.g., if {awayTeam} has a dominant center, mention how they attacked the paint)
- Use standard ASCII characters only, NO emojis or special Unicode characters
- Use Markdown tables for statistics
- Be specific about which players made key plays
- Make each simulation DIFFERENT - vary the scores, key plays, and narratives based on the teams involved";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an expert NBA analyst who creates detailed, realistic game simulations. " +
                        $"CRITICAL RULES: " +
                        $"1) Start EVERY simulation with an '## Injury Report' section listing ALL injured players as 'OUT'. " +
                        $"2) NEVER include injured players in game action or statistics. " +
                        $"3) {rosterSystemRule} " +
                        $"4) Each simulation must be UNIQUE. Generate simulation #{simulationId} with fresh content. " +
                        $"Use only standard ASCII characters."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 0.9f
                };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var simulationText = response.Value.Content[0].Text;

                // Post-process: validate all player names mentioned in the simulation
                var invalidPlayers = FindInvalidPlayerNames(simulationText, validPlayers, homeTeam, awayTeam);
                if (invalidPlayers.Count > 0)
                {
                    _logger.LogWarning("AI simulation #{SimulationId} contains {Count} invalid player name(s): {Players}",
                        simulationId, invalidPlayers.Count, string.Join(", ", invalidPlayers));

                    // Append a correction notice and replace invalid names
                    simulationText = SanitizePlayerNames(simulationText, invalidPlayers, validPlayers, homeRoster, awayRoster, injuredPlayersNba);
                }

                _logger.LogInformation("AI simulation #{SimulationId} generated successfully for {HomeTeam} vs {AwayTeam}",
                    simulationId, homeTeam, awayTeam);

                return simulationText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI simulation FAILED for {HomeTeam} vs {AwayTeam}: {Type}: {Message} - falling back to mock",
                    homeTeam, awayTeam, ex.GetType().FullName, ex.Message);
                return GetMockSimulation(homeTeam, awayTeam, homeRoster, awayRoster, injuredPlayersNba);
            }
        }

        // ── MLB-specific AI generation ────────────────────────────────────────
        private async Task<string> GenerateMlbSimulationAsync(
            string homeTeam, string awayTeam, string simulationId,
            string timestamp, DateTime? gameTime, CancellationToken cancellationToken,
            string? homeProbablePitcher, string? awayProbablePitcher)
        {
            try
            {
                _logger.LogInformation("Calling Azure OpenAI for MLB: {AwayTeam} @ {HomeTeam} (SP: {AwayPitcher} vs {HomePitcher})",
                    awayTeam, homeTeam, awayProbablePitcher ?? "TBD", homeProbablePitcher ?? "TBD");

                var pitcherSection = "";
                if (!string.IsNullOrEmpty(awayProbablePitcher) || !string.IsNullOrEmpty(homeProbablePitcher))
                {
                    pitcherSection = $@"
**SCHEDULED STARTING PITCHERS (from official MLB schedule):**
- {awayTeam} starter: **{awayProbablePitcher ?? "TBD"}**
- {homeTeam} starter: **{homeProbablePitcher ?? "TBD"}**

CRITICAL: You MUST use these exact pitchers as the starting pitchers in your simulation.
- If a pitcher is listed as ""TBD"", choose a realistic current-rotation pitcher for that team.
- The listed starters MUST appear in the Starting Pitchers section and the Pitching Summary.
- Base their stat lines on their real 2024-2025 performance (ERA, K rate, WHIP, etc.).
";
                }

                var prompt = $@"Generate a FRESH, UNIQUE MLB baseball game simulation between {awayTeam} (away) and {homeTeam} (home).

SIMULATION ID: {simulationId}
GENERATED AT: {timestamp}
{pitcherSection}
SIMULATION REQUIREMENTS:
- This is simulation #{simulationId} - make it COMPLETELY DIFFERENT from any previous simulations
- Use REAL current-roster players for both {awayTeam} and {homeTeam}
- DO NOT invent or hallucinate player names - use only real MLB players on those teams
- VARY the final score each time - sometimes high-scoring, sometimes pitchers' duels
- CREATE DIFFERENT game narratives - sometimes close, sometimes blowouts, sometimes walk-offs
- THE GAME MUST HAVE A WINNER - baseball games CANNOT end in a tie
- If the score is tied after 9 innings, simulate extra innings until one team wins
- The home team ALWAYS bats last - if the home team is ahead after the top of the 9th, the bottom of the 9th is not played

Include the following sections using Markdown formatting:

1. **Final Score**: A realistic MLB score with line score (runs per inning). If extra innings were needed, include them.
2. **Game Summary**: 2-3 sentences describing how the game unfolded, mentioning the starting pitchers BY NAME and key moments
3. **Starting Pitchers**: Name both starters with their line (IP, H, R, ER, BB, K). Use the SCHEDULED STARTERS listed above.
4. **Key Performers**: 4-6 players with realistic batting lines (AB, H, R, RBI, HR) or pitching lines
5. **Inning-by-Inning Breakdown**: Describe key moments in select innings (not every inning needs detail - focus on scoring innings and dramatic moments)
6. **Team Statistics**: Markdown table comparing hits, errors, LOB, team batting average, bullpen ERA for the game
7. **Pitching Summary**: List all pitchers used by each team with their lines. The first pitcher for each team MUST be the scheduled starter.
8. **Betting Analysis**: How the result affects the run line (spread), over/under, and moneyline

CRITICAL REQUIREMENTS:
- Use the EXACT scheduled starting pitchers provided above
- Use REAL players currently on {awayTeam} and {homeTeam} rosters
- Use realistic MLB statistics (batting averages, ERA, pitch counts, etc.)
- Base each starter's performance on their REAL career/season stats
- Include realistic baseball details: pitch counts, defensive plays, stolen bases, double plays
- Mention the ballpark (home team's stadium) and how it affected play
- The final score MUST NOT be a tie - one team must win
- Use standard ASCII characters only, NO emojis
- Use Markdown tables for statistics
- Make it feel like a real MLB game recap";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage($"You are an expert MLB baseball analyst who creates detailed, realistic game simulations. Use ONLY real current-roster players. You MUST use the scheduled starting pitchers provided in the prompt - do NOT substitute different starters. Each simulation must be UNIQUE. Generate simulation #{simulationId} with fresh content. Use standard ASCII characters only. CRITICAL: Baseball games CANNOT end in a tie. There must always be a winner."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 0.9f
                };

                var response = await _chatClient!.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var simulationText = response.Value.Content[0].Text;

                _logger.LogInformation("MLB simulation #{SimulationId} generated successfully for {AwayTeam} @ {HomeTeam}",
                    simulationId, awayTeam, homeTeam);

                return simulationText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MLB AI simulation FAILED for {AwayTeam} @ {HomeTeam} - falling back to mock",
                    awayTeam, homeTeam);
                return GetMlbMockSimulation(homeTeam, awayTeam, homeProbablePitcher, awayProbablePitcher);
            }
        }

        // ── Helper methods ────────────────────────────────────────────────────

        private static HashSet<string> BuildValidPlayerSet(
            NBATeamRoster? homeRoster,
            NBATeamRoster? awayRoster,
            HashSet<string> injuredPlayers)
        {
            var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (homeRoster != null)
            {
                foreach (var p in homeRoster.Players.Where(p => !injuredPlayers.Contains(p.Name)))
                    valid.Add(p.Name);
            }
            if (awayRoster != null)
            {
                foreach (var p in awayRoster.Players.Where(p => !injuredPlayers.Contains(p.Name)))
                    valid.Add(p.Name);
            }

            return valid;
        }

        private static List<string> FindInvalidPlayerNames(
            string simulationText,
            HashSet<string> validPlayers,
            string homeTeam,
            string awayTeam)
        {
            var invalid = new List<string>();

            var skipTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                homeTeam, awayTeam,
                "Final Score", "Game Summary", "Key Performers", "Injury Report",
                "Quarter-by-Quarter Breakdown", "Team Statistics", "Betting Analysis",
                "Spread", "Over/Under", "Moneyline", "Top Players",
                "1st Quarter", "2nd Quarter", "3rd Quarter", "4th Quarter",
                "Halftime", "Team Statistics Comparison"
            };

            var boldPattern = new Regex(@"\*\*([^*]+?)\*\*", RegexOptions.Compiled);
            foreach (Match match in boldPattern.Matches(simulationText))
            {
                var name = match.Groups[1].Value.Trim();

                if (name.Length < 4) continue;
                if (char.IsDigit(name[0])) continue;
                if (skipTerms.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                if (name.Contains(':') || name.Contains("pts") || name.Contains("reb") || name.Contains("ast")) continue;
                if (name.Contains(" - OUT", StringComparison.OrdinalIgnoreCase)) continue;

                var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length < 2) continue;
                if (!char.IsUpper(words[0][0])) continue;

                if (!validPlayers.Contains(name))
                {
                    invalid.Add(name);
                }
            }

            return invalid.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string SanitizePlayerNames(
            string simulationText,
            List<string> invalidPlayers,
            HashSet<string> validPlayers,
            NBATeamRoster? homeRoster,
            NBATeamRoster? awayRoster,
            HashSet<string> injuredPlayers)
        {
            var replacementPool = new List<string>();
            if (awayRoster != null)
                replacementPool.AddRange(awayRoster.Players
                    .Where(p => !p.IsStarter && !injuredPlayers.Contains(p.Name))
                    .Select(p => p.Name));
            if (homeRoster != null)
                replacementPool.AddRange(homeRoster.Players
                    .Where(p => !p.IsStarter && !injuredPlayers.Contains(p.Name))
                    .Select(p => p.Name));

            if (awayRoster != null)
                replacementPool.AddRange(awayRoster.Players
                    .Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name))
                    .Select(p => p.Name));
            if (homeRoster != null)
                replacementPool.AddRange(homeRoster.Players
                    .Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name))
                    .Select(p => p.Name));

            replacementPool = replacementPool.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var replacementIndex = 0;
            var replacements = new List<string>();

            foreach (var invalidName in invalidPlayers)
            {
                if (replacementIndex < replacementPool.Count)
                {
                    var replacement = replacementPool[replacementIndex % replacementPool.Count];
                    simulationText = simulationText.Replace(invalidName, replacement, StringComparison.OrdinalIgnoreCase);
                    replacements.Add($"{invalidName} -> {replacement}");
                    replacementIndex++;
                }
            }

            if (replacements.Count > 0)
            {
                simulationText += "\n\n---\n\n> *Note: Some player names were corrected to match current team rosters.*";
            }

            return simulationText;
        }

        // ── Mock simulations ──────────────────────────────────────────────────

        private static string GetMlbMockSimulation(string homeTeam, string awayTeam,
            string? homeProbablePitcher = null, string? awayProbablePitcher = null)
        {
            var seed = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var rng  = new Random(seed);

            int awayRuns = rng.Next(0, 10);
            int homeRuns = rng.Next(0, 10);

            while (awayRuns == homeRuns)
            {
                homeRuns = rng.Next(0, 10);
            }

            bool awayWins = awayRuns > homeRuns;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser  = awayWins ? homeTeam : awayTeam;
            int winScore   = Math.Max(awayRuns, homeRuns);
            int loseScore  = Math.Min(awayRuns, homeRuns);
            int margin     = winScore - loseScore;

            int[] awayInnings = DistributeRuns(rng, awayRuns, 9);
            int[] homeInnings = DistributeRuns(rng, homeRuns, 9);

            string awayLine = string.Join(" | ", awayInnings);
            string homeLine = string.Join(" | ", homeInnings);
            string inningHeaders = string.Join(" | ", Enumerable.Range(1, 9));

            int awayHits = awayRuns + rng.Next(2, 6);
            int homeHits = homeRuns + rng.Next(2, 6);
            int awayErrors = rng.Next(0, 3);
            int homeErrors = rng.Next(0, 3);

            string awaySP = awayProbablePitcher ?? "TBD Starter";
            string homeSP = homeProbablePitcher ?? "TBD Starter";

            return $@"# GAME SIMULATION: {awayTeam} @ {homeTeam}

## Final Score
**{awayTeam}**: {awayRuns}
**{homeTeam}**: {homeRuns}

## Line Score

| Team | {inningHeaders} | R | H | E |
|------|{string.Join("|", Enumerable.Repeat("---|", 9))}---|---|---|
| {awayTeam} | {awayLine} | **{awayRuns}** | {awayHits} | {awayErrors} |
| {homeTeam} | {homeLine} | **{homeRuns}** | {homeHits} | {homeErrors} |

## Game Summary
In a {(margin <= 2 ? "tightly contested" : "decisive")} matchup, **{winner}** {(margin <= 2 ? "edges out a win" : "cruises to victory")} {winScore}-{loseScore} over **{loser}**. **{awaySP}** took the mound for {awayTeam} opposite **{homeSP}** for {homeTeam} in a game that featured timely hitting from the winning club.

## Starting Pitchers

| Pitcher | Team | IP | H | R | ER | BB | K |
|---------|------|----|---|---|----|----|---|
| {awaySP} | {awayTeam} | {rng.Next(5, 8)}.0 | {rng.Next(3, 8)} | {rng.Next(1, 5)} | {rng.Next(1, 4)} | {rng.Next(0, 4)} | {rng.Next(3, 9)} |
| {homeSP} | {homeTeam} | {rng.Next(5, 8)}.0 | {rng.Next(3, 8)} | {rng.Next(1, 5)} | {rng.Next(1, 4)} | {rng.Next(0, 4)} | {rng.Next(3, 9)} |

## Team Statistics

| Statistic | {awayTeam} | {homeTeam} |
|-----------|------------|------------|
| Hits | {awayHits} | {homeHits} |
| Errors | {awayErrors} | {homeErrors} |
| LOB | {rng.Next(4, 10)} | {rng.Next(4, 10)} |
| Team AVG | .{rng.Next(200, 320)} | .{rng.Next(200, 320)} |

## Betting Analysis

**Run Line**: {winner} covers the -1.5 run line{(margin >= 2 ? "" : " - PUSH territory")}
**Over/Under**: Total of {awayRuns + homeRuns} runs
**Moneyline**: {winner} wins outright

---

*This is a simulated game for entertainment purposes. Results and statistics are generated for demonstration.*";
        }

        /// <summary>
        /// Distributes a total number of runs randomly across the given number of innings.
        /// </summary>
        private static int[] DistributeRuns(Random rng, int totalRuns, int innings)
        {
            var result = new int[innings];
            for (int r = 0; r < totalRuns; r++)
            {
                result[rng.Next(innings)]++;
            }
            return result;
        }

        private static string GetMockSimulation(string homeTeam, string awayTeam, NBATeamRoster? homeRoster, NBATeamRoster? awayRoster, HashSet<string> injuredPlayers)
        {
            var awayPlayers = awayRoster?.Players
                .Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name))
                .Take(3)
                .ToList() ?? new List<NBAPlayer>();
            var homePlayers = homeRoster?.Players
                .Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name))
                .Take(3)
                .ToList() ?? new List<NBAPlayer>();

            string awayPlayer1 = awayPlayers.Count > 0 ? awayPlayers[0].Name : "Star Player";
            string awayPlayer2 = awayPlayers.Count > 1 ? awayPlayers[1].Name : "Supporting Player";
            string awayPlayer3 = awayPlayers.Count > 2 ? awayPlayers[2].Name : "Role Player";

            string homePlayer1 = homePlayers.Count > 0 ? homePlayers[0].Name : "Star Player";
            string homePlayer2 = homePlayers.Count > 1 ? homePlayers[1].Name : "Supporting Player";
            string homePlayer3 = homePlayers.Count > 2 ? homePlayers[2].Name : "Role Player";

            var seed = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var random = new Random(seed);

            int awayScore = random.Next(95, 125);
            int homeScore = random.Next(95, 125);

            if (Math.Abs(awayScore - homeScore) > 15)
            {
                if (awayScore > homeScore)
                    awayScore = homeScore + random.Next(1, 12);
                else
                    homeScore = awayScore + random.Next(1, 12);
            }

            bool awayWins = awayScore > homeScore;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser = awayWins ? homeTeam : awayTeam;
            int margin = Math.Abs(awayScore - homeScore);

            int player1Points = random.Next(25, 38);
            int player1Rebounds = random.Next(5, 13);
            int player1Assists = random.Next(4, 11);
            int player2Points = random.Next(18, 28);
            int player2Rebounds = random.Next(4, 9);
            int player3Points = random.Next(12, 22);
            int player3Rebounds = random.Next(8, 14);
            int homePlayer1Points = random.Next(23, 35);
            int homePlayer1Assists = random.Next(6, 12);
            int homePlayer2Points = random.Next(20, 30);

            int q1Away = random.Next(22, 32);
            int q1Home = random.Next(22, 32);
            int halfAway = random.Next(48, 62);
            int halfHome = random.Next(48, 62);
            int q3Away = random.Next(72, 92);
            int q3Home = random.Next(72, 92);

            var injuryReport = "";
            if (injuredPlayers.Any())
            {
                injuryReport = "## Injury Report\n\n";

                var awayInjured = injuredPlayers.Where(p => awayRoster?.Players.Any(rp => rp.Name.Equals(p, StringComparison.OrdinalIgnoreCase)) ?? false).ToList();
                var homeInjured = injuredPlayers.Where(p => homeRoster?.Players.Any(rp => rp.Name.Equals(p, StringComparison.OrdinalIgnoreCase)) ?? false).ToList();

                if (awayInjured.Any())
                {
                    injuryReport += $"**{awayTeam}:**\n";
                    foreach (var player in awayInjured)
                        injuryReport += $"- **{player}** - OUT (Injured)\n";
                    injuryReport += "\n";
                }

                if (homeInjured.Any())
                {
                    injuryReport += $"**{homeTeam}:**\n";
                    foreach (var player in homeInjured)
                        injuryReport += $"- **{player}** - OUT (Injured)\n";
                    injuryReport += "\n";
                }
            }

            return $@"# GAME SIMULATION: {awayTeam} @ {homeTeam}

{injuryReport}## Final Score
**{awayTeam}**: {awayScore}  
**{homeTeam}**: {homeScore}

## Game Summary
In an exciting {(margin < 5 ? "nail-biter" : "hard-fought battle")}, **{winner}** {(margin < 5 ? "narrowly defeats" : "edges")} **{loser}** {awayScore}-{homeScore}. {awayPlayer1} was the driving force for {awayTeam} with {player1Points} points, while {homePlayer1} led {homeTeam} with an impressive {homePlayer1Points}-point, {homePlayer1Assists}-assist performance. The game featured multiple lead changes and came down to execution in the final minutes.

## Key Performers

### {awayTeam} Top Players
- **{awayPlayer1}**: {player1Points} pts, {player1Rebounds} reb, {player1Assists} ast - Dominated on both ends of the floor with efficient scoring and clutch plays
- **{awayPlayer2}**: {player2Points} pts, {player2Rebounds} reb, 3 stl - Provided crucial defensive pressure and timely buckets
- **{awayPlayer3}**: {player3Points} pts, {player3Rebounds} reb - Controlled the paint and won the battle on the boards

### {homeTeam} Top Players
- **{homePlayer1}**: {homePlayer1Points} pts, {homePlayer1Assists} ast, 5 reb - Orchestrated the offense brilliantly and created opportunities for teammates
- **{homePlayer2}**: {homePlayer2Points} pts, 7 reb - Kept the team competitive with aggressive attacks and clutch shooting
- **{homePlayer3}**: {random.Next(15, 22)} pts, 4 ast - Provided steady contribution and solid two-way play

## Quarter-by-Quarter Breakdown

**1st Quarter**: Both teams trade baskets early. {awayPlayer1} establishes himself while {homePlayer1} answers back.  
Score: {awayTeam} {q1Away}, {homeTeam} {q1Home}

**2nd Quarter**: {homePlayer2} heats up, but {awayPlayer2} responds with tough defense and transition buckets.  
Halftime Score: {awayTeam} {halfAway}, {homeTeam} {halfHome}

**3rd Quarter**: {awayPlayer3} dominates inside, giving {awayTeam} momentum. {homeTeam} battles to stay close.  
End of 3rd: {awayTeam} {q3Away}, {homeTeam} {q3Home}

**4th Quarter**: {(awayWins ? $"{awayPlayer1} takes over" : $"{homePlayer1} leads the comeback")} in crunch time. {winner} makes key plays down the stretch to secure the victory.  
Final Score: {awayTeam} {awayScore}, {homeTeam} {homeScore}

## Team Statistics Comparison

| Statistic | {awayTeam} | {homeTeam} |
|-----------|------------|------------|
| FG% | {random.Next(44, 52)}% | {random.Next(43, 51)}% |
| 3-Pointers | {random.Next(10, 16)}/{random.Next(28, 38)} ({random.Next(32, 45)}%) | {random.Next(9, 15)}/{random.Next(27, 40)} ({random.Next(30, 43)}%) |
| Rebounds | {random.Next(38, 48)} | {random.Next(40, 50)} |
| Turnovers | {random.Next(10, 16)} | {random.Next(11, 17)} |
| Fast-break Points | {random.Next(12, 22)} | {random.Next(10, 20)} |

## Betting Analysis

**Spread**: {winner} wins by {margin} points - {(margin > 5 ? "likely covers the spread" : "close to the spread line")}  
**Over/Under**: Total of {awayScore + homeScore} points  
**Moneyline**: {winner} wins outright

---

*This is a simulated game for entertainment purposes. Results and statistics are generated for demonstration.*";
        }
    }
}
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

            // ── NBA simulation ────────────────────────────────────────────────
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
                        injuryInfo = "<p><strong>INJURY REPORT (Players OUT for this game):</strong></p><ul>";
                        if (awayInjuries.Any())
                            injuryInfo += $"<li>{awayTeam}: {string.Join(", ", awayInjuries.Select(i => $"<strong>{i.PlayerName}</strong> ({i.InjuryDescription})"))}</li>";
                        if (homeInjuries.Any())
                            injuryInfo += $"<li>{homeTeam}: {string.Join(", ", homeInjuries.Select(i => $"<strong>{i.PlayerName}</strong> ({i.InjuryDescription})"))}</li>";
                        injuryInfo += "</ul>";
                    }
                }

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
{awayTeam} Available Roster:
Starting 5: {string.Join(", ", awayStarters)}
Key Bench: {string.Join(", ", awayBench)}

{homeTeam} Available Roster:
Starting 5: {string.Join(", ", homeStarters)}
Key Bench: {string.Join(", ", homeBench)}

COMPLETE VALID PLAYER LIST (use ONLY these exact names):
{awayTeam}: {string.Join(", ", awayPlayerList)}
{homeTeam}: {string.Join(", ", homePlayerList)}";

                    rosterSystemRule =
                        "ONLY use player names from the COMPLETE VALID PLAYER LIST provided. " +
                        "Do NOT invent, hallucinate, or reference any player not in that list.";
                }
                else
                {
                    rosterInfo = $@"
ROSTER DATA UNAVAILABLE — Live roster feed could not be reached.
Use ONLY players you are certain currently play for {awayTeam} and {homeTeam} as of today.
Do NOT include any player who has been traded, waived, or retired.";

                    rosterSystemRule =
                        "Roster data is unavailable. Use ONLY players you are certain are on each team RIGHT NOW. " +
                        "If you are not 100% certain a player is still on the team, do NOT mention them. " +
                        "Do NOT include players who have been traded, released, or retired.";
                }

                var prompt = $@"Generate a FRESH, UNIQUE sports game simulation for an NBA game between {awayTeam} (away) and {homeTeam} (home).

SIMULATION ID: {simulationId}
GENERATED AT: {timestamp}

{rosterInfo}

OUTPUT FORMAT — CRITICAL:
- You MUST return valid HTML only. No Markdown whatsoever.
- Use <h2> for section headings, <h3> for sub-headings.
- Use <p> for paragraphs, <ul>/<li> for lists, <table>/<thead>/<tbody>/<tr>/<th>/<td> for tables.
- Use <strong> for emphasis instead of ** or __ markup.
- Do NOT wrap output in ```html``` fences or any code block — return raw HTML directly.

CRITICAL INSTRUCTIONS - ROSTER VALIDATION:
- You MUST ONLY use players from the COMPLETE VALID PLAYER LIST above.
- DO NOT invent, fabricate, or hallucinate any player names.
- DO NOT use players who have been traded away or are on other teams.
- Every player name in your simulation MUST appear exactly as written in the valid player list.
- If you are unsure whether a player is on the team, DO NOT include them.

CRITICAL INSTRUCTIONS - INJURY RULES:
- At the very top of your simulation output include a section with id=""injury-report"" listing all injured players as OUT.
- DO NOT include injured players in any game action, statistics, or narratives.

SIMULATION REQUIREMENTS:
- This is simulation #{simulationId} - make it COMPLETELY DIFFERENT from any previous simulations.
- Use ONLY the players listed in the available roster above.
- Make this simulation UNIQUE and SPECIFIC to {awayTeam} vs {homeTeam}.
- VARY the final score each time.
- CREATE DIFFERENT game narratives — sometimes close games, blowouts, or comeback wins.

Include the following sections:
1. <h2>Final Score</h2> — realistic varied score
2. <h2>Game Summary</h2> — 2-3 sentences mentioning specific players
3. <h2>Key Performers</h2> — 3-5 players with realistic stats in an HTML table
4. <h2>Quarter-by-Quarter Breakdown</h2> — key moments each quarter
5. <h2>Team Statistics</h2> — HTML comparison table (FG%, 3PT%, rebounds, turnovers, fast-break points)
6. <h2>Betting Analysis</h2> — spread, over/under, moneyline impact

FINAL REMINDER — VALID PLAYERS ONLY:
{(rosterDataAvailable ? $"{awayTeam}: {string.Join(", ", awayPlayerList)}\n{homeTeam}: {string.Join(", ", homePlayerList)}\nDo NOT use any player not listed above." : "Use only players you are 100% certain currently play for each team.")}";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an expert NBA analyst who creates detailed, realistic game simulations. " +
                        $"CRITICAL RULES: " +
                        $"1) Return ONLY valid HTML. Do NOT use Markdown syntax (no **, no ##, no -, no ```). " +
                        $"2) Start output with an <section id=\"injury-report\"> listing all injured players as OUT. " +
                        $"3) NEVER include injured players in game action or statistics. " +
                        $"4) {rosterSystemRule} " +
                        $"5) Each simulation must be UNIQUE. Generate simulation #{simulationId} with fresh content."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions { Temperature = 0.9f };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var simulationText = response.Value.Content[0].Text;

                // Strip any accidental markdown code fences the model may add
                simulationText = StripCodeFences(simulationText);

                if (rosterDataAvailable && validPlayers.Count > 0)
                {
                    var invalidPlayers = FindInvalidPlayerNames(simulationText, validPlayers, homeTeam, awayTeam);
                    if (invalidPlayers.Count > 0)
                    {
                        _logger.LogWarning("AI simulation #{SimulationId} contains {Count} invalid player name(s): {Players}",
                            simulationId, invalidPlayers.Count, string.Join(", ", invalidPlayers));
                        simulationText = SanitizePlayerNames(simulationText, invalidPlayers, validPlayers, homeRoster, awayRoster, injuredPlayersNba);
                    }
                }
                else if (!rosterDataAvailable)
                {
                    _logger.LogWarning("Simulation #{SimulationId}: roster data was unavailable — player validation skipped", simulationId);
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
SCHEDULED STARTING PITCHERS (from official MLB schedule):
- {awayTeam} starter: {awayProbablePitcher ?? "TBD"}
- {homeTeam} starter: {homeProbablePitcher ?? "TBD"}

CRITICAL: You MUST use these exact pitchers as the starting pitchers in your simulation.
If a pitcher is listed as TBD, choose a realistic current-rotation pitcher for that team.
Base their stat lines on their real 2024-2025 performance (ERA, K rate, WHIP, etc.).
";
                }

                var prompt = $@"Generate a FRESH, UNIQUE MLB baseball game simulation between {awayTeam} (away) and {homeTeam} (home).

SIMULATION ID: {simulationId}
GENERATED AT: {timestamp}
{pitcherSection}

OUTPUT FORMAT — CRITICAL:
- You MUST return valid HTML only. No Markdown whatsoever.
- Use <h2> for section headings, <h3> for sub-headings.
- Use <p> for paragraphs, <ul>/<li> for lists, <table>/<thead>/<tbody>/<tr>/<th>/<td> for tables.
- Use <strong> for emphasis. Do NOT use ** or ## or any Markdown syntax.
- Do NOT wrap output in ```html``` fences — return raw HTML directly.

SIMULATION REQUIREMENTS:
- This is simulation #{simulationId} - make it COMPLETELY DIFFERENT from any previous simulations.
- Use REAL current-roster players for both {awayTeam} and {homeTeam}.
- DO NOT invent or hallucinate player names.
- VARY the final score each time.
- THE GAME MUST HAVE A WINNER — baseball games CANNOT end in a tie.
- If tied after 9 innings, simulate extra innings until one team wins.
- The home team ALWAYS bats last.

Include the following sections:
1. <h2>Final Score</h2> with an HTML line-score table (runs per inning, R, H, E)
2. <h2>Game Summary</h2> — 2-3 sentences mentioning starting pitchers by name
3. <h2>Starting Pitchers</h2> — HTML table (IP, H, R, ER, BB, K). Use the SCHEDULED STARTERS above.
4. <h2>Key Performers</h2> — HTML table with 4-6 players (batting or pitching lines)
5. <h2>Inning-by-Inning Breakdown</h2> — key scoring innings and dramatic moments
6. <h2>Team Statistics</h2> — HTML table (hits, errors, LOB, team AVG, bullpen ERA)
7. <h2>Pitching Summary</h2> — HTML table of all pitchers used; first entry per team must be the scheduled starter
8. <h2>Betting Analysis</h2> — run line, over/under, moneyline impact

CRITICAL REQUIREMENTS:
- Use the EXACT scheduled starting pitchers listed above.
- Use REAL players currently on {awayTeam} and {homeTeam} rosters.
- The final score MUST NOT be a tie.
- Mention the home ballpark and how it affected play.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an expert MLB baseball analyst who creates detailed, realistic game simulations. " +
                        $"CRITICAL RULES: " +
                        $"1) Return ONLY valid HTML. Do NOT use Markdown (no **, no ##, no ```, no - lists). " +
                        $"2) Use only real current-roster MLB players. " +
                        $"3) You MUST use the scheduled starting pitchers provided — do NOT substitute different starters. " +
                        $"4) Baseball games CANNOT end in a tie — there must always be a winner. " +
                        $"5) Each simulation must be UNIQUE. Generate simulation #{simulationId} with fresh content."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions { Temperature = 0.9f };

                var response = await _chatClient!.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var simulationText = StripCodeFences(response.Value.Content[0].Text);

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

        /// <summary>Removes accidental ```html ... ``` or ``` ... ``` fences some models add.</summary>
        private static string StripCodeFences(string text)
        {
            text = text.Trim();
            if (text.StartsWith("```html", StringComparison.OrdinalIgnoreCase))
                text = text["```html".Length..].Trim();
            else if (text.StartsWith("```"))
                text = text[3..].Trim();
            if (text.EndsWith("```"))
                text = text[..^3].Trim();
            return text;
        }

        private static HashSet<string> BuildValidPlayerSet(
            NBATeamRoster? homeRoster,
            NBATeamRoster? awayRoster,
            HashSet<string> injuredPlayers)
        {
            var valid = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (homeRoster != null)
                foreach (var p in homeRoster.Players.Where(p => !injuredPlayers.Contains(p.Name)))
                    valid.Add(p.Name);
            if (awayRoster != null)
                foreach (var p in awayRoster.Players.Where(p => !injuredPlayers.Contains(p.Name)))
                    valid.Add(p.Name);
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
                "Halftime", "Team Statistics Comparison", "COMPLETE VALID PLAYER LIST",
                "Available Roster", "ROSTER DATA UNAVAILABLE", "Starting", "Key Bench"
            };

            // Match names inside HTML tags like <strong>Jayson Tatum</strong> and plain prose
            var patterns = new[]
            {
                new Regex(@"<strong>([A-Z][a-zA-Z'-]+ [A-Z][a-zA-Z'-]+(?:\s+[A-Z][a-zA-Z'-]+)?)</strong>", RegexOptions.Compiled),
                new Regex(@"\b([A-Z][a-z'-]+\s+[A-Z][a-z'-]+(?:\s+[A-Z][a-z'-]+)?)\b",                     RegexOptions.Compiled),
            };

            foreach (var pattern in patterns)
            {
                foreach (Match match in pattern.Matches(simulationText))
                {
                    var name = match.Groups[1].Value.Trim();

                    if (name.Length < 4) continue;
                    if (char.IsDigit(name[0])) continue;
                    if (skipTerms.Any(t => name.Contains(t, StringComparison.OrdinalIgnoreCase))) continue;
                    if (name.Contains(':') || name.Contains("pts") || name.Contains("reb") || name.Contains("ast")) continue;
                    if (name.Contains("OUT", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains('%') || name.Contains('/')) continue;

                    var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length < 2) continue;

                    if (!validPlayers.Contains(name))
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
                replacementPool.AddRange(awayRoster.Players.Where(p => !p.IsStarter && !injuredPlayers.Contains(p.Name)).Select(p => p.Name));
            if (homeRoster != null)
                replacementPool.AddRange(homeRoster.Players.Where(p => !p.IsStarter && !injuredPlayers.Contains(p.Name)).Select(p => p.Name));
            if (awayRoster != null)
                replacementPool.AddRange(awayRoster.Players.Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name)).Select(p => p.Name));
            if (homeRoster != null)
                replacementPool.AddRange(homeRoster.Players.Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name)).Select(p => p.Name));

            replacementPool = replacementPool.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var replacementIndex = 0;
            var madeReplacements = false;

            foreach (var invalidName in invalidPlayers)
            {
                if (replacementIndex < replacementPool.Count)
                {
                    var replacement = replacementPool[replacementIndex % replacementPool.Count];
                    simulationText = simulationText.Replace(invalidName, replacement, StringComparison.OrdinalIgnoreCase);
                    replacementIndex++;
                    madeReplacements = true;
                }
            }

            if (madeReplacements)
                simulationText += "\n<p><em>Note: Some player names were corrected to match current team rosters.</em></p>";

            return simulationText;
        }

        // ── Mock simulations ──────────────────────────────────────────────────

        private static string GetMlbMockSimulation(string homeTeam, string awayTeam,
            string? homeProbablePitcher = null, string? awayProbablePitcher = null)
        {
            var seed = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var rng = new Random(seed);

            int awayRuns = rng.Next(0, 10);
            int homeRuns = rng.Next(0, 10);
            while (awayRuns == homeRuns) homeRuns = rng.Next(0, 10);

            bool awayWins  = awayRuns > homeRuns;
            string winner  = awayWins ? awayTeam : homeTeam;
            string loser   = awayWins ? homeTeam : awayTeam;
            int winScore   = Math.Max(awayRuns, homeRuns);
            int loseScore  = Math.Min(awayRuns, homeRuns);
            int margin     = winScore - loseScore;

            int[] awayInnings = DistributeRuns(rng, awayRuns, 9);
            int[] homeInnings = DistributeRuns(rng, homeRuns, 9);

            int awayHits   = awayRuns + rng.Next(2, 6);
            int homeHits   = homeRuns + rng.Next(2, 6);
            int awayErrors = rng.Next(0, 3);
            int homeErrors = rng.Next(0, 3);

            string awaySP = awayProbablePitcher ?? "TBD Starter";
            string homeSP = homeProbablePitcher ?? "TBD Starter";

            var inningHeaders = string.Join("", Enumerable.Range(1, 9).Select(i => $"<th>{i}</th>"));
            var awayInningCells = string.Join("", awayInnings.Select(r => $"<td>{r}</td>"));
            var homeInningCells = string.Join("", homeInnings.Select(r => $"<td>{r}</td>"));

            return $@"<h1>GAME SIMULATION: {awayTeam} @ {homeTeam}</h1>

<h2>Final Score</h2>
<p><strong>{awayTeam}:</strong> {awayRuns} &nbsp; <strong>{homeTeam}:</strong> {homeRuns}</p>

<h2>Line Score</h2>
<table>
  <thead><tr><th>Team</th>{inningHeaders}<th>R</th><th>H</th><th>E</th></tr></thead>
  <tbody>
    <tr><td><strong>{awayTeam}</strong></td>{awayInningCells}<td><strong>{awayRuns}</strong></td><td>{awayHits}</td><td>{awayErrors}</td></tr>
    <tr><td><strong>{homeTeam}</strong></td>{homeInningCells}<td><strong>{homeRuns}</strong></td><td>{homeHits}</td><td>{homeErrors}</td></tr>
  </tbody>
</table>

<h2>Game Summary</h2>
<p>In a {(margin <= 2 ? "tightly contested" : "decisive")} matchup, <strong>{winner}</strong> {(margin <= 2 ? "edges out a win" : "cruises to victory")} {winScore}-{loseScore} over <strong>{loser}</strong>. <strong>{awaySP}</strong> took the mound for {awayTeam} opposite <strong>{homeSP}</strong> for {homeTeam} in a game that featured timely hitting from the winning club.</p>

<h2>Starting Pitchers</h2>
<table>
  <thead><tr><th>Pitcher</th><th>Team</th><th>IP</th><th>H</th><th>R</th><th>ER</th><th>BB</th><th>K</th></tr></thead>
  <tbody>
    <tr><td>{awaySP}</td><td>{awayTeam}</td><td>{rng.Next(5, 8)}.0</td><td>{rng.Next(3, 8)}</td><td>{rng.Next(1, 5)}</td><td>{rng.Next(1, 4)}</td><td>{rng.Next(0, 4)}</td><td>{rng.Next(3, 9)}</td></tr>
    <tr><td>{homeSP}</td><td>{homeTeam}</td><td>{rng.Next(5, 8)}.0</td><td>{rng.Next(3, 8)}</td><td>{rng.Next(1, 5)}</td><td>{rng.Next(1, 4)}</td><td>{rng.Next(0, 4)}</td><td>{rng.Next(3, 9)}</td></tr>
  </tbody>
</table>

<h2>Team Statistics</h2>
<table>
  <thead><tr><th>Statistic</th><th>{awayTeam}</th><th>{homeTeam}</th></tr></thead>
  <tbody>
    <tr><td>Hits</td><td>{awayHits}</td><td>{homeHits}</td></tr>
    <tr><td>Errors</td><td>{awayErrors}</td><td>{homeErrors}</td></tr>
    <tr><td>LOB</td><td>{rng.Next(4, 10)}</td><td>{rng.Next(4, 10)}</td></tr>
    <tr><td>Team AVG</td><td>.{rng.Next(200, 320)}</td><td>.{rng.Next(200, 320)}</td></tr>
  </tbody>
</table>

<h2>Betting Analysis</h2>
<ul>
  <li><strong>Run Line:</strong> {winner} covers the -1.5 run line{(margin >= 2 ? "" : " — PUSH territory")}</li>
  <li><strong>Over/Under:</strong> Total of {awayRuns + homeRuns} runs</li>
  <li><strong>Moneyline:</strong> {winner} wins outright</li>
</ul>

<p><em>This is a simulated game for entertainment purposes only.</em></p>";
        }

        private static int[] DistributeRuns(Random rng, int totalRuns, int innings)
        {
            var result = new int[innings];
            for (int r = 0; r < totalRuns; r++)
                result[rng.Next(innings)]++;
            return result;
        }

        private static string GetMockSimulation(string homeTeam, string awayTeam,
            NBATeamRoster? homeRoster, NBATeamRoster? awayRoster, HashSet<string> injuredPlayers)
        {
            var awayPlayers = awayRoster?.Players.Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name)).Take(3).ToList() ?? new();
            var homePlayers = homeRoster?.Players.Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name)).Take(3).ToList() ?? new();

            string ap1 = awayPlayers.Count > 0 ? awayPlayers[0].Name : "Star Player";
            string ap2 = awayPlayers.Count > 1 ? awayPlayers[1].Name : "Supporting Player";
            string ap3 = awayPlayers.Count > 2 ? awayPlayers[2].Name : "Role Player";
            string hp1 = homePlayers.Count > 0 ? homePlayers[0].Name : "Star Player";
            string hp2 = homePlayers.Count > 1 ? homePlayers[1].Name : "Supporting Player";
            string hp3 = homePlayers.Count > 2 ? homePlayers[2].Name : "Role Player";

            var seed   = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var rng    = new Random(seed);

            int awayScore = rng.Next(95, 125);
            int homeScore = rng.Next(95, 125);
            if (Math.Abs(awayScore - homeScore) > 15)
            {
                if (awayScore > homeScore) awayScore = homeScore + rng.Next(1, 12);
                else                       homeScore = awayScore + rng.Next(1, 12);
            }

            bool awayWins = awayScore > homeScore;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser  = awayWins ? homeTeam : awayTeam;
            int margin    = Math.Abs(awayScore - homeScore);

            int p1pts = rng.Next(25, 38), p1reb = rng.Next(5, 13),  p1ast = rng.Next(4, 11);
            int p2pts = rng.Next(18, 28), p2reb = rng.Next(4, 9);
            int p3pts = rng.Next(12, 22), p3reb = rng.Next(8, 14);
            int h1pts = rng.Next(23, 35), h1ast = rng.Next(6, 12);
            int h2pts = rng.Next(20, 30);

            int q1a = rng.Next(22, 32), q1h = rng.Next(22, 32);
            int hfa = rng.Next(48, 62), hfh = rng.Next(48, 62);
            int q3a = rng.Next(72, 92), q3h = rng.Next(72, 92);

            var injurySection = "";
            if (injuredPlayers.Any())
            {
                var awayInj = injuredPlayers.Where(p => awayRoster?.Players.Any(rp => rp.Name.Equals(p, StringComparison.OrdinalIgnoreCase)) ?? false).ToList();
                var homeInj = injuredPlayers.Where(p => homeRoster?.Players.Any(rp => rp.Name.Equals(p, StringComparison.OrdinalIgnoreCase)) ?? false).ToList();
                injurySection = "<section id=\"injury-report\"><h2>Injury Report</h2><ul>";
                foreach (var p in awayInj) injurySection += $"<li><strong>{p}</strong> ({awayTeam}) — OUT (Injured)</li>";
                foreach (var p in homeInj) injurySection += $"<li><strong>{p}</strong> ({homeTeam}) — OUT (Injured)</li>";
                injurySection += "</ul></section>";
            }

            return $@"{injurySection}
<h1>GAME SIMULATION: {awayTeam} @ {homeTeam}</h1>

<h2>Final Score</h2>
<p><strong>{awayTeam}:</strong> {awayScore} &nbsp; <strong>{homeTeam}:</strong> {homeScore}</p>

<h2>Game Summary</h2>
<p>In an exciting {(margin < 5 ? "nail-biter" : "hard-fought battle")}, <strong>{winner}</strong> {(margin < 5 ? "narrowly defeats" : "edges")} <strong>{loser}</strong> {awayScore}-{homeScore}. <strong>{ap1}</strong> was the driving force for {awayTeam} with {p1pts} points, while <strong>{hp1}</strong> led {homeTeam} with an impressive {h1pts}-point, {h1ast}-assist performance. The game featured multiple lead changes and came down to execution in the final minutes.</p>

<h2>Key Performers</h2>
<h3>{awayTeam}</h3>
<table>
  <thead><tr><th>Player</th><th>PTS</th><th>REB</th><th>AST</th><th>Notes</th></tr></thead>
  <tbody>
    <tr><td><strong>{ap1}</strong></td><td>{p1pts}</td><td>{p1reb}</td><td>{p1ast}</td><td>Dominated on both ends with clutch plays</td></tr>
    <tr><td><strong>{ap2}</strong></td><td>{p2pts}</td><td>{p2reb}</td><td>3</td><td>Key defensive pressure and timely buckets</td></tr>
    <tr><td><strong>{ap3}</strong></td><td>{p3pts}</td><td>{p3reb}</td><td>2</td><td>Controlled the paint</td></tr>
  </tbody>
</table>
<h3>{homeTeam}</h3>
<table>
  <thead><tr><th>Player</th><th>PTS</th><th>REB</th><th>AST</th><th>Notes</th></tr></thead>
  <tbody>
    <tr><td><strong>{hp1}</strong></td><td>{h1pts}</td><td>5</td><td>{h1ast}</td><td>Orchestrated the offense brilliantly</td></tr>
    <tr><td><strong>{hp2}</strong></td><td>{h2pts}</td><td>7</td><td>4</td><td>Aggressive attacks and clutch shooting</td></tr>
    <tr><td><strong>{hp3}</strong></td><td>{rng.Next(15, 22)}</td><td>4</td><td>3</td><td>Steady two-way contribution</td></tr>
  </tbody>
</table>

<h2>Quarter-by-Quarter Breakdown</h2>
<table>
  <thead><tr><th>Quarter</th><th>{awayTeam}</th><th>{homeTeam}</th><th>Key Moment</th></tr></thead>
  <tbody>
    <tr><td>1st</td><td>{q1a}</td><td>{q1h}</td><td><strong>{ap1}</strong> establishes early while <strong>{hp1}</strong> answers back</td></tr>
    <tr><td>Half</td><td>{hfa}</td><td>{hfh}</td><td><strong>{hp2}</strong> heats up; <strong>{ap2}</strong> responds with tough defense</td></tr>
    <tr><td>3rd</td><td>{q3a}</td><td>{q3h}</td><td><strong>{ap3}</strong> dominates inside giving {awayTeam} momentum</td></tr>
    <tr><td>Final</td><td>{awayScore}</td><td>{homeScore}</td><td>{(awayWins ? $"<strong>{ap1}</strong> takes over in crunch time" : $"<strong>{hp1}</strong> leads the comeback")}</td></tr>
  </tbody>
</table>

<h2>Team Statistics</h2>
<table>
  <thead><tr><th>Statistic</th><th>{awayTeam}</th><th>{homeTeam}</th></tr></thead>
  <tbody>
    <tr><td>FG%</td><td>{rng.Next(44, 52)}%</td><td>{rng.Next(43, 51)}%</td></tr>
    <tr><td>3-Pointers Made</td><td>{rng.Next(10, 16)}</td><td>{rng.Next(9, 15)}</td></tr>
    <tr><td>Rebounds</td><td>{rng.Next(38, 48)}</td><td>{rng.Next(40, 50)}</td></tr>
    <tr><td>Turnovers</td><td>{rng.Next(10, 16)}</td><td>{rng.Next(11, 17)}</td></tr>
    <tr><td>Fast-Break Points</td><td>{rng.Next(12, 22)}</td><td>{rng.Next(10, 20)}</td></tr>
  </tbody>
</table>

<h2>Betting Analysis</h2>
<ul>
  <li><strong>Spread:</strong> {winner} wins by {margin} points — {(margin > 5 ? "likely covers the spread" : "close to the spread line")}</li>
  <li><strong>Over/Under:</strong> Total of {awayScore + homeScore} points</li>
  <li><strong>Moneyline:</strong> {winner} wins outright</li>
</ul>

<p><em>This is a simulated game for entertainment purposes only.</em></p>";
        }
    }
}
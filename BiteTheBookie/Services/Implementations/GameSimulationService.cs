using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.Models;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BiteTheBookie.Services.Implementations
{
    public class GameSimulationService : IGameSimulationService
    {
        private readonly ChatClient? _chatClient;
        private readonly ILogger<GameSimulationService> _logger;

        private static readonly Regex StrongNamePattern = new(
            @"<strong>([A-Z][a-zA-Z''-]+(?: [A-Z][a-zA-Z''-]+){1,2})</strong>",
            RegexOptions.Compiled);

        public GameSimulationService(ChatClient? chatClient, ILogger<GameSimulationService> logger)
        {
            _logger = logger;
            _chatClient = chatClient;

            if (_chatClient == null)
                _logger.LogWarning("Azure OpenAI ChatClient is not configured. Will use mock simulation.");
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

            _logger.LogInformation(
                "Generating simulation #{SimulationId} for {HomeTeam} vs {AwayTeam} ({League}) at {Timestamp}",
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

            if (league.Equals("MLB", StringComparison.OrdinalIgnoreCase))
            {
                return await GenerateMlbSimulationAsync(homeTeam, awayTeam, simulationId, timestamp,
                    gameTime, cancellationToken, homeProbablePitcher, awayProbablePitcher);
            }

            // ── NBA ───────────────────────────────────────────────────────────
            var injuredPlayersNba = injuries?
                .Where(i => i.InjuryStatus.Equals("Out", StringComparison.OrdinalIgnoreCase))
                .Select(i => i.PlayerName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

            // ── Fetch rosters from OpenAI ─────────────────────────────────────
            _logger.LogInformation("Fetching NBA rosters from OpenAI for {AwayTeam} and {HomeTeam}", awayTeam, homeTeam);

            // Fetch both rosters concurrently for speed
            var rosterTasks = await Task.WhenAll(
                FetchOpenAIRosterAsync(awayTeam, awayRoster?.TeamCode ?? awayTeam, cancellationToken),
                FetchOpenAIRosterAsync(homeTeam, homeRoster?.TeamCode ?? homeTeam, cancellationToken));

            // Do NOT fall back to ESPN — a null here intentionally triggers the hard-stop
            // below so the simulation never runs with stale or incorrect player data.
            awayRoster = rosterTasks[0];
            homeRoster = rosterTasks[1];

            if (injuredPlayersNba.Any())
            {
                _logger.LogWarning("EXCLUDING {Count} injured players: {Players}",
                    injuredPlayersNba.Count, string.Join(", ", injuredPlayersNba));

                foreach (var inj in injuries!.Where(i => i.InjuryStatus.Equals("Out", StringComparison.OrdinalIgnoreCase)))
                    _logger.LogWarning("  {Player} ({Team}) - OUT: {Description}",
                        inj.PlayerName, inj.TeamCode, inj.InjuryDescription);
            }

            var validPlayers = BuildValidPlayerSet(homeRoster, awayRoster, injuredPlayersNba);

            try
            {
                _logger.LogInformation("Calling Azure OpenAI for {HomeTeam} vs {AwayTeam}...", homeTeam, awayTeam);

                var awayPlayerList = awayRoster?.Players
                    .Where(p => !injuredPlayersNba.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList() ?? new List<string>();
                var homePlayerList = homeRoster?.Players
                    .Where(p => !injuredPlayersNba.Contains(p.Name))
                    .Select(p => p.Name)
                    .ToList() ?? new List<string>();

                var rosterDataAvailable = awayPlayerList.Count > 0 && homePlayerList.Count > 0;

                if (!rosterDataAvailable)
                {
                    _logger.LogWarning(
                        "Simulation aborted for {AwayTeam} @ {HomeTeam} — live roster unavailable for one or both teams.",
                        awayTeam, homeTeam);

                    return $@"<section class=""alert alert-warning"">
  <h2>Simulation Unavailable</h2>
  <p>The current roster could not be retrieved for
    <strong>{awayTeam}</strong> and/or <strong>{homeTeam}</strong> at this time.</p>
  <p>To protect accuracy, this simulation will not run with unverified player data.
    Please try again in a few minutes.</p>
</section>";
                }

                var injuryInfo = "";
                if (injuredPlayersNba.Any())
                {
                    var awayInj = injuries?.Where(i => i.TeamCode == awayRoster?.TeamCode && injuredPlayersNba.Contains(i.PlayerName)).ToList() ?? new();
                    var homeInj = injuries?.Where(i => i.TeamCode == homeRoster?.TeamCode && injuredPlayersNba.Contains(i.PlayerName)).ToList() ?? new();

                    if (awayInj.Any() || homeInj.Any())
                    {
                        injuryInfo = "<p><strong>INJURY REPORT (Players OUT for this game):</strong></p><ul>";
                        if (awayInj.Any())
                            injuryInfo += $"<li>{awayTeam}: {string.Join(", ", awayInj.Select(i => $"<strong>{i.PlayerName}</strong> ({i.InjuryDescription})"))}</li>";
                        if (homeInj.Any())
                            injuryInfo += $"<li>{homeTeam}: {string.Join(", ", homeInj.Select(i => $"<strong>{i.PlayerName}</strong> ({i.InjuryDescription})"))}</li>";
                        injuryInfo += "</ul>";
                    }
                }

                static string PlayerLine(NBAPlayer p)
                {
                    var stats = (p.PointsPerGame > 0 || p.ReboundsPerGame > 0 || p.AssistsPerGame > 0)
                        ? $"  ~{p.PointsPerGame:F1} PPG / {p.ReboundsPerGame:F1} RPG / {p.AssistsPerGame:F1} APG"
                        : "  (stats unavailable)";
                    return $"  • {p.Name} ({p.Position}){stats}";
                }

                var awayRosterLines = awayRoster!.Players
                    .Where(p => !injuredPlayersNba.Contains(p.Name))
                    .Select(PlayerLine);
                var homeRosterLines = homeRoster!.Players
                    .Where(p => !injuredPlayersNba.Contains(p.Name))
                    .Select(PlayerLine);

                var rosterBlock = $@"=== AUTHORITATIVE ROSTER — TODAY'S DATE: {DateTime.UtcNow:yyyy-MM-dd} ===
These are the ONLY players you are permitted to mention anywhere in the simulation.
This list was fetched live from OpenAI moments ago. Any player NOT on this list is
no longer on the team — they have been traded, waived, injured, or retired.
Season averages are listed beside each name — use them as the BASELINE for stat lines.
A player's single-game line may vary ±30% from their average, but must not be doubled.

{awayTeam} ROSTER (starters first):
{string.Join("\n", awayRosterLines)}

{homeTeam} ROSTER (starters first):
{string.Join("\n", homeRosterLines)}

=== END ROSTER ===";

                const string rosterSystemRule =
                    "You have been given the official current roster for both teams fetched TODAY via OpenAI. " +
                    "These are the ONLY players currently on each roster. " +
                    "ANY player not on this list is no longer on the team. " +
                    "You MUST NOT reference any player not in the FULL LIST — not in prose, not in tables, not anywhere. " +
                    "Base every stat line on the season averages shown — do NOT inflate scores or make role players into stars. " +
                    "Wrap every player name in <strong> tags every time it appears.";

                var prompt = $@"Generate a FRESH, UNIQUE NBA game simulation: {awayTeam} (away) vs {homeTeam} (home).

SIMULATION ID  : {simulationId}
GENERATED AT   : {timestamp}

{rosterBlock}

{injuryInfo}

═══════════════════════════════════════════════════════════
OUTPUT FORMAT — NON-NEGOTIABLE
═══════════════════════════════════════════════════════════
• Return ONLY raw HTML. Zero Markdown (no **, no ##, no ```, no dashes as bullets).
• Use <h2> for sections, <h3> for sub-headings, <p> for prose, <ul>/<li> for lists.
• EVERY player name — without exception — must be wrapped in <strong> tags
  each and every time it appears, e.g. <strong>Jayson Tatum</strong>.
• Do NOT put anything outside the HTML (no preamble, no code fences).

═══════════════════════════════════════════════════════════
ROSTER ENFORCEMENT — NON-NEGOTIABLE
═══════════════════════════════════════════════════════════
• You may reference ONLY players listed in the AUTHORITATIVE ROSTER above.
• If a name is not on the list, DO NOT use it — not in prose, not in tables, not anywhere.
• Injured players (listed above) must appear ONLY in the Injury Report section as OUT.
  They must NEVER appear in game action, stats, or analysis.

SIMULATION REQUIREMENTS:
• Make it UNIQUE (ID {simulationId}) — vary score, key performers, and narrative each time.
• Reflect realistic playing styles for {awayTeam} and {homeTeam}.
• Scores should be believable NBA totals (typically 100-130 per team).
• STAT REALISM IS MANDATORY:
  - Base every player's game line on the season averages in the AUTHORITATIVE ROSTER.
  - A single-game line may vary up to ~30% above or below the season average.
  - Do NOT double a player's average (e.g. a 10 PPG player cannot score 25).
  - Feature the team's actual leading scorers as the primary contributors.
  - Role players should have role-player stat lines.
  - Include ALL key rotation players (high-average scorers MUST appear in Key Performers).

Include these sections in order:
1. <section id=""injury-report""><h2>Injury Report</h2></section> — list all OUT players (omit section if none injured)
2. <h2>Final Score</h2>
3. <h2>Game Summary</h2> — 2-3 sentences with specific players
4. <h2>Key Performers</h2> — HTML table: Player | PTS | REB | AST | Notes (3-5 players per team)
5. <h2>Quarter-by-Quarter Breakdown</h2> — HTML table: Quarter | {awayTeam} | {homeTeam} | Key Moment
6. <h2>Team Statistics</h2> — HTML table: FG%, 3PT%, Rebounds, Turnovers, Fast-Break Points
7. <h2>Betting Analysis</h2> — spread, over/under, moneyline impact

FINAL CHECK BEFORE RESPONDING:
Review every player name in your response. If ANY name does not appear in the AUTHORITATIVE ROSTER above, remove it before responding.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an expert NBA game simulation engine. " +
                        $"ABSOLUTE RULES — violating any of these will produce an invalid simulation: " +
                        $"(1) Output raw HTML only — zero Markdown. " +
                        $"(2) {rosterSystemRule} " +
                        $"(3) NEVER include injured or unavailable players in game action or statistics. " +
                        $"(4) EVERY player name must be inside <strong> tags wherever it appears. " +
                        $"(5) Each simulation must be entirely unique. This is #{simulationId}."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions { Temperature = 0.9f };

                var response = await _chatClient.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var simulationText = StripCodeFences(response.Value.Content[0].Text);

                var invalidPlayers = FindInvalidPlayerNames(simulationText, validPlayers, homeTeam, awayTeam);
                if (invalidPlayers.Count > 0)
                {
                    _logger.LogWarning(
                        "Simulation #{SimulationId} contains {Count} non-roster player(s): {Players}",
                        simulationId, invalidPlayers.Count, string.Join(", ", invalidPlayers));

                    simulationText = SanitizePlayerNames(
                        simulationText, invalidPlayers, homeRoster, awayRoster, injuredPlayersNba);
                }

                _logger.LogInformation("Simulation #{SimulationId} complete for {HomeTeam} vs {AwayTeam}",
                    simulationId, homeTeam, awayTeam);

                return simulationText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "AI simulation FAILED for {HomeTeam} vs {AwayTeam}: {Type}: {Message} — falling back to mock",
                    homeTeam, awayTeam, ex.GetType().FullName, ex.Message);
                return GetMockSimulation(homeTeam, awayTeam, homeRoster, awayRoster, injuredPlayersNba);
            }
        }

        // ── OpenAI Roster Fetch ───────────────────────────────────────────────

        private async Task<NBATeamRoster?> FetchOpenAIRosterAsync(
            string teamName,
            string teamCode,
            CancellationToken cancellationToken)
        {
            if (_chatClient == null) return null;

            try
            {
                _logger.LogInformation("Fetching OpenAI roster for {Team}", teamName);

                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var season = "2025-2026";

                // ── Pass 1: Get the raw roster ────────────────────────────────
                var fetchPrompt = $@"Return the {season} NBA roster for the {teamName} as of {today} (April 2026).

Respond with ONLY a valid JSON object — no markdown, no explanation, no code fences.
Use this exact schema:
{{
  ""players"": [
    {{
      ""name"": ""Full Name"",
      ""position"": ""PG"",
      ""isStarter"": true,
      ""pointsPerGame"": 0.0,
      ""reboundsPerGame"": 0.0,
      ""assistsPerGame"": 0.0
    }}
  ]
}}

Rules:
- Include 12-15 players on the ACTIVE roster as of April 2026.
- Do NOT include players who have RETIRED from the NBA.
- Do NOT include players traded, waived, or released before April 2026.
- List the 5 current starters first (isStarter: true), then bench players (isStarter: false).
- Use real {season} season averages. Use 0.0 if injured all season but still on roster.
- Position must be one of: PG, SG, SF, PF, C.
- Do NOT include two-way contract players.";

                var fetchMessages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an NBA roster database for the {season} season (April 2026). " +
                        $"Return ONLY a valid JSON object. No markdown, no code fences, no explanation."),
                    new UserChatMessage(fetchPrompt)
                };

                var fetchResponse = await _chatClient.CompleteChatAsync(
                    fetchMessages, new ChatCompletionOptions { Temperature = 0.0f }, cancellationToken);
                var rawJson = StripCodeFences(fetchResponse.Value.Content[0].Text);

                using var doc  = JsonDocument.Parse(rawJson);
                var root        = doc.RootElement;

                if (!root.TryGetProperty("players", out var playersEl))
                {
                    _logger.LogWarning("OpenAI roster response for {Team} missing 'players' array", teamName);
                    return null;
                }

                var candidates = new List<NBAPlayer>();
                foreach (var p in playersEl.EnumerateArray())
                {
                    var name      = p.TryGetProperty("name",            out var n)  ? n.GetString()  ?? "" : "";
                    var pos       = p.TryGetProperty("position",        out var ps) ? ps.GetString() ?? "" : "";
                    var isStarter = p.TryGetProperty("isStarter",       out var s)  && s.GetBoolean();
                    var ppg       = p.TryGetProperty("pointsPerGame",   out var pg) ? pg.GetDouble() : 0;
                    var rpg       = p.TryGetProperty("reboundsPerGame", out var rg) ? rg.GetDouble() : 0;
                    var apg       = p.TryGetProperty("assistsPerGame",  out var ag) ? ag.GetDouble() : 0;

                    if (string.IsNullOrWhiteSpace(name)) continue;

                    candidates.Add(new NBAPlayer
                    {
                        Name            = name,
                        Position        = pos,
                        IsStarter       = isStarter,
                        PointsPerGame   = ppg,
                        ReboundsPerGame = rpg,
                        AssistsPerGame  = apg
                    });
                }

                if (candidates.Count == 0)
                {
                    _logger.LogWarning("OpenAI returned zero players for {Team} in pass 1", teamName);
                    return null;
                }

                // ── Pass 2: Validate — strip retired / inactive players ────────
                var retired = await GetRetiredPlayersAsync(
                    candidates.Select(p => p.Name).ToList(), teamName, today, cancellationToken);

                var players = candidates
                    .Where(p => !retired.Contains(p.Name))
                    .ToList();

                if (retired.Count > 0)
                    _logger.LogWarning(
                        "Roster validation removed {Count} inactive/retired player(s) from {Team}: {Names}",
                        retired.Count, teamName, string.Join(", ", retired));

                if (players.Count == 0)
                {
                    _logger.LogWarning("No active players remained for {Team} after validation", teamName);
                    return null;
                }

                _logger.LogInformation(
                    "OpenAI roster: {Count} active players confirmed for {Team}", players.Count, teamName);

                return new NBATeamRoster
                {
                    TeamCode = teamCode.ToUpper(),
                    TeamName = teamName,
                    Players  = players
                };
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to parse OpenAI roster JSON for {Team}", teamName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI roster fetch failed for {Team}", teamName);
                return null;
            }
        }

        /// <summary>
        /// Second-pass validation: asks OpenAI which players in <paramref name="playerNames"/>
        /// are retired, unsigned, or no longer active in the NBA as of April 2026.
        /// Returns the names that should be removed from the roster.
        /// </summary>
        private async Task<HashSet<string>> GetRetiredPlayersAsync(
            List<string> playerNames,
            string teamName,
            string today,
            CancellationToken cancellationToken)
        {
            if (_chatClient == null) return new HashSet<string>();

            try
            {
                var nameList = string.Join(", ", playerNames);

                var validationPrompt =
                    $"The following players were listed on the {teamName} 2025-2026 NBA roster: {nameList}.\n\n" +
                    $"Today is {today} (April 2026).\n\n" +
                    $"Identify ANY player from this list who:\n" +
                    $"- Has retired from the NBA\n" +
                    $"- Is no longer under an active NBA contract\n" +
                    $"- Was traded away from {teamName} before April 2026\n" +
                    $"- Was waived or released before April 2026\n\n" +
                    $"Respond with ONLY a JSON array of the names to REMOVE, e.g. [\"Player One\", \"Player Two\"]. " +
                    $"If all players are valid current {teamName} roster members, respond with an empty array: [].";

                var validationMessages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an NBA roster validator for the 2025-2026 season as of April 2026. " +
                        $"You have authoritative knowledge of which players are retired, traded, waived, or still active. " +
                        $"Respond ONLY with a JSON array of player names to remove. No explanation, no markdown."),
                    new UserChatMessage(validationPrompt)
                };

                var validationResponse = await _chatClient.CompleteChatAsync(
                    validationMessages, new ChatCompletionOptions { Temperature = 0.0f }, cancellationToken);

                var json    = StripCodeFences(validationResponse.Value.Content[0].Text).Trim();
                using var doc = JsonDocument.Parse(json);

                var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = el.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        removed.Add(name);
                }

                return removed;
            }
            catch (Exception ex)
            {
                // Validation is best-effort — a failure here should not block the simulation
                _logger.LogWarning(ex, "Roster validation call failed for {Team} — skipping", teamName);
                return new HashSet<string>();
            }
        }

        // ── MLB ───────────────────────────────────────────────────────────────

        private async Task<string> GenerateMlbSimulationAsync(
            string homeTeam, string awayTeam, string simulationId,
            string timestamp, DateTime? gameTime, CancellationToken cancellationToken,
            string? homeProbablePitcher, string? awayProbablePitcher)
        {
            try
            {
                _logger.LogInformation(
                    "Calling Azure OpenAI for MLB: {AwayTeam} @ {HomeTeam} (SP: {AwayPitcher} vs {HomePitcher})",
                    awayTeam, homeTeam, awayProbablePitcher ?? "TBD", homeProbablePitcher ?? "TBD");

                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

                // ── Fetch and validate rosters concurrently ───────────────────
                _logger.LogInformation("Fetching MLB rosters from OpenAI for {AwayTeam} and {HomeTeam}", awayTeam, homeTeam);

                var mlbRosters = await Task.WhenAll(
                    FetchOpenAIMLBRosterAsync(awayTeam, cancellationToken),
                    FetchOpenAIMLBRosterAsync(homeTeam, cancellationToken));

                var awayRosterPlayers = mlbRosters[0];
                var homeRosterPlayers = mlbRosters[1];

                var rosterDataAvailable = awayRosterPlayers.Count > 0 && homeRosterPlayers.Count > 0;

                if (!rosterDataAvailable)
                {
                    _logger.LogWarning(
                        "MLB simulation aborted for {AwayTeam} @ {HomeTeam} — roster unavailable.",
                        awayTeam, homeTeam);

                    return $@"<section class=""alert alert-warning"">
  <h2>Simulation Unavailable</h2>
  <p>The current roster could not be retrieved for
    <strong>{awayTeam}</strong> and/or <strong>{homeTeam}</strong> at this time.</p>
  <p>Please try again in a few minutes.</p>
</section>";
                }

                var awayRosterBlock = string.Join("\n", awayRosterPlayers.Select(p => $"  • {p}"));
                var homeRosterBlock = string.Join("\n", homeRosterPlayers.Select(p => $"  • {p}"));

                var pitcherSection = "";
                if (!string.IsNullOrEmpty(awayProbablePitcher) || !string.IsNullOrEmpty(homeProbablePitcher))
                {
                    pitcherSection = $@"
SCHEDULED STARTING PITCHERS (from official MLB schedule):
- {awayTeam} starter: {awayProbablePitcher ?? "TBD"}
- {homeTeam} starter: {homeProbablePitcher ?? "TBD"}

You MUST use these exact pitchers. Base stat lines on real 2025-2026 performance (ERA, K rate, WHIP, etc.).
";
                }

                var prompt = $@"Generate a FRESH, UNIQUE MLB game simulation: {awayTeam} (away) @ {homeTeam} (home).

SIMULATION ID : {simulationId}
GENERATED AT  : {timestamp}
SEASON        : 2026 MLB season (April 2026)
{pitcherSection}

═══════════════════════════════════════════════════════════
AUTHORITATIVE ROSTERS — {today}
═══════════════════════════════════════════════════════════
These are the ONLY players you may reference. Do NOT use any player not on this list.

{awayTeam} ACTIVE ROSTER:
{awayRosterBlock}

{homeTeam} ACTIVE ROSTER:
{homeRosterBlock}

═══════════════════════════════════════════════════════════
OUTPUT FORMAT — NON-NEGOTIABLE
═══════════════════════════════════════════════════════════
• Return ONLY raw HTML. Zero Markdown (no **, no ##, no ```, no dashes as bullets).
• Use <h2> sections, <p> prose, <ul>/<li> lists, <table> tables.
• Wrap every player name in <strong> tags each time it appears.
• Do NOT include a preamble or code fences.

ROSTER ENFORCEMENT — NON-NEGOTIABLE:
• Use ONLY players listed in the AUTHORITATIVE ROSTERS provided — no others.
• Do NOT invent players or use anyone not on the list.

SIMULATION REQUIREMENTS:
• VARY the score — sometimes pitching duels, sometimes high-scoring, sometimes walk-offs.
• THE GAME MUST HAVE A WINNER. No ties. Extra innings if needed.
• The home team bats last. If they lead after the top of the 9th, skip the bottom.
• Mention the home ballpark by name and how it affected play.

Include these sections:
1. <h2>Final Score</h2> — include HTML line-score table (inning-by-inning R, H, E)
2. <h2>Game Summary</h2> — 2-3 sentences naming both starters
3. <h2>Starting Pitchers</h2> — HTML table: Pitcher | Team | IP | H | R | ER | BB | K
4. <h2>Key Performers</h2> — HTML table: 4-6 players with batting or pitching lines
5. <h2>Inning-by-Inning Breakdown</h2> — focus on scoring innings and dramatic moments
6. <h2>Team Statistics</h2> — HTML table: Hits, Errors, LOB, Team AVG, Bullpen ERA
7. <h2>Pitching Summary</h2> — all pitchers used; scheduled starter must be first for each team
8. <h2>Betting Analysis</h2> — run line, over/under, moneyline

FINAL CHECK: Review every player name in your response. Remove any name not in the AUTHORITATIVE ROSTERS above.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an expert MLB game simulation engine for the 2026 season (April 2026). " +
                        $"ABSOLUTE RULES: " +
                        $"(1) Output raw HTML only — zero Markdown. " +
                        $"(2) Use ONLY players listed in the AUTHORITATIVE ROSTERS provided — no others. " +
                        $"(3) Use the scheduled starting pitchers provided — do NOT substitute. " +
                        $"(4) Every game must have a winner — no ties. " +
                        $"(5) Wrap every player name in <strong> tags. " +
                        $"(6) Each simulation must be unique. This is #{simulationId}.",
                        $""),
                    new UserChatMessage(prompt)
                };

                var response       = await _chatClient!.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.9f }, cancellationToken);
                var simulationText = StripCodeFences(response.Value.Content[0].Text);

                _logger.LogInformation("MLB simulation #{SimulationId} complete for {AwayTeam} @ {HomeTeam}",
                    simulationId, awayTeam, homeTeam);

                return simulationText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MLB simulation FAILED for {AwayTeam} @ {HomeTeam} — falling back to mock",
                    awayTeam, homeTeam);
                return GetMlbMockSimulation(homeTeam, awayTeam, homeProbablePitcher, awayProbablePitcher);
            }
        }

        /// <summary>
        /// Pass 1: Fetches the current 2026 MLB roster for <paramref name="teamName"/> from OpenAI.
        /// Pass 2: Validates the list, removing any traded, released, or inactive players.
        /// Returns a deduplicated list of active player name strings.
        /// </summary>
        private async Task<List<string>> FetchOpenAIMLBRosterAsync(
            string teamName,
            CancellationToken cancellationToken)
        {
            if (_chatClient == null) return new List<string>();

            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

                // ── Pass 1: Fetch ─────────────────────────────────────────────
                var fetchPrompt =
                    $"List the current 2026 MLB active roster (25-man roster) for the {teamName} as of {today} (April 2026).\n\n" +
                    $"Respond with ONLY a JSON array of player full names, e.g. [\"First Last\", \"First Last\"].\n" +
                    $"Rules:\n" +
                    $"- Include pitchers and position players currently on the 26-man active roster.\n" +
                    $"- Do NOT include players on the IL, traded away, released, or retired.\n" +
                    $"- Do NOT include players who play for a different team in April 2026.\n" +
                    $"- No explanation, no markdown — raw JSON array only.";

                var fetchMessages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an MLB roster database for the 2026 season as of {today}. " +
                        $"Return ONLY a JSON array of active player full names. No markdown, no explanation."),
                    new UserChatMessage(fetchPrompt)
                };

                var fetchResponse = await _chatClient.CompleteChatAsync(
                    fetchMessages, new ChatCompletionOptions { Temperature = 0.0f }, cancellationToken);

                var json = StripCodeFences(fetchResponse.Value.Content[0].Text).Trim();
                using var doc = JsonDocument.Parse(json);

                var candidates = new List<string>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = el.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        candidates.Add(name);
                }

                if (candidates.Count == 0)
                {
                    _logger.LogWarning("OpenAI returned zero MLB players for {Team}", teamName);
                    return new List<string>();
                }

                // ── Pass 2: Validate ──────────────────────────────────────────
                var invalid = await GetInactiveMLBPlayersAsync(candidates, teamName, today, cancellationToken);

                var validated = candidates
                    .Where(p => !invalid.Contains(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (invalid.Count > 0)
                    _logger.LogWarning(
                        "MLB roster validation removed {Count} invalid player(s) from {Team}: {Names}",
                        invalid.Count, teamName, string.Join(", ", invalid));

                _logger.LogInformation("MLB roster: {Count} active players confirmed for {Team}", validated.Count, teamName);
                return validated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MLB roster fetch failed for {Team}", teamName);
                return new List<string>();
            }
        }

        /// <summary>
        /// Pass 2 validation: identifies any players in <paramref name="playerNames"/> who are
        /// traded, released, retired, or otherwise not on <paramref name="teamName"/> in April 2026.
        /// </summary>
        private async Task<HashSet<string>> GetInactiveMLBPlayersAsync(
            List<string> playerNames,
            string teamName,
            string today,
            CancellationToken cancellationToken)
        {
            if (_chatClient == null) return new HashSet<string>();

            try
            {
                var nameList = string.Join(", ", playerNames);

                var validationPrompt =
                    $"The following players were listed on the {teamName} 2026 MLB roster: {nameList}.\n\n" +
                    $"Today is {today} (April 2026).\n\n" +
                    $"Identify ANY player from this list who:\n" +
                    $"- Does NOT play for {teamName} in April 2026\n" +
                    $"- Was traded to another team before April 2026\n" +
                    $"- Was released, waived, or retired\n" +
                    $"- Is currently on the IL and not on the active 26-man roster\n\n" +
                    $"Respond with ONLY a JSON array of names to REMOVE, e.g. [\"Player One\"]. " +
                    $"If all players are valid, respond with an empty array: [].";

                var validationMessages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an MLB roster validator for the 2026 season as of {today}. " +
                        $"You have authoritative knowledge of every player's current team status. " +
                        $"Respond ONLY with a JSON array of names to remove. No markdown, no explanation."),
                    new UserChatMessage(validationPrompt)
                };

                var validationResponse = await _chatClient.CompleteChatAsync(
                    validationMessages, new ChatCompletionOptions { Temperature = 0.0f }, cancellationToken);

                var json = StripCodeFences(validationResponse.Value.Content[0].Text).Trim();
                using var doc = JsonDocument.Parse(json);

                var removed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var name = el.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                        removed.Add(name);
                }

                return removed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MLB roster validation failed for {Team} — skipping", teamName);
                return new HashSet<string>();
            }
        }

        // ── College Football (CFB) ─────────────────────────────────────────────

        private async Task<string> GenerateCfbSimulationAsync(
            string homeTeam, string awayTeam, string simulationId,
            string timestamp, DateTime? gameTime, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Calling Azure OpenAI for CFB: {AwayTeam} @ {HomeTeam}", awayTeam, homeTeam);

                var today  = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var season = "2025 college football season";

                var prompt = $@"Generate a FRESH, UNIQUE college football (NCAA FBS) game simulation: {awayTeam} (away) at {homeTeam} (home).

SIMULATION ID : {simulationId}
GENERATED AT  : {timestamp}
SEASON        : {season} (as of {today})

OUTPUT FORMAT — NON-NEGOTIABLE:
- Return ONLY raw HTML. Zero Markdown.
- Use <h2> for sections, <h3> for sub-headings, <p> for prose, <ul>/<li> for lists, <table> for tables.
- Wrap every player name in <strong> tags each and every time it appears.
- Do NOT put anything outside the HTML (no preamble, no code fences).

ROSTER GUIDANCE:
- Reference realistic, plausible current players for each program (QB, RB, WR, key defenders).
- Use only players who plausibly play for {awayTeam} or {homeTeam} in the {season}.
- Do NOT invent absurd names or reference players from other programs.

SIMULATION REQUIREMENTS:
- This is FOOTBALL, not basketball. Scores MUST be realistic college football totals
  (typically 10-45 points per team; blowouts can reach the 50s-60s).
- THE GAME MUST HAVE A WINNER. No ties — use overtime if the score is level after regulation.
- Reflect each team's realistic playing style, conference, and home-field environment
  (name the home stadium and how the crowd/venue affected play).
- Football stat lines only — passing yards, rushing yards, receiving yards, touchdowns,
  tackles, sacks, interceptions. Do NOT use basketball stats (points, rebounds, assists).

Include these sections in order:
1. <h2>Final Score</h2> — include an HTML quarter-by-quarter line score table (Q1-Q4 + Final)
2. <h2>Game Summary</h2> — 2-3 sentences naming key players
3. <h2>Key Performers</h2> — HTML table: Player | Team | Pos | Statline (QB/RB/WR/defense)
4. <h2>Scoring Summary</h2> — HTML table: Quarter | Team | Play | Score
5. <h2>Team Statistics</h2> — HTML table: Total Yards, Passing, Rushing, Turnovers, Time of Possession, 3rd-Down %
6. <h2>Betting Analysis</h2> — spread, over/under, moneyline impact

FINAL CHECK BEFORE RESPONDING:
Confirm every stat is a FOOTBALL stat and the final score is a realistic football score.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(
                        $"You are an expert college football (NCAA FBS) game simulation engine for the {season}. " +
                        $"ABSOLUTE RULES — violating any of these produces an invalid simulation: " +
                        $"(1) Output raw HTML only — zero Markdown. " +
                        $"(2) This is FOOTBALL — use only football stats (yards, TDs, tackles, sacks, INTs). " +
                        $"Never use basketball stats (points, rebounds, assists) and never produce basketball-style scores. " +
                        $"(3) Final scores must be realistic college football totals and the game must have a winner. " +
                        $"(4) Wrap every player name in <strong> tags wherever it appears. " +
                        $"(5) Each simulation must be entirely unique. This is #{simulationId}."),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient!.CompleteChatAsync(
                    messages, new ChatCompletionOptions { Temperature = 0.9f }, cancellationToken);
                var simulationText = StripCodeFences(response.Value.Content[0].Text);

                _logger.LogInformation("CFB simulation #{SimulationId} complete for {AwayTeam} @ {HomeTeam}",
                    simulationId, awayTeam, homeTeam);

                return simulationText;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CFB simulation FAILED for {AwayTeam} @ {HomeTeam} — falling back to mock",
                    awayTeam, homeTeam);
                return GetCfbMockSimulation(homeTeam, awayTeam);
            }
        }

        private static string GetCfbMockSimulation(string homeTeam, string awayTeam)
        {
            var seed = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var rng  = new Random(seed);

            int[] awayQ = { rng.Next(0, 15), rng.Next(0, 15), rng.Next(0, 15), rng.Next(0, 15) };
            int[] homeQ = { rng.Next(0, 15), rng.Next(0, 15), rng.Next(0, 15), rng.Next(0, 15) };
            int awayScore = awayQ.Sum();
            int homeScore = homeQ.Sum();
            while (awayScore == homeScore) { homeQ[3] += rng.Next(3, 8); homeScore = homeQ.Sum(); }

            bool awayWins = awayScore > homeScore;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser  = awayWins ? homeTeam : awayTeam;
            int margin    = Math.Abs(awayScore - homeScore);

            int awayPassYds = rng.Next(150, 380), awayRushYds = rng.Next(80, 260);
            int homePassYds = rng.Next(150, 380), homeRushYds = rng.Next(80, 260);

            return $@"<h1>GAME SIMULATION: {awayTeam} @ {homeTeam}</h1>

<h2>Final Score</h2>
<table>
  <thead><tr><th>Team</th><th>Q1</th><th>Q2</th><th>Q3</th><th>Q4</th><th>Final</th></tr></thead>
  <tbody>
    <tr><td><strong>{awayTeam}</strong></td><td>{awayQ[0]}</td><td>{awayQ[1]}</td><td>{awayQ[2]}</td><td>{awayQ[3]}</td><td><strong>{awayScore}</strong></td></tr>
    <tr><td><strong>{homeTeam}</strong></td><td>{homeQ[0]}</td><td>{homeQ[1]}</td><td>{homeQ[2]}</td><td>{homeQ[3]}</td><td><strong>{homeScore}</strong></td></tr>
  </tbody>
</table>

<h2>Game Summary</h2>
<p>In a {(margin <= 7 ? "tightly contested" : "decisive")} matchup, <strong>{winner}</strong> {(margin <= 7 ? "edges out" : "controls")} <strong>{loser}</strong> {Math.Max(awayScore, homeScore)}-{Math.Min(awayScore, homeScore)}. Both offenses moved the ball, but {winner} made the difference late.</p>

<h2>Team Statistics</h2>
<table>
  <thead><tr><th>Statistic</th><th>{awayTeam}</th><th>{homeTeam}</th></tr></thead>
  <tbody>
    <tr><td>Total Yards</td><td>{awayPassYds + awayRushYds}</td><td>{homePassYds + homeRushYds}</td></tr>
    <tr><td>Passing Yards</td><td>{awayPassYds}</td><td>{homePassYds}</td></tr>
    <tr><td>Rushing Yards</td><td>{awayRushYds}</td><td>{homeRushYds}</td></tr>
    <tr><td>Turnovers</td><td>{rng.Next(0, 4)}</td><td>{rng.Next(0, 4)}</td></tr>
    <tr><td>Time of Possession</td><td>{rng.Next(26, 34)}:00</td><td>{rng.Next(26, 34)}:00</td></tr>
  </tbody>
</table>

<h2>Betting Analysis</h2>
<ul>
  <li><strong>Spread:</strong> {winner} wins by {margin}</li>
  <li><strong>Over/Under:</strong> Combined {awayScore + homeScore} points</li>
  <li><strong>Moneyline:</strong> {winner} wins outright</li>
</ul>
<p><em>Simulated game for entertainment purposes only.</em></p>";
        }

        // ── Helpers ───────────────────────────────────────────────────────────

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
            NBATeamRoster? homeRoster, NBATeamRoster? awayRoster, HashSet<string> injuredPlayers)
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
            string html,
            HashSet<string> validPlayers,
            string homeTeam,
            string awayTeam)
        {
            var teamWords = new HashSet<string>(
                (homeTeam + " " + awayTeam)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            var invalid = new List<string>();

            foreach (Match m in StrongNamePattern.Matches(html))
            {
                var name = m.Groups[1].Value.Trim();
                if (name.Split(' ').Any(w => teamWords.Contains(w))) continue;
                if (!validPlayers.Contains(name))
                    invalid.Add(name);
            }

            return invalid.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string SanitizePlayerNames(
            string html,
            List<string> invalidPlayers,
            NBATeamRoster? homeRoster,
            NBATeamRoster? awayRoster,
            HashSet<string> injuredPlayers)
        {
            var pool = new List<string>();
            foreach (var roster in new[] { awayRoster, homeRoster })
            {
                if (roster == null) continue;
                pool.AddRange(roster.Players
                    .Where(p => !p.IsStarter && !injuredPlayers.Contains(p.Name))
                    .Select(p => p.Name));
            }
            foreach (var roster in new[] { awayRoster, homeRoster })
            {
                if (roster == null) continue;
                pool.AddRange(roster.Players
                    .Where(p => p.IsStarter && !injuredPlayers.Contains(p.Name))
                    .Select(p => p.Name));
            }
            pool = pool.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var idx = 0;
            var madeChanges = false;

            foreach (var invalid in invalidPlayers)
            {
                if (idx >= pool.Count) break;

                var find = $"<strong>{invalid}</strong>";
                var replace = $"<strong>{pool[idx % pool.Count]}</strong>";

                if (html.Contains(find, StringComparison.OrdinalIgnoreCase))
                {
                    html = html.Replace(find, replace, StringComparison.OrdinalIgnoreCase);
                    madeChanges = true;
                    idx++;
                }
            }

            if (madeChanges)
                html += "\n<p><em>Note: One or more player names were corrected to match the current team roster.</em></p>";

            return html;
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

            bool awayWins = awayRuns > homeRuns;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser = awayWins ? homeTeam : awayTeam;
            int winScore = Math.Max(awayRuns, homeRuns);
            int loseScore = Math.Min(awayRuns, homeRuns);
            int margin = winScore - loseScore;

            int[] awayInnings = DistributeRuns(rng, awayRuns, 9);
            int[] homeInnings = DistributeRuns(rng, homeRuns, 9);
            int awayHits = awayRuns + rng.Next(2, 6);
            int homeHits = homeRuns + rng.Next(2, 6);
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
<p>In a {(margin <= 2 ? "tightly contested" : "decisive")} matchup, <strong>{winner}</strong> {(margin <= 2 ? "edges out a win" : "cruises to victory")} {winScore}-{loseScore} over <strong>{loser}</strong>. <strong>{awaySP}</strong> took the mound for {awayTeam} opposite <strong>{homeSP}</strong> for {homeTeam}.</p>

<h2>Starting Pitchers</h2>
<table>
  <thead><tr><th>Pitcher</th><th>Team</th><th>IP</th><th>H</th><th>R</th><th>ER</th><th>BB</th><th>K</th></tr></thead>
  <tbody>
    <tr><td><strong>{awaySP}</strong></td><td>{awayTeam}</td><td>{rng.Next(5, 8)}.0</td><td>{rng.Next(3, 8)}</td><td>{rng.Next(1, 5)}</td><td>{rng.Next(1, 4)}</td><td>{rng.Next(0, 4)}</td><td>{rng.Next(3, 9)}</td></tr>
    <tr><td><strong>{homeSP}</strong></td><td>{homeTeam}</td><td>{rng.Next(5, 8)}.0</td><td>{rng.Next(3, 8)}</td><td>{rng.Next(1, 5)}</td><td>{rng.Next(1, 4)}</td><td>{rng.Next(0, 4)}</td><td>{rng.Next(3, 9)}</td></tr>
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
<p><em>Simulated game for entertainment purposes only.</em></p>";
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

            var seed = (homeTeam + awayTeam + DateTime.UtcNow.Ticks).GetHashCode();
            var rng = new Random(seed);

            int awayScore = rng.Next(95, 125);
            int homeScore = rng.Next(95, 125);
            if (Math.Abs(awayScore - homeScore) > 15)
            {
                if (awayScore > homeScore) awayScore = homeScore + rng.Next(1, 12);
                else homeScore = awayScore + rng.Next(1, 12);
            }

            bool awayWins = awayScore > homeScore;
            string winner = awayWins ? awayTeam : homeTeam;
            string loser = awayWins ? homeTeam : awayTeam;
            int margin = Math.Abs(awayScore - homeScore);

            int p1pts = rng.Next(25, 38), p1reb = rng.Next(5, 13), p1ast = rng.Next(4, 11);
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
                if (awayInj.Any() || homeInj.Any())
                {
                    injurySection = "<section id=\"injury-report\"><h2>Injury Report</h2><ul>";
                    foreach (var p in awayInj) injurySection += $"<li><strong>{p}</strong> ({awayTeam}) — OUT</li>";
                    foreach (var p in homeInj) injurySection += $"<li><strong>{p}</strong> ({homeTeam}) — OUT</li>";
                    injurySection += "</ul></section>";
                }
            }

            return $@"{injurySection}
<h1>GAME SIMULATION: {awayTeam} @ {homeTeam}</h1>

<h2>Final Score</h2>
<p><strong>{awayTeam}:</strong> {awayScore} &nbsp; <strong>{homeTeam}:</strong> {homeScore}</p>

<h2>Game Summary</h2>
<p>In an exciting {(margin < 5 ? "nail-biter" : "hard-fought battle")}, <strong>{winner}</strong> {(margin < 5 ? "narrowly defeats" : "edges")} <strong>{loser}</strong> {awayScore}-{homeScore}. <strong>{ap1}</strong> drove {awayTeam} with {p1pts} points while <strong>{hp1}</strong> led {homeTeam} with {h1pts} points and {h1ast} assists.</p>

<h2>Key Performers</h2>
<h3>{awayTeam}</h3>
<table>
  <thead><tr><th>Player</th><th>PTS</th><th>REB</th><th>AST</th><th>Notes</th></tr></thead>
  <tbody>
    <tr><td><strong>{ap1}</strong></td><td>{p1pts}</td><td>{p1reb}</td><td>{p1ast}</td><td>Dominant on both ends</td></tr>
    <tr><td><strong>{ap2}</strong></td><td>{p2pts}</td><td>{p2reb}</td><td>3</td><td>Timely buckets and defense</td></tr>
    <tr><td><strong>{ap3}</strong></td><td>{p3pts}</td><td>{p3reb}</td><td>2</td><td>Controlled the paint</td></tr>
  </tbody>
</table>
<h3>{homeTeam}</h3>
<table>
  <thead><tr><th>Player</th><th>PTS</th><th>REB</th><th>AST</th><th>Notes</th></tr></thead>
  <tbody>
    <tr><td><strong>{hp1}</strong></td><td>{h1pts}</td><td>5</td><td>{h1ast}</td><td>Orchestrated the offense</td></tr>
    <tr><td><strong>{hp2}</strong></td><td>{h2pts}</td><td>7</td><td>4</td><td>Clutch shooting</td></tr>
    <tr><td><strong>{hp3}</strong></td><td>{rng.Next(15, 22)}</td><td>4</td><td>3</td><td>Steady two-way play</td></tr>
  </tbody>
</table>

<h2>Quarter-by-Quarter Breakdown</h2>
<table>
  <thead><tr><th>Quarter</th><th>{awayTeam}</th><th>{homeTeam}</th><th>Key Moment</th></tr></thead>
  <tbody>
    <tr><td>1st</td><td>{q1a}</td><td>{q1h}</td><td><strong>{ap1}</strong> asserts early; <strong>{hp1}</strong> responds</td></tr>
    <tr><td>Half</td><td>{hfa}</td><td>{hfh}</td><td><strong>{hp2}</strong> heats up; <strong>{ap2}</strong> answers</td></tr>
    <tr><td>3rd</td><td>{q3a}</td><td>{q3h}</td><td><strong>{ap3}</strong> dominates inside</td></tr>
    <tr><td>Final</td><td>{awayScore}</td><td>{homeScore}</td><td>{(awayWins ? $"<strong>{ap1}</strong> seals it in crunch time" : $"<strong>{hp1}</strong> leads the comeback")}</td></tr>
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
  <li><strong>Spread:</strong> {winner} wins by {margin} — {(margin > 5 ? "likely covers" : "near the line")}</li>
  <li><strong>Over/Under:</strong> {awayScore + homeScore} total points</li>
  <li><strong>Moneyline:</strong> {winner} wins outright</li>
</ul>
<p><em>Simulated game for entertainment purposes only.</em></p>";
        }
    }
}
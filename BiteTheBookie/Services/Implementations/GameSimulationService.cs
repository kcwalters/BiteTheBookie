using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using OpenAI.Chat;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BiteTheBookie.Services.Implementations
{
    public class GameSimulationService : IGameSimulationService
    {
        private readonly ChatClient? _chatClient;
        private readonly ILogger<GameSimulationService> _logger;
        private readonly IHttpClientFactory _httpFactory;

        private static readonly Regex StrongNamePattern = new(
            @"<strong>([A-Z][a-zA-Z''-]+(?: [A-Z][a-zA-Z''-]+){1,2})</strong>",
            RegexOptions.Compiled);

        public GameSimulationService(ChatClient? chatClient, ILogger<GameSimulationService> logger, IHttpClientFactory httpFactory, IConfiguration configuration)
        {
            _logger = logger;
            _chatClient = chatClient;
            _httpFactory = httpFactory;

            if (_chatClient == null)
                _logger.LogWarning("Azure OpenAI ChatClient is not configured. Simulations may fail.");
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
            var simulationId = Guid.NewGuid().ToString("N")[..8];
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInformation(
                "Starting simulation #{SimulationId}: {AwayTeam} @ {HomeTeam} ({League})",
                simulationId, awayTeam, homeTeam, league);
            
            if (_chatClient == null)
            {
                _logger.LogError("Simulation aborted: ChatClient is NULL.");
                return $@"<section class=""alert alert-warning"">
  <h2>Simulation Unavailable</h2>
  <p>Simulation is temporarily unavailable due to ChatClient misconfiguration.</p>
</section>";
            }

            try
            {
                // Fetch rosters from Azure OpenAI — league-specific so MLB gets a
                // baseball roster and NBA gets a basketball roster.
                _logger.LogInformation("Fetching rosters from Azure OpenAI for {AwayTeam} and {HomeTeam} ({League})", awayTeam, homeTeam, league);

                if (league.Equals("MLB", StringComparison.OrdinalIgnoreCase))
                {
                    var mlbRosters = await Task.WhenAll(
                        FetchOpenAIMLBRosterAsync(awayTeam, cancellationToken),
                        FetchOpenAIMLBRosterAsync(homeTeam, cancellationToken));

                    var awayPlayers = mlbRosters[0];
                    var homePlayers = mlbRosters[1];

                    if (awayPlayers.Count == 0 || homePlayers.Count == 0)
                    {
                        _logger.LogError("Incomplete MLB rosters for {AwayTeam} / {HomeTeam}, simulation aborted.", awayTeam, homeTeam);
                        return $@"<section class=""alert alert-warning"">
  <h2>Simulation Unavailable</h2>
  <p>The current roster could not be retrieved for <strong>{awayTeam}</strong> and/or <strong>{homeTeam}</strong>. Please try again later.</p>
</section>";
                    }

                    return await GenerateMlbSimulationAsync(
                        homeTeam, awayTeam, simulationId, timestamp,
                        awayPlayers, homePlayers,
                        homeProbablePitcher, awayProbablePitcher, cancellationToken);
                }

                // ?? NBA (and other basketball-style leagues) ??????????????????????
                var rosterTasks = await Task.WhenAll(
                    FetchOpenAIRosterAsync(awayTeam, awayRoster?.TeamCode ?? awayTeam, cancellationToken),
                    FetchOpenAIRosterAsync(homeTeam, homeRoster?.TeamCode ?? homeTeam, cancellationToken));
                awayRoster = rosterTasks[0];
                homeRoster = rosterTasks[1];

                if (awayRoster == null || awayRoster.Players.Count == 0 || homeRoster == null || homeRoster.Players.Count == 0)
                {
                    _logger.LogError("Incomplete rosters fetched for {AwayTeam} and {HomeTeam}, simulation aborted.", awayTeam, homeTeam);
                    return $@"<section class=""alert alert-warning"">
  <h2>Simulation Unavailable</h2>
  <p>Unable to retrieve full rosters for <strong>{awayTeam}</strong> and/or <strong>{homeTeam}</strong>. Please try again later.</p>
</section>";
                }

                // Prepare the game simulation prompt
                var prompt = PrepareSimulationPrompt(homeTeam, awayTeam, league, homeRoster, awayRoster, injuries);
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage($"You are an expert sports simulation engine for {league}. Generate a game simulation based on the rosters provided."),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.8f }, cancellationToken);
                var simulationContent = CleanHtmlResponse(response.Value.Content[0].Text);

                _logger.LogInformation("Simulation #{SimulationId} completed successfully for {HomeTeam} vs {AwayTeam}.", simulationId, homeTeam, awayTeam);
                return simulationContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Simulation failed for {AwayTeam} vs {HomeTeam}", awayTeam, homeTeam);
                return $@"<section class=""alert alert-danger"">
  <h2>Simulation Failed</h2>
  <p>An error occurred during the simulation. Please try again later.</p>
</section>";
            }
        }

        private async Task<NBATeamRoster?> FetchOpenAIRosterAsync(
            string teamName,
            string teamCode,
            CancellationToken cancellationToken)
        {
            if (_chatClient == null) return null;

            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var prompt = $@"Return the official {today} NBA roster for the {teamName}.
Respond with ONLY a JSON object in this format:
{{ ""players"": [{{ ""name"": ""Full Name"", ""position"": ""PG"", ""isStarter"": true, ""pointsPerGame"": 0.0, ""reboundsPerGame"": 0.0, ""assistsPerGame"": 0.0 }} ] }}";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an expert NBA roster retrieval engine."),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient.CompleteChatAsync(
                    messages, new ChatCompletionOptions { Temperature = 0.0f }, cancellationToken);
                
                var json = StripCodeFences(response.Value.Content[0].Text);
                using var doc = JsonDocument.Parse(json);

                // Accept either { "players": [...] } or a bare [...] array.
                JsonElement playersArray;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    playersArray = doc.RootElement;
                }
                else if (!doc.RootElement.TryGetProperty("players", out playersArray)
                         || playersArray.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("OpenAI roster response for {Team} missing 'players' array. Raw: {Raw}", teamName, json);
                    return null;
                }

                var players = new List<NBAPlayer>();
                foreach (var player in playersArray.EnumerateArray())
                {
                    var name = player.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    players.Add(new NBAPlayer
                    {
                        Name            = name,
                        Position        = player.TryGetProperty("position", out var ps) ? ps.GetString() ?? string.Empty : string.Empty,
                        IsStarter       = player.TryGetProperty("isStarter", out var s) && s.GetBoolean(),
                        PointsPerGame   = player.TryGetProperty("pointsPerGame", out var pg) ? pg.GetDouble() : 0,
                        ReboundsPerGame = player.TryGetProperty("reboundsPerGame", out var rg) ? rg.GetDouble() : 0,
                        AssistsPerGame  = player.TryGetProperty("assistsPerGame", out var ag) ? ag.GetDouble() : 0
                    });
                }

                return new NBATeamRoster
                {
                    TeamCode = teamCode,
                    TeamName = teamName,
                    Players = players
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI call failed while fetching roster for {Team}", teamName);
                return null;
            }
        }

        private async Task<List<string>> FetchOpenAIMLBRosterAsync(string teamName, CancellationToken cancellationToken)
        {
            if (_chatClient == null) return new List<string>();

            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                var prompt =
                    $"List the current 2026 MLB active roster for the {teamName} as of {today}.\n" +
                    "Respond with ONLY a JSON array of player full names, e.g. [\"First Last\", \"First Last\"].\n" +
                    "Include pitchers and position players on the active roster. " +
                    "Do NOT include traded, released, or retired players. No markdown, raw JSON only.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage($"You are an MLB roster database for the 2026 season as of {today}. Return ONLY a JSON array of active player full names."),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.0f }, cancellationToken);
                var json = StripCodeFences(response.Value.Content[0].Text).Trim();

                using var doc = JsonDocument.Parse(json);

                // Accept a bare array or an object wrapping a "players" array.
                JsonElement arr;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    arr = doc.RootElement;
                else if (!doc.RootElement.TryGetProperty("players", out arr) || arr.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("MLB roster response for {Team} was not a JSON array. Raw: {Raw}", teamName, json);
                    return new List<string>();
                }

                var players = new List<string>();
                foreach (var el in arr.EnumerateArray())
                {
                    var name = el.ValueKind == JsonValueKind.String
                        ? el.GetString()
                        : (el.TryGetProperty("name", out var n) ? n.GetString() : null);
                    if (!string.IsNullOrWhiteSpace(name)) players.Add(name!);
                }

                _logger.LogInformation("Azure OpenAI MLB roster: {Count} players for {Team}", players.Count, teamName);
                return players.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI MLB roster fetch failed for {Team}", teamName);
                return new List<string>();
            }
        }

        private async Task<string> GenerateMlbSimulationAsync(
            string homeTeam, string awayTeam, string simulationId, string timestamp,
            List<string> awayRoster, List<string> homeRoster,
            string? homeProbablePitcher, string? awayProbablePitcher,
            CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var awayBlock = string.Join("\n", awayRoster.Select(p => $"  • {p}"));
            var homeBlock = string.Join("\n", homeRoster.Select(p => $"  • {p}"));

            var pitcherSection = "";
            if (!string.IsNullOrEmpty(awayProbablePitcher) || !string.IsNullOrEmpty(homeProbablePitcher))
            {
                pitcherSection = $@"
SCHEDULED STARTING PITCHERS:
- {awayTeam} starter: {awayProbablePitcher ?? "TBD"}
- {homeTeam} starter: {homeProbablePitcher ?? "TBD"}
Use these exact pitchers.";
            }

            var prompt = $@"Generate a FRESH, UNIQUE MLB game simulation: {awayTeam} (away) @ {homeTeam} (home).

SIMULATION ID : {simulationId}
GENERATED AT  : {timestamp}
SEASON        : 2026 MLB season
{pitcherSection}

AUTHORITATIVE ROSTERS as of {today} — use ONLY these players:
{awayTeam}:
{awayBlock}

{homeTeam}:
{homeBlock}

OUTPUT FORMAT — NON-NEGOTIABLE:
- Return ONLY raw HTML. Zero Markdown.
- Use <h2> sections, <p> prose, <table> tables. Wrap every player name in <strong> tags.
- This is BASEBALL — use baseball stats only (IP, H, R, ER, BB, K, AVG, HR, RBI). Never basketball stats.
- THE GAME CAN NEVER END IN A TIE. Baseball has no ties. If the score is level
  after 9 innings, continue playing extra innings until one team leads at the end
  of a complete inning. The home team bats last; if they lead after the top of the
  9th (or any later inning), they do not bat in the bottom half.
- The final score MUST show one team with strictly more runs than the other.

Include: <h2>Final Score</h2> (inning-by-inning line score), <h2>Game Summary</h2>,
<h2>Starting Pitchers</h2>, <h2>Key Performers</h2>, <h2>Inning-by-Inning Breakdown</h2>,
<h2>Team Statistics</h2>, <h2>Betting Analysis</h2> (run line, over/under, moneyline).

FINAL CHECK: (1) remove any player name not in the rosters above. (2) Confirm the two
teams have DIFFERENT run totals — if they are equal, add extra innings until they differ.";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(
                    "You are an expert MLB game simulation engine for the 2026 season. " +
                    "Output raw HTML only. Use ONLY the provided rosters. Use baseball stats only. " +
                    "A baseball game can NEVER end in a tie — if tied after 9 innings, play extra " +
                    "innings until one team wins. The two final run totals must differ. " +
                    "Wrap every player name in <strong> tags."),
                new UserChatMessage(prompt)
            };

            var response = await _chatClient!.CompleteChatAsync(messages, new ChatCompletionOptions { Temperature = 0.9f }, cancellationToken);
            var simulationText = StripCodeFences(response.Value.Content[0].Text);

            _logger.LogInformation("MLB simulation #{SimulationId} complete for {AwayTeam} @ {HomeTeam}", simulationId, awayTeam, homeTeam);
            return simulationText;
        }

        private static string PrepareSimulationPrompt(
            string homeTeam,
            string awayTeam,
            string league,
            NBATeamRoster? homeRoster,
            NBATeamRoster? awayRoster,
            List<PlayerInjuryReport>? injuries)
        {
            return $@"Generate a full {league} game simulation for {awayTeam} (away) vs {homeTeam} (home).
Include starting lineups, key plays, final scores, and betting insights.

Details:
- Away Team: {awayTeam} (Roster: {awayRoster?.Players.Count} players)
- Home Team: {homeTeam} (Roster: {homeRoster?.Players.Count} players)
- Injuries: {string.Join(", ", injuries?.Select(i => $"{i.PlayerName} ({i.InjuryStatus})") ?? Array.Empty<string>())}.

Provide the simulation as a fully valid HTML document with sections including:
- <h2>Game Summary</h2>: Brief description of the game outcome.
- <h2>Final Score</h2>: The final score for both teams.
- <h2>Player Statistics</h2>: Key players' performance with highlights (PTS/REB/AST).";
        }

        private static string CleanHtmlResponse(string content)
        {
            content = content.Trim();
            if (content.StartsWith("```"))
                content = content.Substring(3).Trim();
            if (content.EndsWith("```"))
                content = content.Substring(0, content.Length - 3).Trim();
            return content;
        }

        /// <summary>
        /// Cleans a JSON string response by removing any leading/trailing code block markers or extra whitespace.
        /// </summary>
        private static string CleanResponseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "{}";
            json = json.Trim();
            if (json.StartsWith("```json"))
                json = json.Substring(7).Trim();
            else if (json.StartsWith("```"))
                json = json.Substring(3).Trim();
            if (json.EndsWith("```"))
                json = json.Substring(0, json.Length - 3).Trim();
            return json;
        }

        private static string StripCodeFences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim();

            // Remove an opening fence along with any language identifier that
            // follows it on the same line (e.g. ```html, ```json, ```).
            if (text.StartsWith("```"))
            {
                var newlineIndex = text.IndexOf('\n');
                text = newlineIndex >= 0
                    ? text[(newlineIndex + 1)..]
                    : text[3..];
                text = text.Trim();
            }

            if (text.EndsWith("```"))
                text = text[..^3].Trim();

            return text;
        }
    }
}

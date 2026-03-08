using Azure;
using Azure.AI.OpenAI;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using OpenAI.Chat;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class NBAGamesService : INBAGamesService
    {
        private readonly ILogger<NBAGamesService> _logger;
        private readonly ChatClient? _chatClient;
        private readonly TheOddsApiClient _oddsApiClient;
        private readonly INBAScoresService _scoresService; // ADD THIS
        private readonly Dictionary<string, (string Name, string Logo, string Code)> _teamInfo;

        public NBAGamesService(
            IConfiguration configuration, 
            ILogger<NBAGamesService> logger,
            TheOddsApiClient oddsApiClient,
            INBAScoresService scoresService) // ADD THIS PARAMETER
        {
            _logger = logger;
            _oddsApiClient = oddsApiClient;
            _scoresService = scoresService; // ADD THIS
            
            // Initialize team information
            _teamInfo = InitializeTeamInfo();

            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var apiKey = configuration["AzureOpenAI:ApiKey"];
            var deploymentName = configuration["AzureOpenAI:DeploymentName"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Azure OpenAI configuration is missing. Will use fallback game data if The Odds API fails.");
                _chatClient = null;
            }
            else
            {
                var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                _chatClient = azureClient.GetChatClient(deploymentName);
            }
        }

        public async Task<List<NBAGameMatchup>> GetUpcomingNBAGamesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching NBA games from The Odds API and ESPN Scores");
            
            // Fetch odds from The Odds API
            var oddsData = await _oddsApiClient.GetAsync("/v4/sports/basketball_nba/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american", cancellationToken);
            var games = ParseNBAOddsApiResponse(oddsData);
            
            if (!games.Any())
            {
                _logger.LogWarning("No games available from The Odds API");
                return new List<NBAGameMatchup>();
            }
            
            // Fetch live scores from ESPN
            try
            {
                var liveScores = await _scoresService.GetGamesAsync(cancellationToken);
                _logger.LogInformation("Fetched {Count} live scores from ESPN", liveScores.Count);
                
                // Merge live scores with odds data
                MergeScoresWithGames(games, liveScores);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch live scores from ESPN, continuing with odds data only");
            }
            
            // Filter to include TODAY and TOMORROW games (all games, no exceptions)
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var todayAndTomorrowGames = games
                .Where(g => g.GameTime.Date == today || g.GameTime.Date == tomorrow)
                .OrderBy(g => g.GameTime)
                .ToList();
            
            _logger.LogInformation("Returning {Count} games for today and tomorrow ({Today} - {Tomorrow})", 
                todayAndTomorrowGames.Count, today.ToString("yyyy-MM-dd"), tomorrow.ToString("yyyy-MM-dd"));
            
            return todayAndTomorrowGames; // FIXED: Changed from todaysGames
        }

       
        private List<NBAGameMatchup> ParseNBAOddsApiResponse(JsonElement oddsData)
        {
            var games = new List<NBAGameMatchup>();

            if (oddsData.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected response format from The Odds API");
                return games;
            }

            var totalGames = oddsData.GetArrayLength();
            _logger.LogInformation("Processing {TotalGames} games from The Odds API", totalGames);
            
            var skippedGames = 0;
            var unmappedTeams = new HashSet<string>();

            foreach (var game in oddsData.EnumerateArray())
            {
                try
                {
                    var homeTeam = game.GetProperty("home_team").GetString() ?? "";
                    var awayTeam = game.GetProperty("away_team").GetString() ?? "";
                    var commenceTime = game.GetProperty("commence_time").GetDateTime();

                    // Ensure DateTime is properly marked as UTC for correct timezone conversion
                    if (commenceTime.Kind == DateTimeKind.Unspecified)
                    {
                        commenceTime = DateTime.SpecifyKind(commenceTime, DateTimeKind.Utc);
                    }

                    // Map team names to our codes
                    var homeTeamCode = MapTeamNameToCode(homeTeam);
                    var awayTeamCode = MapTeamNameToCode(awayTeam);

                    if (string.IsNullOrEmpty(homeTeamCode) || string.IsNullOrEmpty(awayTeamCode))
                    {
                        _logger.LogWarning("Could not map teams: {Home} ({HomeCode}) / {Away} ({AwayCode})", 
                            homeTeam, homeTeamCode, awayTeam, awayTeamCode);
                        
                        if (string.IsNullOrEmpty(homeTeamCode)) unmappedTeams.Add(homeTeam);
                        if (string.IsNullOrEmpty(awayTeamCode)) unmappedTeams.Add(awayTeam);
                        
                        skippedGames++;
                        continue;
                    }

                    _logger.LogDebug("Mapped teams: {Away} → {AwayCode} @ {Home} → {HomeCode}", 
                        awayTeam, awayTeamCode, homeTeam, homeTeamCode);

                    var homeInfo = _teamInfo.GetValueOrDefault(homeTeamCode);
                    var awayInfo = _teamInfo.GetValueOrDefault(awayTeamCode);

                    if (homeInfo == default || awayInfo == default)
                    {
                        _logger.LogWarning("Team code lookup failed: {HomeCode} or {AwayCode} not found in team info dictionary", 
                            homeTeamCode, awayTeamCode);
                        skippedGames++;
                        continue;
                    }

                    // Extract betting lines from bookmakers
                    decimal? spread = null;
                    decimal? overUnder = null;
                    int? homeMoneyline = null;
                    int? awayMoneyline = null;

                    if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var bookmaker in bookmakers.EnumerateArray())
                        {
                            if (bookmaker.TryGetProperty("markets", out var markets) && markets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var market in markets.EnumerateArray())
                                {
                                    var marketKey = market.GetProperty("key").GetString();

                                    if (marketKey == "spreads" && !spread.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined)
                                        {
                                            spread = homeOutcome.GetProperty("point").GetDecimal();
                                        }
                                    }
                                    else if (marketKey == "totals" && !overUnder.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        if (outcomes.Any())
                                        {
                                            overUnder = outcomes.First().GetProperty("point").GetDecimal();
                                        }
                                    }
                                    else if (marketKey == "h2h")
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        var awayOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == awayTeam);
                                        
                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined && !homeMoneyline.HasValue)
                                        {
                                            homeMoneyline = homeOutcome.GetProperty("price").GetInt32();
                                        }
                                        if (awayOutcome.ValueKind != JsonValueKind.Undefined && !awayMoneyline.HasValue)
                                        {
                                            awayMoneyline = awayOutcome.GetProperty("price").GetInt32();
                                        }
                                    }
                                }
                            }

                            // Break after first bookmaker with data
                            if (spread.HasValue && overUnder.HasValue)
                            {
                                break;
                            }
                        }
                    }

                    _logger.LogDebug("Successfully parsed game: {AwayCode} @ {HomeCode} at {GameTime} (Spread: {Spread}, O/U: {OverUnder})",
                        awayTeamCode, homeTeamCode, commenceTime, spread, overUnder);

                    games.Add(new NBAGameMatchup
                    {
                        GameId = $"{awayTeamCode.ToLower()}-{homeTeamCode.ToLower()}-{commenceTime:yyyyMMdd}",
                        AwayTeamCode = awayInfo.Code,
                        AwayTeamName = awayInfo.Name,
                        AwayTeamLogo = awayInfo.Logo,
                        HomeTeamCode = homeInfo.Code,
                        HomeTeamName = homeInfo.Name,
                        HomeTeamLogo = homeInfo.Logo,
                        GameTime = commenceTime,
                        Spread = spread,
                        OverUnder = overUnder,
                        HomeMoneyline = homeMoneyline,
                        AwayMoneyline = awayMoneyline,
                        Status = "Scheduled"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing game from Odds API");
                    skippedGames++;
                }
            }

            // Summary logging
            if (unmappedTeams.Any())
            {
                _logger.LogWarning("⚠️ Unmapped teams detected: {UnmappedTeams}. Add these to MapTeamNameToCode() dictionary.", 
                    string.Join(", ", unmappedTeams));
            }

            _logger.LogInformation("Parsing complete: {ParsedGames}/{TotalGames} games parsed successfully, {SkippedGames} skipped", 
                games.Count, totalGames, skippedGames);

            return games.OrderBy(g => g.GameTime).ToList();
        }

        private List<CBBGameMatchup> ParseCBBOddsApiResponse(JsonElement oddsData)
        {
            var games = new List<CBBGameMatchup>();

            if (oddsData.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected response format from The Odds API");
                return games;
            }

            var totalGames = oddsData.GetArrayLength();
            _logger.LogInformation("Processing {TotalGames} games from The Odds API", totalGames);

            var skippedGames = 0;
            var unmappedTeams = new HashSet<string>();

            foreach (var game in oddsData.EnumerateArray())
            {
                try
                {
                    var homeTeam = game.GetProperty("home_team").GetString() ?? "";
                    var awayTeam = game.GetProperty("away_team").GetString() ?? "";
                    var commenceTime = game.GetProperty("commence_time").GetDateTime();

                    // Ensure DateTime is properly marked as UTC for correct timezone conversion
                    if (commenceTime.Kind == DateTimeKind.Unspecified)
                    {
                        commenceTime = DateTime.SpecifyKind(commenceTime, DateTimeKind.Utc);
                    }

                    // Map team names to our codes
                    var homeTeamCode = MapTeamNameToCode(homeTeam);
                    var awayTeamCode = MapTeamNameToCode(awayTeam);

                    if (string.IsNullOrEmpty(homeTeamCode) || string.IsNullOrEmpty(awayTeamCode))
                    {
                        _logger.LogWarning("Could not map teams: {Home} ({HomeCode}) / {Away} ({AwayCode})",
                            homeTeam, homeTeamCode, awayTeam, awayTeamCode);

                        if (string.IsNullOrEmpty(homeTeamCode)) unmappedTeams.Add(homeTeam);
                        if (string.IsNullOrEmpty(awayTeamCode)) unmappedTeams.Add(awayTeam);

                        skippedGames++;
                        continue;
                    }

                    _logger.LogDebug("Mapped teams: {Away} → {AwayCode} @ {Home} → {HomeCode}",
                        awayTeam, awayTeamCode, homeTeam, homeTeamCode);

                    var homeInfo = _teamInfo.GetValueOrDefault(homeTeamCode);
                    var awayInfo = _teamInfo.GetValueOrDefault(awayTeamCode);

                    if (homeInfo == default || awayInfo == default)
                    {
                        _logger.LogWarning("Team code lookup failed: {HomeCode} or {AwayCode} not found in team info dictionary",
                            homeTeamCode, awayTeamCode);
                        skippedGames++;
                        continue;
                    }

                    // Extract betting lines from bookmakers
                    decimal? spread = null;
                    decimal? overUnder = null;
                    int? homeMoneyline = null;
                    int? awayMoneyline = null;

                    if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var bookmaker in bookmakers.EnumerateArray())
                        {
                            if (bookmaker.TryGetProperty("markets", out var markets) && markets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var market in markets.EnumerateArray())
                                {
                                    var marketKey = market.GetProperty("key").GetString();

                                    if (marketKey == "spreads" && !spread.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined)
                                        {
                                            spread = homeOutcome.GetProperty("point").GetDecimal();
                                        }
                                    }
                                    else if (marketKey == "totals" && !overUnder.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        if (outcomes.Any())
                                        {
                                            overUnder = outcomes.First().GetProperty("point").GetDecimal();
                                        }
                                    }
                                    else if (marketKey == "h2h")
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        var awayOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == awayTeam);

                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined && !homeMoneyline.HasValue)
                                        {
                                            homeMoneyline = homeOutcome.GetProperty("price").GetInt32();
                                        }
                                        if (awayOutcome.ValueKind != JsonValueKind.Undefined && !awayMoneyline.HasValue)
                                        {
                                            awayMoneyline = awayOutcome.GetProperty("price").GetInt32();
                                        }
                                    }
                                }
                            }

                            // Break after first bookmaker with data
                            if (spread.HasValue && overUnder.HasValue)
                            {
                                break;
                            }
                        }
                    }

                    _logger.LogDebug("Successfully parsed game: {AwayCode} @ {HomeCode} at {GameTime} (Spread: {Spread}, O/U: {OverUnder})",
                        awayTeamCode, homeTeamCode, commenceTime, spread, overUnder);

                    games.Add(new CBBGameMatchup
                    {
                        GameId = $"{awayTeamCode.ToLower()}-{homeTeamCode.ToLower()}-{commenceTime:yyyyMMdd}",
                        AwayTeamCode = awayInfo.Code,
                        AwayTeamName = awayInfo.Name,
                        AwayTeamLogo = awayInfo.Logo,
                        HomeTeamCode = homeInfo.Code,
                        HomeTeamName = homeInfo.Name,
                        HomeTeamLogo = homeInfo.Logo,
                        GameTime = commenceTime,
                        Spread = spread,
                        OverUnder = overUnder,
                        HomeMoneyline = homeMoneyline,
                        AwayMoneyline = awayMoneyline,
                        Status = "Scheduled"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing game from Odds API");
                    skippedGames++;
                }
            }

            // Summary logging
            if (unmappedTeams.Any())
            {
                _logger.LogWarning("⚠️ Unmapped teams detected: {UnmappedTeams}. Add these to MapTeamNameToCode() dictionary.",
                    string.Join(", ", unmappedTeams));
            }

            _logger.LogInformation("Parsing complete: {ParsedGames}/{TotalGames} games parsed successfully, {SkippedGames} skipped",
                games.Count, totalGames, skippedGames);

            return games.OrderBy(g => g.GameTime).ToList();
        }

        private string MapTeamNameToCode(string teamName)
        {
            // Map The Odds API team names to our team codes
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Atlanta Hawks", "ATL" },
                { "Boston Celtics", "BOS" },
                { "Brooklyn Nets", "BKN" },
                { "Charlotte Hornets", "CHA" },
                { "Chicago Bulls", "CHI" },
                { "Cleveland Cavaliers", "CLE" },
                { "Dallas Mavericks", "DAL" },
                { "Denver Nuggets", "DEN" },
                { "Detroit Pistons", "DET" },
                { "Golden State Warriors", "GSW" },
                { "Houston Rockets", "HOU" },
                { "Indiana Pacers", "IND" },
                { "Los Angeles Clippers", "LAC" },
                { "LA Clippers", "LAC" },  // Alternative
                { "Los Angeles Lakers", "LAL" },
                { "Memphis Grizzlies", "MEM" },
                { "Miami Heat", "MIA" },
                { "Milwaukee Bucks", "MIL" },
                { "Minnesota Timberwolves", "MIN" },
                { "New Orleans Pelicans", "NOP" },
                { "New York Knicks", "NYK" },
                { "Oklahoma City Thunder", "OKC" },
                { "Orlando Magic", "ORL" },
                { "Philadelphia 76ers", "PHI" },
                { "Phoenix Suns", "PHX" },
                { "Portland Trail Blazers", "POR" },
                { "Sacramento Kings", "SAC" },
                { "San Antonio Spurs", "SAS" },
                { "Toronto Raptors", "TOR" },
                { "Utah Jazz", "UTA" },
                { "Washington Wizards", "WAS" }
            };

            return mapping.GetValueOrDefault(teamName, "");
        }

        private async Task<List<NBAGameMatchup>> GetAIGeneratedGamesAsync(CancellationToken cancellationToken)
        {
            if (_chatClient == null)
            {
                _logger.LogInformation("No AI available, using fallback game data");
                return GetFallbackGames();
            }

            try
            {
                _logger.LogInformation("Using AI to generate game schedule as fallback");
                
                var today = DateTime.UtcNow.Date;
                var tomorrow = today.AddDays(1);

                var prompt = $@"Generate a realistic NBA game schedule for today ({today:MMM dd, yyyy}) and tomorrow ({tomorrow:MMM dd, yyyy}).

Create 5-8 NBA games with the following details for each game:
- Away team code (3 letters, e.g., BOS, LAL, GSW)
- Home team code (3 letters)
- Game time in UTC (realistic NBA game times: evening games typically 7-10 PM local time)
- Realistic betting odds:
  - Spread (home team perspective, e.g., -3.5 or +5.0)
  - Over/Under total points (typically 210-235)
  - Moneyline (favorite negative, underdog positive, e.g., -150, +130)

Use ONLY these NBA teams: BOS (Boston Celtics), MIL (Milwaukee Bucks), DEN (Denver Nuggets), UTA (Utah Jazz), HOU (Houston Rockets), WAS (Washington Wizards), LAL (Lakers), GSW (Warriors), PHI (76ers), MIA (Heat), BKN (Nets), DAL (Mavericks), PHX (Suns), LAC (Clippers)

Return ONLY a JSON array with this exact structure (no markdown, no explanations):
[
  {{
    ""awayTeam"": ""BOS"",
    ""homeTeam"": ""MIL"",
    ""gameTime"": ""2024-01-15T00:30:00Z"",
    ""spread"": -2.5,
    ""overUnder"": 218.5,
    ""awayMoneyline"": 120,
    ""homeMoneyline"": -140
  }}
]

Ensure game times are realistic for today/tomorrow and teams don't play multiple games in the same day.";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage("You are an NBA schedule generator. Return only valid JSON arrays with realistic game data. Never include markdown formatting or explanations."),
                    new UserChatMessage(prompt)
                };

                var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
                var content = response.Value.Content[0].Text;

                // Clean up the response (remove markdown if present)
                content = content.Trim();
                if (content.StartsWith("```json")) content = content.Substring(7);
                if (content.StartsWith("```")) content = content.Substring(3);
                if (content.EndsWith("```")) content = content.Substring(0, content.Length - 3);
                content = content.Trim();

                // Parse the JSON response
                var gamesData = JsonSerializer.Deserialize<List<AIGameData>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (gamesData == null || !gamesData.Any())
                {
                    _logger.LogWarning("No games returned from AI, using static fallback");
                    return GetFallbackGames();
                }

                // Convert AI data to game matchups
                var games = new List<NBAGameMatchup>();
                foreach (var gameData in gamesData)
                {
                    var awayTeam = _teamInfo.GetValueOrDefault(gameData.AwayTeam.ToUpper());
                    var homeTeam = _teamInfo.GetValueOrDefault(gameData.HomeTeam.ToUpper());

                    if (awayTeam == default || homeTeam == default)
                    {
                        _logger.LogWarning("Unknown team codes: {Away} or {Home}", gameData.AwayTeam, gameData.HomeTeam);
                        continue;
                    }

                    games.Add(new NBAGameMatchup
                    {
                        GameId = $"{gameData.AwayTeam.ToLower()}-{gameData.HomeTeam.ToLower()}-{gameData.GameTime:yyyyMMdd}",
                        AwayTeamCode = awayTeam.Code,
                        AwayTeamName = awayTeam.Name,
                        AwayTeamLogo = awayTeam.Logo,
                        HomeTeamCode = homeTeam.Code,
                        HomeTeamName = homeTeam.Name,
                        HomeTeamLogo = homeTeam.Logo,
                        GameTime = gameData.GameTime,
                        Spread = gameData.Spread,
                        OverUnder = gameData.OverUnder,
                        AwayMoneyline = gameData.AwayMoneyline,
                        HomeMoneyline = gameData.HomeMoneyline,
                        Status = "Scheduled"
                    });
                }

                _logger.LogInformation("Successfully generated {Count} NBA games from AI", games.Count);
                return games.OrderBy(g => g.GameTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating games from AI, using static fallback");
                return GetFallbackGames();
            }
        }

        private void MergeScoresWithGames(List<NBAGameMatchup> games, IReadOnlyList<NBATickerView> liveScores)
        {
            _logger.LogInformation("Merging {ScoreCount} live scores with {GameCount} games", liveScores.Count, games.Count);
            
            foreach (var game in games)
            {
                // Try to find matching live score by team codes
                var liveGameIndex = -1;
                for (int i = 0; i < liveScores.Count; i++)
                {
                    var score = liveScores[i];
                    if (score.AwayTeam?.Equals(game.AwayTeamCode, StringComparison.OrdinalIgnoreCase) == true && 
                        score.HomeTeam?.Equals(game.HomeTeamCode, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        liveGameIndex = i;
                        break;
                    }
                }
                
                if (liveGameIndex >= 0)
                {
                    var liveGame = liveScores[liveGameIndex];
            
                    // Merge the scores and status
                    game.AwayScore = liveGame.AwayScore;
                    game.HomeScore = liveGame.HomeScore;
                    game.Status = DetermineGameStatus(liveGame.IsLive, liveGame.IsFinal);
            
                    _logger.LogInformation("✅ Merged score: {Away} {AwayScore} @ {Home} {HomeScore} - {Status}",
                        game.AwayTeamCode, game.AwayScore ?? 0, game.HomeTeamCode, game.HomeScore ?? 0, game.Status);
                }
                else
                {
                    _logger.LogDebug("No live score found for {Away} @ {Home}", game.AwayTeamCode, game.HomeTeamCode);
                }
            }
        }

        // Helper method using correct NBATickerView properties
        private string DetermineGameStatus(bool isLive, bool isFinal)
        {
            if (isFinal)
                return "Final";
            
            if (isLive)
                return "Live";
            
            return "Scheduled";
        }

        private List<NBAGameMatchup> GetFallbackGames()
        {
            var games = new List<NBAGameMatchup>
            {
                new NBAGameMatchup
                {
                    GameId = "bos-mil-20260303",
                    AwayTeamCode = "BOS",
                    AwayTeamName = "Boston Celtics",
                    AwayTeamLogo = "https://a.espncdn.com/i/teamlogos/nba/500/bos.png",
                    HomeTeamCode = "MIL",
                    HomeTeamName = "Milwaukee Bucks",
                    HomeTeamLogo = "https://a.espncdn.com/i/teamlogos/nba/500/mil.png",
                    GameTime = DateTime.UtcNow.AddHours(3),
                    Spread = 2.5m,
                    OverUnder = 215.5m,
                    AwayMoneyline = -120,
                    HomeMoneyline = 100,
                    Status = "Scheduled"
                },
                new NBAGameMatchup
                {
                    GameId = "den-uta-20260303",
                    AwayTeamCode = "DEN",
                    AwayTeamName = "Denver Nuggets",
                    AwayTeamLogo = "https://a.espncdn.com/i/teamlogos/nba/500/den.png",
                    HomeTeamCode = "UTA",
                    HomeTeamName = "Utah Jazz",
                    HomeTeamLogo = "https://a.espncdn.com/i/teamlogos/nba/500/utah.png",
                    GameTime = DateTime.UtcNow.AddHours(5),
                    Spread = 11.5m,
                    OverUnder = 243.5m,
                    AwayMoneyline = -450,
                    HomeMoneyline = 350,
                    Status = "Scheduled"
                },
                new NBAGameMatchup
                {
                    GameId = "hou-was-20260303",
                    AwayTeamCode = "HOU",
                    AwayTeamName = "Houston Rockets",
                    AwayTeamLogo = "https://a.espncdn.com/i/teamlogos/nba/500/hou.png",
                    HomeTeamCode = "WAS",
                    HomeTeamName = "Washington Wizards",
                    HomeTeamLogo = "https://a.espncdn.com/i/teamlogos/nba/500/wsh.png",
                    GameTime = DateTime.UtcNow.AddHours(2.5),
                    Spread = 14.5m,
                    OverUnder = 225.5m,
                    AwayMoneyline = -800,
                    HomeMoneyline = 600,
                    Status = "Scheduled"
                }
            };

            _logger.LogInformation("Returning {Count} fallback NBA games", games.Count);
            return games;
        }

        private Dictionary<string, (string Name, string Logo, string Code)> InitializeTeamInfo()
        {
            // Using ESPN logo CDN to match the NBA Ticker
            return new Dictionary<string, (string Name, string Logo, string Code)>
            {
                { "ATL", ("Atlanta Hawks", "https://a.espncdn.com/i/teamlogos/nba/500/atl.png", "ATL") },
                { "BOS", ("Boston Celtics", "https://a.espncdn.com/i/teamlogos/nba/500/bos.png", "BOS") },
                { "BKN", ("Brooklyn Nets", "https://a.espncdn.com/i/teamlogos/nba/500/bkn.png", "BKN") },
                { "CHA", ("Charlotte Hornets", "https://a.espncdn.com/i/teamlogos/nba/500/cha.png", "CHA") },
                { "CHI", ("Chicago Bulls", "https://a.espncdn.com/i/teamlogos/nba/500/chi.png", "CHI") },
                { "CLE", ("Cleveland Cavaliers", "https://a.espncdn.com/i/teamlogos/nba/500/cle.png", "CLE") },
                { "DAL", ("Dallas Mavericks", "https://a.espncdn.com/i/teamlogos/nba/500/dal.png", "DAL") },
                { "DEN", ("Denver Nuggets", "https://a.espncdn.com/i/teamlogos/nba/500/den.png", "DEN") },
                { "DET", ("Detroit Pistons", "https://a.espncdn.com/i/teamlogos/nba/500/det.png", "DET") },
                { "GSW", ("Golden State Warriors", "https://a.espncdn.com/i/teamlogos/nba/500/gs.png", "GSW") },
                { "HOU", ("Houston Rockets", "https://a.espncdn.com/i/teamlogos/nba/500/hou.png", "HOU") },
                { "IND", ("Indiana Pacers", "https://a.espncdn.com/i/teamlogos/nba/500/ind.png", "IND") },
                { "LAC", ("LA Clippers", "https://a.espncdn.com/i/teamlogos/nba/500/lac.png", "LAC") },
                { "LAL", ("Los Angeles Lakers", "https://a.espncdn.com/i/teamlogos/nba/500/lal.png", "LAL") },
                { "MEM", ("Memphis Grizzlies", "https://a.espncdn.com/i/teamlogos/nba/500/mem.png", "MEM") },
                { "MIA", ("Miami Heat", "https://a.espncdn.com/i/teamlogos/nba/500/mia.png", "MIA") },
                { "MIL", ("Milwaukee Bucks", "https://a.espncdn.com/i/teamlogos/nba/500/mil.png", "MIL") },
                { "MIN", ("Minnesota Timberwolves", "https://a.espncdn.com/i/teamlogos/nba/500/min.png", "MIN") },
                { "NOP", ("New Orleans Pelicans", "https://a.espncdn.com/i/teamlogos/nba/500/no.png", "NOP") },
                { "NYK", ("New York Knicks", "https://a.espncdn.com/i/teamlogos/nba/500/ny.png", "NYK") },
                { "OKC", ("Oklahoma City Thunder", "https://a.espncdn.com/i/teamlogos/nba/500/okc.png", "OKC") },
                { "ORL", ("Orlando Magic", "https://a.espncdn.com/i/teamlogos/nba/500/orl.png", "ORL") },
                { "PHI", ("Philadelphia 76ers", "https://a.espncdn.com/i/teamlogos/nba/500/phi.png", "PHI") },
                { "PHX", ("Phoenix Suns", "https://a.espncdn.com/i/teamlogos/nba/500/phx.png", "PHX") },
                { "POR", ("Portland Trail Blazers", "https://a.espncdn.com/i/teamlogos/nba/500/por.png", "POR") },
                { "SAC", ("Sacramento Kings", "https://a.espncdn.com/i/teamlogos/nba/500/sac.png", "SAC") },
                { "SAS", ("San Antonio Spurs", "https://a.espncdn.com/i/teamlogos/nba/500/sa.png", "SAS") },
                { "TOR", ("Toronto Raptors", "https://a.espncdn.com/i/teamlogos/nba/500/tor.png", "TOR") },
                { "UTA", ("Utah Jazz", "https://a.espncdn.com/i/teamlogos/nba/500/utah.png", "UTA") },
                { "WAS", ("Washington Wizards", "https://a.espncdn.com/i/teamlogos/nba/500/wsh.png", "WAS") }
            };
        }

        // Helper class for deserializing AI response
        private class AIGameData
        {
            public string AwayTeam { get; set; } = string.Empty;
            public string HomeTeam { get; set; } = string.Empty;
            public DateTime GameTime { get; set; }
            public decimal Spread { get; set; }
            public decimal OverUnder { get; set; }
            public int AwayMoneyline { get; set; }
            public int HomeMoneyline { get; set; }
        }
    }
}



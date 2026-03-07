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
        private readonly Dictionary<string, (string Name, string Logo, string Code)> _teamInfo;

        public NBAGamesService(
            IConfiguration configuration, 
            ILogger<NBAGamesService> logger,
            TheOddsApiClient oddsApiClient)
        {
            _logger = logger;
            _oddsApiClient = oddsApiClient;
            
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
            _logger.LogInformation("Fetching real NBA games from The Odds API");
            
            // Fetch real games from The Odds API
            var oddsData = await _oddsApiClient.GetAsync("/v4/sports/basketball_nba/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american", cancellationToken);
            
            var games = ParseNBAOddsApiResponse(oddsData);
            
            if (games.Any())        
            {
                _logger.LogInformation("Successfully fetched {Count} real NBA games from The Odds API", games.Count);
                return games;
            }
            
            // Return empty list instead of falling back to mock data
            _logger.LogWarning("No games available from The Odds API");
            return new List<NBAGameMatchup>();
        }

        public async Task<List<CBBGameMatchup>> GetUpcomingCBBGamesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching real NCAA games from The Odds API");

            // Fetch real games from The Odds API
            var oddsData = await _oddsApiClient.GetAsync("/v4/sports/basketball_cbb/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american", cancellationToken);

            var games = ParseCBBOddsApiResponse(oddsData);

            if (games.Any())
            {
                _logger.LogInformation("Successfully fetched {Count} real NBA games from The Odds API", games.Count);
                return games;
            }

            // Return empty list instead of falling back to mock data
            _logger.LogWarning("No games available from The Odds API");
            return new List<CBBGameMatchup>();
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

        private List<NBAGameMatchup> GetFallbackGames()
        {
            var games = new List<NBAGameMatchup>
            {
                new NBAGameMatchup
                {
                    GameId = "bos-mil-20260303",
                    AwayTeamCode = "BOS",
                    AwayTeamName = "Boston Celtics",
                    AwayTeamLogo = "https://sports.cbsimg.net/fly/images/nba/logos/team/334.svg",
                    HomeTeamCode = "MIL",
                    HomeTeamName = "Milwaukee Bucks",
                    HomeTeamLogo = "https://sports.cbsimg.net/fly/images/nba/logos/team/347.svg",
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
                    AwayTeamLogo = "https://sports.cbsimg.net/fly/images/nba/logos/team/339.svg",
                    HomeTeamCode = "UTA",
                    HomeTeamName = "Utah Jazz",
                    HomeTeamLogo = "https://sports.cbsimg.net/fly/images/nba/logos/team/359.svg",
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
                    AwayTeamLogo = "https://sports.cbsimg.net/fly/images/nba/logos/team/342.svg",
                    HomeTeamCode = "WAS",
                    HomeTeamName = "Washington Wizards",
                    HomeTeamLogo = "https://sports.cbsimg.net/fly/images/nba/logos/team/361.svg",
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
            return new Dictionary<string, (string Name, string Logo, string Code)>
            {
                { "ATL", ("Atlanta Hawks", "https://sports.cbsimg.net/fly/images/nba/logos/team/333.svg", "ATL") },
                { "BOS", ("Boston Celtics", "https://sports.cbsimg.net/fly/images/nba/logos/team/334.svg", "BOS") },
                { "BKN", ("Brooklyn Nets", "https://sports.cbsimg.net/fly/images/nba/logos/team/335.svg", "BKN") },
                { "CHA", ("Charlotte Hornets", "https://sports.cbsimg.net/fly/images/nba/logos/team/336.svg", "CHA") },
                { "CHI", ("Chicago Bulls", "https://sports.cbsimg.net/fly/images/nba/logos/team/337.svg", "CHI") },
                { "CLE", ("Cleveland Cavaliers", "https://sports.cbsimg.net/fly/images/nba/logos/team/338.svg", "CLE") },
                { "DAL", ("Dallas Mavericks", "https://sports.cbsimg.net/fly/images/nba/logos/team/338.svg", "DAL") },
                { "DEN", ("Denver Nuggets", "https://sports.cbsimg.net/fly/images/nba/logos/team/339.svg", "DEN") },
                { "DET", ("Detroit Pistons", "https://sports.cbsimg.net/fly/images/nba/logos/team/340.svg", "DET") },
                { "GSW", ("Golden State Warriors", "https://sports.cbsimg.net/fly/images/nba/logos/team/341.svg", "GSW") },
                { "HOU", ("Houston Rockets", "https://sports.cbsimg.net/fly/images/nba/logos/team/342.svg", "HOU") },
                { "IND", ("Indiana Pacers", "https://sports.cbsimg.net/fly/images/nba/logos/team/343.svg", "IND") },
                { "LAC", ("LA Clippers", "https://sports.cbsimg.net/fly/images/nba/logos/team/344.svg", "LAC") },
                { "LAL", ("Los Angeles Lakers", "https://sports.cbsimg.net/fly/images/nba/logos/team/343.svg", "LAL") },
                { "MEM", ("Memphis Grizzlies", "https://sports.cbsimg.net/fly/images/nba/logos/team/345.svg", "MEM") },
                { "MIA", ("Miami Heat", "https://sports.cbsimg.net/fly/images/nba/logos/team/346.svg", "MIA") },
                { "MIL", ("Milwaukee Bucks", "https://sports.cbsimg.net/fly/images/nba/logos/team/347.svg", "MIL") },
                { "MIN", ("Minnesota Timberwolves", "https://sports.cbsimg.net/fly/images/nba/logos/team/348.svg", "MIN") },
                { "NOP", ("New Orleans Pelicans", "https://sports.cbsimg.net/fly/images/nba/logos/team/349.svg", "NOP") },
                { "NYK", ("New York Knicks", "https://sports.cbsimg.net/fly/images/nba/logos/team/350.svg", "NYK") },
                { "OKC", ("Oklahoma City Thunder", "https://sports.cbsimg.net/fly/images/nba/logos/team/351.svg", "OKC") },
                { "ORL", ("Orlando Magic", "https://sports.cbsimg.net/fly/images/nba/logos/team/352.svg", "ORL") },
                { "PHI", ("Philadelphia 76ers", "https://sports.cbsimg.net/fly/images/nba/logos/team/352.svg", "PHI") },
                { "PHX", ("Phoenix Suns", "https://sports.cbsimg.net/fly/images/nba/logos/team/353.svg", "PHX") },
                { "POR", ("Portland Trail Blazers", "https://sports.cbsimg.net/fly/images/nba/logos/team/354.svg", "POR") },
                { "SAC", ("Sacramento Kings", "https://sports.cbsimg.net/fly/images/nba/logos/team/355.svg", "SAC") },
                { "SAS", ("San Antonio Spurs", "https://sports.cbsimg.net/fly/images/nba/logos/team/356.svg", "SAS") },
                { "TOR", ("Toronto Raptors", "https://sports.cbsimg.net/fly/images/nba/logos/team/357.svg", "TOR") },
                { "UTA", ("Utah Jazz", "https://sports.cbsimg.net/fly/images/nba/logos/team/359.svg", "UTA") },
                { "WAS", ("Washington Wizards", "https://sports.cbsimg.net/fly/images/nba/logos/team/361.svg", "WAS") }
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



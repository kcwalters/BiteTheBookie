using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class NBAGamesService : INBAGamesService
    {
        private readonly ILogger<NBAGamesService> _logger;
        private readonly ChatClient? _chatClient;
        private readonly Dictionary<string, (string Name, string Logo, string Code)> _teamInfo;

        public NBAGamesService(IConfiguration configuration, ILogger<NBAGamesService> logger)
        {
            _logger = logger;
            
            // Initialize team information
            _teamInfo = InitializeTeamInfo();

            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var apiKey = configuration["AzureOpenAI:ApiKey"];
            var deploymentName = configuration["AzureOpenAI:DeploymentName"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Azure OpenAI configuration is missing. Will use fallback game data.");
                _chatClient = null;
            }
            else
            {
                var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                _chatClient = azureClient.GetChatClient(deploymentName);
            }
        }

        public async Task<List<NBAGameMatchup>> GetUpcomingGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_chatClient == null)
            {
                _logger.LogInformation("Using fallback game data");
                return GetFallbackGames();
            }

            try
            {
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

                _logger.LogInformation("OpenAI Response: {Content}", content);

                // Clean up the response (remove markdown if present)
                content = content.Trim();
                if (content.StartsWith("```json"))
                {
                    content = content.Substring(7);
                }
                if (content.StartsWith("```"))
                {
                    content = content.Substring(3);
                }
                if (content.EndsWith("```"))
                {
                    content = content.Substring(0, content.Length - 3);
                }
                content = content.Trim();

                // Parse the JSON response
                var gamesData = JsonSerializer.Deserialize<List<AIGameData>>(content, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

                if (gamesData == null || !gamesData.Any())
                {
                    _logger.LogWarning("No games returned from OpenAI, using fallback");
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

                _logger.LogInformation("Successfully generated {Count} NBA games from OpenAI", games.Count);
                return games.OrderBy(g => g.GameTime).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating games from OpenAI");
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
                { "BOS", ("Boston Celtics", "https://sports.cbsimg.net/fly/images/nba/logos/team/334.svg", "BOS") },
                { "MIL", ("Milwaukee Bucks", "https://sports.cbsimg.net/fly/images/nba/logos/team/347.svg", "MIL") },
                { "DEN", ("Denver Nuggets", "https://sports.cbsimg.net/fly/images/nba/logos/team/339.svg", "DEN") },
                { "UTA", ("Utah Jazz", "https://sports.cbsimg.net/fly/images/nba/logos/team/359.svg", "UTA") },
                { "HOU", ("Houston Rockets", "https://sports.cbsimg.net/fly/images/nba/logos/team/342.svg", "HOU") },
                { "WAS", ("Washington Wizards", "https://sports.cbsimg.net/fly/images/nba/logos/team/361.svg", "WAS") },
                { "LAL", ("Los Angeles Lakers", "https://sports.cbsimg.net/fly/images/nba/logos/team/343.svg", "LAL") },
                { "GSW", ("Golden State Warriors", "https://sports.cbsimg.net/fly/images/nba/logos/team/341.svg", "GSW") },
                { "PHI", ("Philadelphia 76ers", "https://sports.cbsimg.net/fly/images/nba/logos/team/352.svg", "PHI") },
                { "MIA", ("Miami Heat", "https://sports.cbsimg.net/fly/images/nba/logos/team/346.svg", "MIA") },
                { "BKN", ("Brooklyn Nets", "https://sports.cbsimg.net/fly/images/nba/logos/team/335.svg", "BKN") },
                { "DAL", ("Dallas Mavericks", "https://sports.cbsimg.net/fly/images/nba/logos/team/338.svg", "DAL") },
                { "PHX", ("Phoenix Suns", "https://sports.cbsimg.net/fly/images/nba/logos/team/353.svg", "PHX") },
                { "LAC", ("LA Clippers", "https://sports.cbsimg.net/fly/images/nba/logos/team/344.svg", "LAC") }
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


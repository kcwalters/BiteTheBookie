using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class InjuryReportService : IInjuryReportService
    {
        private readonly ILogger<InjuryReportService> _logger;
        private readonly ChatClient? _chatClient;
        private readonly EspnApiClient _espnApiClient;

        public InjuryReportService(
            IConfiguration configuration, 
            ILogger<InjuryReportService> logger,
            EspnApiClient espnApiClient)
        {
            _logger = logger;
            _espnApiClient = espnApiClient;

            var endpoint = configuration["AzureOpenAI:Endpoint"];
            var apiKey = configuration["AzureOpenAI:ApiKey"];
            var deploymentName = configuration["AzureOpenAI:DeploymentName"];

            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(deploymentName))
            {
                _logger.LogWarning("Azure OpenAI configuration is missing. Will use mock injury data as final fallback.");
                _chatClient = null;
            }
            else
            {
                var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));
                _chatClient = azureClient.GetChatClient(deploymentName);
            }
        }

        public async Task<List<PlayerInjuryReport>> GetCurrentInjuriesAsync(string teamCode, CancellationToken cancellationToken = default)
        {
            // Priority 1: Try ESPN API for real data
            try
            {
                _logger.LogInformation("Fetching real injury data from ESPN API for {TeamCode}", teamCode);
                var espnInjuries = await _espnApiClient.GetTeamInjuriesAsync(teamCode, cancellationToken);
                
                if (espnInjuries.Any())
                {
                    _logger.LogInformation("? Successfully retrieved {Count} real injuries from ESPN for {TeamCode}", espnInjuries.Count, teamCode);
                    return espnInjuries;
                }
                
                _logger.LogInformation("No injuries found on ESPN for {TeamCode}, trying AI fallback", teamCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ESPN API failed for {TeamCode}, trying AI fallback", teamCode);
            }

            // Priority 2: Try OpenAI for recent injury knowledge
            if (_chatClient != null)
            {
                try
                {
                    return await GetAIInjuriesAsync(teamCode, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AI failed for {TeamCode}, using mock data", teamCode);
                }
            }

            // Priority 3: Use mock data as last resort
            _logger.LogWarning("Using mock injury data for {TeamCode}", teamCode);
            return GetMockInjuries(teamCode);
        }

        private async Task<List<PlayerInjuryReport>> GetAIInjuriesAsync(string teamCode, CancellationToken cancellationToken)
        {
            try
            {
                var prompt = $@"Provide the CURRENT, REAL-WORLD injury report for the NBA team: {teamCode} as of today's date.

IMPORTANT: 
- Use your knowledge of ACTUAL CURRENT injuries for this team
- Include players who are currently listed as Out, Questionable, or Doubtful
- Use real player names who are actually on the {teamCode} roster
- Include accurate injury descriptions based on recent news
- If you don't have current information, provide the most recent known injuries

Return ONLY a JSON array (no markdown, no explanations):
[
  {{
    ""playerName"": ""Actual Player Name"",
    ""teamCode"": ""{teamCode}"",
    ""injuryStatus"": ""Out"" or ""Questionable"" or ""Doubtful"",
    ""injuryDescription"": ""Actual injury (e.g., Ankle sprain, Knee soreness)"",
    ""reportedTime"": ""{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"",
    ""estimatedReturn"": ""ISO 8601 datetime or null""
  }}
]

Include all players currently on the injury report. If the team has no injuries, return an empty array: []

Team Code: {teamCode}
Today's Date: {DateTime.UtcNow:MMMM dd, yyyy}";

                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage($"You are an NBA injury report specialist with access to current injury information as of {DateTime.UtcNow:MMMM dd, yyyy}. Provide accurate, up-to-date injury reports for NBA teams. Return only valid JSON arrays with real current injury data. If you're unsure about current injuries, use the most recent information you have."),
                    new UserChatMessage(prompt)
                };

                var chatOptions = new ChatCompletionOptions
                {
                    Temperature = 0.3f // Lower temperature for more factual, consistent responses
                };

                var response = await _chatClient!.CompleteChatAsync(messages, chatOptions, cancellationToken);
                var content = response.Value.Content[0].Text.Trim();

                _logger.LogInformation("Received injury report from AI for {TeamCode}: {Preview}", teamCode, content.Substring(0, Math.Min(100, content.Length)));

                // Clean up response
                if (content.StartsWith("```json")) content = content.Substring(7);
                if (content.StartsWith("```")) content = content.Substring(3);
                if (content.EndsWith("```")) content = content.Substring(0, content.Length - 3);
                content = content.Trim();

                var injuries = JsonSerializer.Deserialize<List<PlayerInjuryReport>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<PlayerInjuryReport>();

                _logger.LogInformation("Retrieved {Count} injuries for {TeamCode} from AI", injuries.Count, teamCode);
                
                // Log each injury for debugging
                foreach (var injury in injuries)
                {
                    _logger.LogInformation("Injury: {Player} ({Team}) - {Status} - {Description}", 
                        injury.PlayerName, injury.TeamCode, injury.InjuryStatus, injury.InjuryDescription);
                }

                return injuries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting injuries for {TeamCode} from AI, using mock data", teamCode);
                return GetMockInjuries(teamCode);
            }
        }

        public async Task<List<PlayerInjuryReport>> GetCurrentInjuriesForGameAsync(
            string awayTeamCode, 
            string homeTeamCode, 
            DateTime gameTime, 
            CancellationToken cancellationToken = default)
        {
            var awayInjuries = await GetCurrentInjuriesAsync(awayTeamCode, cancellationToken);
            var homeInjuries = await GetCurrentInjuriesAsync(homeTeamCode, cancellationToken);

            var allInjuries = awayInjuries.Concat(homeInjuries).ToList();

            // Count players who are OUT (no time filtering)
            var outInjuries = allInjuries
                .Where(i => i.InjuryStatus.Equals("Out", StringComparison.OrdinalIgnoreCase))
                .ToList();

            _logger.LogInformation("Found {Count} OUT injuries for game (no time restriction)", outInjuries.Count);

            return allInjuries; // Return all injuries, filtering by status happens in simulation service
        }

        private List<PlayerInjuryReport> GetMockInjuries(string teamCode)
        {
            // Mock injury data - in production this would come from an API like ESPN or NBA.com
            var mockInjuries = new Dictionary<string, List<PlayerInjuryReport>>
            {
                {
                    "BOS", new List<PlayerInjuryReport>
                    {
                        new PlayerInjuryReport
                        {
                            PlayerName = "Jayson Tatum",
                            TeamCode = "BOS",
                            InjuryStatus = "Out",
                            InjuryDescription = "Ankle injury",
                            ReportedTime = DateTime.UtcNow.AddHours(-6),
                            EstimatedReturn = DateTime.UtcNow.AddDays(1)
                        },
                        new PlayerInjuryReport
                        {
                            PlayerName = "Jaylen Brown",
                            TeamCode = "BOS",
                            InjuryStatus = "Out",
                            InjuryDescription = "Shoulder injury",
                            ReportedTime = DateTime.UtcNow.AddHours(-8),
                            EstimatedReturn = DateTime.UtcNow.AddDays(2)
                        },
                        new PlayerInjuryReport
                        {
                            PlayerName = "Robert Williams III",
                            TeamCode = "BOS",
                            InjuryStatus = "Out",
                            InjuryDescription = "Knee injury",
                            ReportedTime = DateTime.UtcNow.AddDays(-2),
                            EstimatedReturn = DateTime.UtcNow.AddDays(14)
                        }
                    }
                },
                {
                    "MIL", new List<PlayerInjuryReport>
                    {
                        new PlayerInjuryReport
                        {
                            PlayerName = "Jae Crowder",
                            TeamCode = "MIL",
                            InjuryStatus = "Questionable",
                            InjuryDescription = "Ankle sprain",
                            ReportedTime = DateTime.UtcNow.AddHours(-6),
                            EstimatedReturn = null
                        }
                    }
                },
                {
                    "DEN", new List<PlayerInjuryReport>()
                },
                {
                    "UTA", new List<PlayerInjuryReport>
                    {
                        new PlayerInjuryReport
                        {
                            PlayerName = "Taylor Hendricks",
                            TeamCode = "UTA",
                            InjuryStatus = "Out",
                            InjuryDescription = "Hamstring strain",
                            ReportedTime = DateTime.UtcNow.AddDays(-1),
                            EstimatedReturn = DateTime.UtcNow.AddDays(7)
                        }
                    }
                },
                {
                    "HOU", new List<PlayerInjuryReport>()
                },
                {
                    "WAS", new List<PlayerInjuryReport>
                    {
                        new PlayerInjuryReport
                        {
                            PlayerName = "Isaiah Livers",
                            TeamCode = "WAS",
                            InjuryStatus = "Out",
                            InjuryDescription = "Hip injury",
                            ReportedTime = DateTime.UtcNow.AddDays(-3),
                            EstimatedReturn = null
                        }
                    }
                },
                {
                    "LAL", new List<PlayerInjuryReport>()
                },
                {
                    "GSW", new List<PlayerInjuryReport>()
                },
                {
                    "PHI", new List<PlayerInjuryReport>()
                },
                {
                    "MIA", new List<PlayerInjuryReport>()
                },
                {
                    "BKN", new List<PlayerInjuryReport>()
                },
                {
                    "DAL", new List<PlayerInjuryReport>()
                },
                {
                    "PHX", new List<PlayerInjuryReport>()
                },
                {
                    "LAC", new List<PlayerInjuryReport>()
                }
            };

            return mockInjuries.GetValueOrDefault(teamCode.ToUpper(), new List<PlayerInjuryReport>());
        }
    }
}


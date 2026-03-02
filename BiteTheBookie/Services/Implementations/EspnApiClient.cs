using BiteTheBookie.Models;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class EspnApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnApiClient> _logger;

        public EspnApiClient(HttpClient httpClient, ILogger<EspnApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.BaseAddress = new Uri("https://site.api.espn.com/");
        }

        public async Task<List<PlayerInjuryReport>> GetTeamInjuriesAsync(string teamAbbreviation, CancellationToken cancellationToken = default)
        {
            try
            {
                var injuries = new List<PlayerInjuryReport>();
                
                // ESPN API endpoint for team injuries
                var response = await _httpClient.GetAsync($"apis/site/v2/sports/basketball/nba/teams/{teamAbbreviation.ToLower()}/injuries", cancellationToken);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN API returned {StatusCode} for team {Team}", response.StatusCode, teamAbbreviation);
                    return injuries;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);

                if (jsonDoc.RootElement.TryGetProperty("injuries", out var injuriesArray))
                {
                    foreach (var injury in injuriesArray.EnumerateArray())
                    {
                        try
                        {
                            var playerName = injury.GetProperty("athlete").GetProperty("displayName").GetString() ?? "";
                            var status = injury.GetProperty("status").GetString() ?? "";
                            var description = injury.TryGetProperty("details", out var details) 
                                ? (details.TryGetProperty("type", out var type) ? type.GetString() ?? "Unknown injury" : "Unknown injury")
                                : "Unknown injury";

                            var dateString = injury.TryGetProperty("date", out var date) ? date.GetString() : null;
                            var reportedTime = DateTime.UtcNow;
                            if (!string.IsNullOrEmpty(dateString) && DateTime.TryParse(dateString, out var parsedDate))
                            {
                                reportedTime = parsedDate.ToUniversalTime();
                            }

                            injuries.Add(new PlayerInjuryReport
                            {
                                PlayerName = playerName,
                                TeamCode = teamAbbreviation.ToUpper(),
                                InjuryStatus = MapEspnStatus(status),
                                InjuryDescription = description,
                                ReportedTime = reportedTime,
                                EstimatedReturn = null // ESPN doesn't always provide this
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error parsing injury from ESPN API");
                        }
                    }
                }

                _logger.LogInformation("Retrieved {Count} injuries from ESPN for {Team}", injuries.Count, teamAbbreviation);
                return injuries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching injuries from ESPN for {Team}", teamAbbreviation);
                return new List<PlayerInjuryReport>();
            }
        }

        private string MapEspnStatus(string espnStatus)
        {
            // Map ESPN status to our status format
            return espnStatus.ToLower() switch
            {
                "out" => "Out",
                "questionable" => "Questionable",
                "doubtful" => "Doubtful",
                "day to day" => "Day-to-Day",
                "day-to-day" => "Day-to-Day",
                _ => espnStatus
            };
        }
    }
}

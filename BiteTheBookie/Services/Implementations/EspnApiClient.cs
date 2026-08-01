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

        /// <summary>
        /// Fetches a live NBA team roster from the ESPN Site API.
        /// Handles both the grouped (position-grouped items array) and flat athletes formats.
        /// Returns null if the request fails.
        /// </summary>
        public async Task<NBATeamRoster?> GetTeamRosterAsync(string teamAbbreviation, CancellationToken cancellationToken = default)
        {
            try
            {
                var espnCode = MapToEspnCode(teamAbbreviation);
                var response = await _httpClient.GetAsync(
                    $"apis/site/v2/sports/basketball/nba/teams/{espnCode}/roster",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN roster API returned {Status} for {Team}", response.StatusCode, teamAbbreviation);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonDocument.Parse(content).RootElement;

                var teamName = teamAbbreviation;
                if (root.TryGetProperty("team", out var teamEl) &&
                    teamEl.TryGetProperty("displayName", out var dn))
                {
                    teamName = dn.GetString() ?? teamAbbreviation;
                }

                var players = new List<NBAPlayer>();

                if (root.TryGetProperty("athletes", out var athletesEl))
                {
                    foreach (var item in athletesEl.EnumerateArray())
                    {
                        // Grouped format: ESPN returns position groups, each with an "items" array
                        if (item.TryGetProperty("items", out var groupItems))
                        {
                            foreach (var athlete in groupItems.EnumerateArray())
                            {
                                var player = ParsePlayer(athlete);
                                if (player != null) players.Add(player);
                            }
                        }
                        else
                        {
                            // Flat format: each array item is directly a player object
                            var player = ParsePlayer(item);
                            if (player != null) players.Add(player);
                        }
                    }
                }

                _logger.LogInformation("ESPN roster: fetched {Count} players for {Team}", players.Count, teamAbbreviation);

                return new NBATeamRoster
                {
                    TeamCode = teamAbbreviation.ToUpper(),
                    TeamName = teamName,
                    Players = players
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch ESPN roster for {Team}", teamAbbreviation);
                return null;
            }
        }

        public async Task<List<PlayerInjuryReport>> GetTeamInjuriesAsync(string teamAbbreviation, CancellationToken cancellationToken = default)
        {
            try
            {
                var injuries = new List<PlayerInjuryReport>();

                var response = await _httpClient.GetAsync(
                    $"apis/site/v2/sports/basketball/nba/teams/{teamAbbreviation.ToLower()}/injuries",
                    cancellationToken);

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
                                reportedTime = parsedDate.ToUniversalTime();

                            injuries.Add(new PlayerInjuryReport
                            {
                                PlayerName = playerName,
                                TeamCode = teamAbbreviation.ToUpper(),
                                InjuryStatus = MapEspnStatus(status),
                                InjuryDescription = description,
                                ReportedTime = reportedTime,
                                EstimatedReturn = null
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

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static readonly HashSet<string> _nonRosterStatusTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "waived", "retired", "released", "suspended-indefinitely",
            "non-roster-invitee", "did-not-report"
        };

        private static NBAPlayer? ParsePlayer(JsonElement athlete)
        {
            if (!athlete.TryGetProperty("displayName", out var nameProp)) return null;
            var name = nameProp.GetString();
            if (string.IsNullOrEmpty(name)) return null;

            if (athlete.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
                return null;

            if (athlete.TryGetProperty("status", out var statusEl) &&
                statusEl.TryGetProperty("type", out var statusType))
            {
                var typeStr = statusType.GetString() ?? "";
                if (_nonRosterStatusTypes.Contains(typeStr))
                    return null;
            }

            var position = string.Empty;
            if (athlete.TryGetProperty("position", out var posProp) &&
                posProp.TryGetProperty("abbreviation", out var abbr))
                position = abbr.GetString() ?? string.Empty;

            // ── Season averages (ESPN returns a "statistics" array on the athlete node) ──
            double ppg = 0, rpg = 0, apg = 0;
            if (athlete.TryGetProperty("statistics", out var stats))
            {
                // ESPN returns categories as an array; find by name
                foreach (var cat in stats.EnumerateArray())
                {
                    if (!cat.TryGetProperty("name",  out var catName))  continue;
                    if (!cat.TryGetProperty("value", out var catValue)) continue;

                    switch (catName.GetString())
                    {
                        case "avgPoints":   ppg = catValue.GetDouble(); break;
                        case "avgRebounds": rpg = catValue.GetDouble(); break;
                        case "avgAssists":  apg = catValue.GetDouble(); break;
                    }
                }
            }

            return new NBAPlayer
            {
                Name           = name,
                Position       = position,
                IsStarter      = false,
                PointsPerGame   = ppg,
                ReboundsPerGame = rpg,
                AssistsPerGame  = apg,
            };
        }

        /// <summary>
        /// Maps internal app team codes to the abbreviations used by the ESPN Site API.
        /// ESPN uses shorter codes for a handful of teams (e.g. "gs" instead of "gsw").
        /// </summary>
        private static string MapToEspnCode(string teamCode) => teamCode.ToUpper() switch
        {
            "GSW" => "gs",
            "NOP" => "no",
            "NYK" => "ny",
            "SAS" => "sa",
            _ => teamCode.ToLower()
        };

        private static string MapEspnStatus(string espnStatus) => espnStatus.ToLower() switch
        {
            "out"          => "Out",
            "questionable" => "Questionable",
            "doubtful"     => "Doubtful",
            "day to day"   => "Day-to-Day",
            "day-to-day"   => "Day-to-Day",
            _              => espnStatus
        };

        // ── Generic roster helper ─────────────────────────────────────────────

        /// <summary>
        /// Fetches a roster from any ESPN Site API sport/league endpoint.
        /// Returns a list of player full names; empty list on failure.
        /// </summary>
        private async Task<List<string>> GetRosterNamesAsync(
            string sportPath,   // e.g. "football/nfl"
            string teamCode,
            CancellationToken cancellationToken)
        {
            try
            {
                var url = $"apis/site/v2/sports/{sportPath}/teams/{teamCode.ToLower()}/roster";
                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN {Sport} roster returned {Status} for {Team}", sportPath, response.StatusCode, teamCode);
                    return new List<string>();
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var root = JsonDocument.Parse(content).RootElement;

                var names = new List<string>();

                if (root.TryGetProperty("athletes", out var athletesEl))
                {
                    foreach (var item in athletesEl.EnumerateArray())
                    {
                        // Grouped format (position groups with "items" array)
                        if (item.TryGetProperty("items", out var groupItems))
                        {
                            foreach (var athlete in groupItems.EnumerateArray())
                                ExtractName(athlete, names);
                        }
                        else
                        {
                            ExtractName(item, names);
                        }
                    }
                }

                _logger.LogInformation("ESPN {Sport} roster: {Count} players for {Team}", sportPath, names.Count, teamCode);
                return names;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch ESPN {Sport} roster for {Team}", sportPath, teamCode);
                return new List<string>();
            }
        }

        private static void ExtractName(JsonElement athlete, List<string> names)
        {
            // Skip inactive/non-roster statuses
            if (athlete.TryGetProperty("active", out var activeProp) && !activeProp.GetBoolean())
                return;
            if (athlete.TryGetProperty("status", out var statusEl) &&
                statusEl.TryGetProperty("type", out var statusType) &&
                _nonRosterStatusTypes.Contains(statusType.GetString() ?? ""))
                return;

            if (athlete.TryGetProperty("displayName", out var nameProp))
            {
                var name = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }
        }

        /// <summary>Fetches the current NFL roster for a team by ESPN team code (e.g. "phi", "ne").</summary>
        public Task<List<string>> GetNflRosterAsync(string teamCode, CancellationToken cancellationToken = default)
            => GetRosterNamesAsync("football/nfl", teamCode, cancellationToken);

        /// <summary>Fetches the current CFB roster for a team by ESPN team code (e.g. "alabama", "ohio-state").</summary>
        public Task<List<string>> GetCfbRosterAsync(string teamCode, CancellationToken cancellationToken = default)
            => GetRosterNamesAsync("football/college-football", teamCode, cancellationToken);

        /// <summary>Fetches the current NHL roster for a team by ESPN team code (e.g. "pit", "bos").</summary>
        public Task<List<string>> GetNhlRosterAsync(string teamCode, CancellationToken cancellationToken = default)
            => GetRosterNamesAsync("hockey/nhl", teamCode, cancellationToken);

        /// <summary>Fetches the current NBA roster as a plain name list (parallel to GetTeamRosterAsync but simpler output).</summary>
        public async Task<List<string>> GetNbaRosterNamesAsync(string teamCode, CancellationToken cancellationToken = default)
        {
            var roster = await GetTeamRosterAsync(teamCode, cancellationToken);
            return roster?.Players.Select(p => p.Name).ToList() ?? new List<string>();
        }
    }
}

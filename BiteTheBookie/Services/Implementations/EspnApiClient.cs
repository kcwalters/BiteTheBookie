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
            _httpClient.BaseAddress = new Uri("https://site.web.api.espn.com/");
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

        // -- Helpers --------------------------------------------------------------

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

            // -- Season averages (ESPN returns a "statistics" array on the athlete node) --
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

        // -- Generic roster helper ---------------------------------------------

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

        /// <summary>
        /// Fetches live team details (location, venue, record, standing) from the ESPN Site
        /// API. <paramref name="sportLeaguePath"/> is the ESPN segment such as
        /// "football/college-football", "basketball/mens-college-basketball", or
        /// "football/nfl". <paramref name="teamIdOrCode"/> is the ESPN numeric id or code.
        /// Returns null if the request fails so callers can degrade gracefully.
        /// </summary>
        public async Task<EspnTeamDetails?> GetTeamDetailsAsync(string sportLeaguePath, string teamIdOrCode, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sportLeaguePath) || string.IsNullOrWhiteSpace(teamIdOrCode))
                return null;

            try
            {
                var response = await _httpClient.GetAsync(
                    $"apis/site/v2/sports/{sportLeaguePath}/teams/{teamIdOrCode}",
                    cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN team-details API returned {Status} for {Path}/{Team}",
                        response.StatusCode, sportLeaguePath, teamIdOrCode);
                    return null;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("team", out var team) || team.ValueKind != JsonValueKind.Object)
                    return null;

                var details = new EspnTeamDetails
                {
                    DisplayName = GetString(team, "displayName"),
                    Location = GetString(team, "location"),
                    Nickname = GetString(team, "nickname"),
                    Color = GetString(team, "color")
                };

                // Venue (may be nested with an address).
                if (team.TryGetProperty("franchise", out var franchise) &&
                    franchise.TryGetProperty("venue", out var fvenue))
                {
                    ParseVenue(fvenue, details);
                }
                else if (team.TryGetProperty("venue", out var venue))
                {
                    ParseVenue(venue, details);
                }

                // Record + standing summaries.
                if (team.TryGetProperty("record", out var record) &&
                    record.TryGetProperty("items", out var items) &&
                    items.ValueKind == JsonValueKind.Array &&
                    items.GetArrayLength() > 0)
                {
                    var first = items[0];
                    details.RecordSummary = GetString(first, "summary");
                }

                if (team.TryGetProperty("standingSummary", out var standingEl))
                {
                    details.StandingSummary = standingEl.GetString() ?? string.Empty;
                }

                return details;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch ESPN team details for {Path}/{Team}", sportLeaguePath, teamIdOrCode);
                return null;
            }
        }

        private static void ParseVenue(JsonElement venue, EspnTeamDetails details)
        {
            if (venue.ValueKind != JsonValueKind.Object) return;

            details.Venue = GetString(venue, "fullName");

            if (venue.TryGetProperty("address", out var address) && address.ValueKind == JsonValueKind.Object)
            {
                var city = GetString(address, "city");
                var state = GetString(address, "state");
                details.VenueCity = (city, state) switch
                {
                    (not "", not "") => $"{city}, {state}",
                    (not "", "") => city,
                    _ => state
                };
            }
        }

        private static string GetString(JsonElement element, string property) =>
            element.TryGetProperty(property, out var prop) && prop.ValueKind == JsonValueKind.String
                ? prop.GetString() ?? string.Empty
                : string.Empty;

        /// <summary>
        /// Fetches recent news headlines from the ESPN Site API. <paramref name="sportLeaguePath"/>
        /// is the ESPN segment such as "football/college-football". When
        /// <paramref name="teamId"/> is supplied, results are filtered to that team.
        /// Returns an empty list if the request fails.
        /// </summary>
        public async Task<List<string>> GetNewsHeadlinesAsync(string sportLeaguePath, string? teamId = null, int count = 5, CancellationToken cancellationToken = default)
        {
            var headlines = new List<string>();
            if (string.IsNullOrWhiteSpace(sportLeaguePath))
                return headlines;

            try
            {
                var url = $"apis/site/v2/sports/{sportLeaguePath}/news?limit={Math.Max(1, count)}";
                if (!string.IsNullOrWhiteSpace(teamId))
                    url += $"&team={teamId}";

                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN news API returned {Status} for {Path} (team {Team})",
                        response.StatusCode, sportLeaguePath, teamId ?? "all");
                    return headlines;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (doc.RootElement.TryGetProperty("articles", out var articles) &&
                    articles.ValueKind == JsonValueKind.Array)
                {
                    foreach (var article in articles.EnumerateArray())
                    {
                        var headline = GetString(article, "headline");
                        if (!string.IsNullOrWhiteSpace(headline))
                            headlines.Add(headline);
                        if (headlines.Count >= count)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch ESPN news for {Path} (team {Team})", sportLeaguePath, teamId ?? "all");
            }

            return headlines;
        }

        /// <summary>
        /// Fetches a team's current-season schedule (games and results) from the ESPN Site API.
        /// <paramref name="sportLeaguePath"/> is the ESPN segment such as "football/college-football"
        /// or "football/nfl". <paramref name="teamIdOrCode"/> is the ESPN numeric id or abbreviation.
        /// Returns an empty list if the request fails.
        /// </summary>
        public async Task<List<EspnScheduleEntry>> GetTeamScheduleAsync(string sportLeaguePath, string teamIdOrCode, CancellationToken cancellationToken = default)
        {
            var entries = new List<EspnScheduleEntry>();
            if (string.IsNullOrWhiteSpace(sportLeaguePath) || string.IsNullOrWhiteSpace(teamIdOrCode))
                return entries;

            try
            {
                var url = $"https://site.api.espn.com/apis/site/v2/sports/{sportLeaguePath}/teams/{teamIdOrCode}/schedule";
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ESPN schedule API returned {Status} for {Path}/{Team}",
                        response.StatusCode, sportLeaguePath, teamIdOrCode);
                    return entries;
                }

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                if (!doc.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
                    return entries;

                foreach (var ev in events.EnumerateArray())
                {
                    var entry = ParseScheduleEvent(ev, teamIdOrCode);
                    if (entry != null)
                        entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch ESPN schedule for {Path}/{Team}", sportLeaguePath, teamIdOrCode);
            }

            return entries;
        }

        private static EspnScheduleEntry? ParseScheduleEvent(JsonElement ev, string teamIdOrCode)
        {
            if (ev.ValueKind != JsonValueKind.Object)
                return null;

            if (!ev.TryGetProperty("competitions", out var competitions) ||
                competitions.ValueKind != JsonValueKind.Array ||
                competitions.GetArrayLength() == 0)
                return null;

            var comp = competitions[0];
            if (!comp.TryGetProperty("competitors", out var competitors) ||
                competitors.ValueKind != JsonValueKind.Array)
                return null;

            var entry = new EspnScheduleEntry();

            if (ev.TryGetProperty("date", out var dateEl) &&
                dateEl.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(dateEl.GetString(), out var parsedDate))
            {
                entry.Date = parsedDate;
            }

            JsonElement teamSide = default, oppSide = default;
            bool foundTeam = false, foundOpp = false;

            foreach (var competitor in competitors.EnumerateArray())
            {
                var isThisTeam = CompetitorMatches(competitor, teamIdOrCode);
                if (isThisTeam && !foundTeam)
                {
                    teamSide = competitor;
                    foundTeam = true;
                }
                else if (!foundOpp)
                {
                    oppSide = competitor;
                    foundOpp = true;
                }
            }

            // Fallback: if we couldn't positively identify our team, treat first as team, second as opponent.
            if (!foundTeam && competitors.GetArrayLength() >= 2)
            {
                teamSide = competitors[0];
                oppSide = competitors[1];
                foundTeam = foundOpp = true;
            }

            if (!foundOpp)
                return null;

            if (teamSide.ValueKind == JsonValueKind.Object &&
                teamSide.TryGetProperty("homeAway", out var homeAway))
            {
                entry.IsHome = string.Equals(homeAway.GetString(), "home", StringComparison.OrdinalIgnoreCase);
            }

            if (oppSide.TryGetProperty("team", out var oppTeam) && oppTeam.ValueKind == JsonValueKind.Object)
            {
                entry.OpponentName = GetString(oppTeam, "displayName");
                if (string.IsNullOrEmpty(entry.OpponentName))
                    entry.OpponentName = GetString(oppTeam, "name");
                entry.OpponentLogo = GetString(oppTeam, "logo");
            }

            // Venue.
            if (comp.TryGetProperty("venue", out var venue) && venue.ValueKind == JsonValueKind.Object)
                entry.Venue = GetString(venue, "fullName");

            // Status / completion.
            if (comp.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Object)
            {
                if (status.TryGetProperty("type", out var statusType) && statusType.ValueKind == JsonValueKind.Object)
                {
                    if (statusType.TryGetProperty("completed", out var completedEl) &&
                        (completedEl.ValueKind == JsonValueKind.True || completedEl.ValueKind == JsonValueKind.False))
                    {
                        entry.IsCompleted = completedEl.GetBoolean();
                    }
                    entry.StatusDetail = GetString(statusType, "shortDetail");
                }
            }

            if (entry.IsCompleted)
                entry.ResultText = BuildResultText(teamSide, oppSide);

            return entry;
        }

        private static bool CompetitorMatches(JsonElement competitor, string teamIdOrCode)
        {
            if (competitor.ValueKind != JsonValueKind.Object)
                return false;

            if (competitor.TryGetProperty("id", out var idEl) &&
                string.Equals(idEl.GetString(), teamIdOrCode, StringComparison.OrdinalIgnoreCase))
                return true;

            if (competitor.TryGetProperty("team", out var team) && team.ValueKind == JsonValueKind.Object)
            {
                if (team.TryGetProperty("id", out var teamId) &&
                    string.Equals(teamId.GetString(), teamIdOrCode, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(GetString(team, "abbreviation"), teamIdOrCode, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static string BuildResultText(JsonElement teamSide, JsonElement oppSide)
        {
            var teamScore = GetScore(teamSide);
            var oppScore = GetScore(oppSide);
            if (teamScore == null || oppScore == null)
                return string.Empty;

            string outcome;
            if (teamScore > oppScore) outcome = "W";
            else if (teamScore < oppScore) outcome = "L";
            else outcome = "T";

            return $"{outcome} {teamScore}-{oppScore}";
        }

        private static int? GetScore(JsonElement competitor)
        {
            if (competitor.ValueKind != JsonValueKind.Object ||
                !competitor.TryGetProperty("score", out var score))
                return null;

            // Score can be an object { value, displayValue } or a plain string/number.
            if (score.ValueKind == JsonValueKind.Object)
            {
                if (score.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.Number)
                    return (int)val.GetDouble();
                if (score.TryGetProperty("displayValue", out var disp) &&
                    int.TryParse(disp.GetString(), out var dispVal))
                    return dispVal;
                return null;
            }

            if (score.ValueKind == JsonValueKind.Number)
                return (int)score.GetDouble();

            if (score.ValueKind == JsonValueKind.String && int.TryParse(score.GetString(), out var strVal))
                return strVal;

            return null;
        }
    }
}

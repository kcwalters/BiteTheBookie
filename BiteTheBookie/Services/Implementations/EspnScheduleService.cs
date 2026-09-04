using System.Text.Json;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Retrieves a league schedule for a specific date from the public ESPN scoreboard
    /// API (<c>?dates=YYYYMMDD</c>). Provides accurate teams, start times, venues, and
    /// live/final status so the Scores &amp; Simulations page can default to the current
    /// day and let users browse other dates — for every supported sport.
    /// </summary>
    public class EspnScheduleService : ILeagueScheduleService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnScheduleService> _logger;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        // Maps our league codes to ESPN scoreboard endpoints.
        private static readonly Dictionary<string, string> ScoreboardPaths =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["NBA"] = "apis/site/v2/sports/basketball/nba/scoreboard",
            ["NFL"] = "apis/site/v2/sports/football/nfl/scoreboard",
            ["NHL"] = "apis/site/v2/sports/hockey/nhl/scoreboard",
            ["MLB"] = "apis/site/v2/sports/baseball/mlb/scoreboard",
            ["CFB"] = "apis/site/v2/sports/football/college-football/scoreboard",
            ["CBB"] = "apis/site/v2/sports/basketball/mens-college-basketball/scoreboard",
        };

        public EspnScheduleService(HttpClient httpClient, ILogger<EspnScheduleService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            // Use the "web" API host — site.api.espn.com returns 403 for server-side
            // requests, while site.web.api.espn.com serves the identical JSON unblocked.
            _httpClient.BaseAddress ??= new Uri("https://site.web.api.espn.com/");
        }

        public bool IsSupported(string league) =>
            !string.IsNullOrWhiteSpace(league) && ScoreboardPaths.ContainsKey(league);

        public async Task<List<NBAGameMatchup>> GetGamesForDateAsync(string league, DateTime date, CancellationToken cancellationToken = default)
        {
            if (!ScoreboardPaths.TryGetValue(league ?? string.Empty, out var path))
            {
                _logger.LogWarning("Unsupported league '{League}' requested for schedule", league);
                return new List<NBAGameMatchup>();
            }

            var dateKey = date.ToString("yyyyMMdd");
            var cacheKey = $"schedule:{league}:{dateKey}";

            if (_cache.TryGetValue(cacheKey, out List<NBAGameMatchup>? cached) && cached is not null)
            {
                return cached;
            }

            var games = new List<NBAGameMatchup>();

            try
            {
                var url = $"{path}?dates={dateKey}";
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (root.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                {
                    foreach (var ev in events.EnumerateArray())
                    {
                        var game = MapEvent(ev);
                        if (game is not null)
                            games.Add(game);
                    }
                }

                games = games.OrderBy(g => g.GameTime).ToList();
                _logger.LogInformation("ESPN {League} schedule for {Date}: {Count} games", league, dateKey, games.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch {League} schedule for {Date}", league, dateKey);
            }

            _cache.Set(cacheKey, games, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            return games;
        }

        public async Task<List<NBAGameMatchup>> GetNflGamesByWeekAsync(CancellationToken cancellationToken = default)
        {
            const string path = "apis/site/v2/sports/football/nfl/scoreboard";
            const int regularSeasonWeeks = 18;

            var result = new List<NBAGameMatchup>();

            // Determine the current NFL season year from the default scoreboard payload.
            var seasonYear = DateTime.Today.Year;
            try
            {
                using var seasonResponse = await _httpClient.GetAsync(path, cancellationToken);
                seasonResponse.EnsureSuccessStatusCode();
                await using var seasonStream = await seasonResponse.Content.ReadAsStreamAsync(cancellationToken);
                using var seasonDoc = await JsonDocument.ParseAsync(seasonStream, cancellationToken: cancellationToken);
                if (seasonDoc.RootElement.TryGetProperty("season", out var seasonEl) &&
                    seasonEl.TryGetProperty("year", out var yearEl) &&
                    yearEl.TryGetInt32(out var year))
                {
                    seasonYear = year;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not determine current NFL season year; defaulting to {Year}", seasonYear);
            }

            for (var week = 1; week <= regularSeasonWeeks; week++)
            {
                var cacheKey = $"nflweek:{seasonYear}:{week}";
                if (_cache.TryGetValue(cacheKey, out List<NBAGameMatchup>? cached) && cached is not null)
                {
                    result.AddRange(cached);
                    continue;
                }

                var weekGames = new List<NBAGameMatchup>();

                try
                {
                    var url = $"{path}?dates={seasonYear}&seasontype=2&week={week}";
                    using var response = await _httpClient.GetAsync(url, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ev in events.EnumerateArray())
                        {
                            var game = MapEvent(ev);
                            if (game is not null)
                            {
                                game.Week = week;
                                weekGames.Add(game);
                            }
                        }
                    }

                    weekGames = weekGames.OrderBy(g => g.GameTime).ToList();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch NFL schedule for {Season} week {Week}", seasonYear, week);
                }

                // Per-week schedules rarely change, so cache them longer than the daily feed.
                _cache.Set(cacheKey, weekGames, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
                });

                result.AddRange(weekGames);
            }

            return result;
        }

        private static NBAGameMatchup? MapEvent(JsonElement ev)
        {
            if (!ev.TryGetProperty("competitions", out var comps) ||
                comps.ValueKind != JsonValueKind.Array || comps.GetArrayLength() == 0)
            {
                return null;
            }

            var comp = comps[0];

            DateTime gameTime = ev.TryGetProperty("date", out var dateEl) &&
                                dateEl.TryGetDateTime(out var parsed)
                ? parsed.ToUniversalTime()
                : DateTime.UtcNow;

            // ?? Status ??????????????????????????????????????????????????????????
            string state = "pre";
            string statusDetail = string.Empty;
            if (comp.TryGetProperty("status", out var statusEl) &&
                statusEl.TryGetProperty("type", out var typeEl))
            {
                state = typeEl.TryGetProperty("state", out var stateEl) ? stateEl.GetString() ?? "pre" : "pre";
                statusDetail = typeEl.TryGetProperty("shortDetail", out var sdEl) ? sdEl.GetString() ?? string.Empty : string.Empty;
            }

            var status = state switch
            {
                "in" => "Live",
                "post" => "Final",
                _ => "Scheduled"
            };

            // ?? Venue ???????????????????????????????????????????????????????????
            string venue = string.Empty;
            if (comp.TryGetProperty("venue", out var venueEl))
            {
                var name = venueEl.TryGetProperty("fullName", out var vn) ? vn.GetString() : null;
                string? city = null;
                if (venueEl.TryGetProperty("address", out var addr) &&
                    addr.TryGetProperty("city", out var cityEl))
                {
                    city = cityEl.GetString();
                }

                venue = (name, city) switch
                {
                    (not null, not null) => $"{name}, {city}",
                    (not null, null) => name!,
                    _ => string.Empty
                };
            }

            // ?? Teams / scores ??????????????????????????????????????????????????
            string awayCode = "", awayName = "", awayLogo = "";
            string homeCode = "", homeName = "", homeLogo = "";
            int? awayScore = null, homeScore = null;

            if (comp.TryGetProperty("competitors", out var competitors) &&
                competitors.ValueKind == JsonValueKind.Array)
            {
                foreach (var c in competitors.EnumerateArray())
                {
                    var homeAway = c.TryGetProperty("homeAway", out var haEl) ? haEl.GetString() : null;
                    var team = c.TryGetProperty("team", out var teamEl) ? teamEl : default;

                    var abbr = team.ValueKind == JsonValueKind.Object &&
                               team.TryGetProperty("abbreviation", out var abEl) ? abEl.GetString() ?? "" : "";
                    var displayName = team.ValueKind == JsonValueKind.Object &&
                               team.TryGetProperty("displayName", out var dnEl) ? dnEl.GetString() ?? abbr : abbr;
                    var logo = team.ValueKind == JsonValueKind.Object &&
                               team.TryGetProperty("logo", out var logoEl) ? logoEl.GetString() ?? "" : "";

                    int? score = null;
                    if (c.TryGetProperty("score", out var scoreEl) &&
                        scoreEl.ValueKind == JsonValueKind.String &&
                        int.TryParse(scoreEl.GetString(), out var s))
                    {
                        score = s;
                    }

                    if (homeAway == "away")
                    {
                        awayCode = abbr; awayName = displayName; awayLogo = logo; awayScore = score;
                    }
                    else
                    {
                        homeCode = abbr; homeName = displayName; homeLogo = logo; homeScore = score;
                    }
                }
            }

            if (string.IsNullOrEmpty(homeCode) || string.IsNullOrEmpty(awayCode))
                return null;

            return new NBAGameMatchup
            {
                // GameId format matches the existing simulation Detail route parser (away-home codes).
                GameId = $"{awayCode.ToLower()}-{homeCode.ToLower()}-{gameTime:yyyyMMdd}",
                AwayTeamCode = awayCode,
                AwayTeamName = awayName,
                AwayTeamLogo = awayLogo,
                HomeTeamCode = homeCode,
                HomeTeamName = homeName,
                HomeTeamLogo = homeLogo,
                GameTime = gameTime,
                Venue = venue,
                Status = status,
                StatusDetail = statusDetail,
                AwayScore = awayScore,
                HomeScore = homeScore
            };
        }
    }
}

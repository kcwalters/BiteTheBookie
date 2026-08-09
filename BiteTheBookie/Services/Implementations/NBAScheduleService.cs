using System.Text.Json;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Retrieves the NBA schedule for a specific date from the public ESPN scoreboard
    /// API (<c>?dates=YYYYMMDD</c>). Provides accurate teams, start times, venues, and
    /// live/final status so the Scores &amp; Simulations page can default to the current
    /// day and let users browse other dates.
    /// </summary>
    public class NBAScheduleService : INBAScheduleService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NBAScheduleService> _logger;
        private readonly IMemoryCache _cache;

        private const string ScoreboardUrl =
            "https://site.web.api.espn.com/apis/site/v2/sports/basketball/nba/scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NBAScheduleService(HttpClient httpClient, ILogger<NBAScheduleService> logger, IMemoryCache cache)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
        }

        public async Task<List<NBAGameMatchup>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var dateKey = date.ToString("yyyyMMdd");
            var cacheKey = $"nba:schedule:{dateKey}";

            if (_cache.TryGetValue(cacheKey, out List<NBAGameMatchup>? cached) && cached is not null)
            {
                return cached;
            }

            var games = new List<NBAGameMatchup>();

            try
            {
                var url = $"{ScoreboardUrl}?dates={dateKey}";
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
                _logger.LogInformation("ESPN NBA schedule for {Date}: {Count} games", dateKey, games.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch NBA schedule for {Date}", dateKey);
            }

            _cache.Set(cacheKey, games, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheTtl
            });

            return games;
        }

        private static NBAGameMatchup? MapEvent(JsonElement ev)
        {
            if (!ev.TryGetProperty("competitions", out var comps) ||
                comps.ValueKind != JsonValueKind.Array || comps.GetArrayLength() == 0)
            {
                return null;
            }

            var comp = comps[0];

            var eventId = ev.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;

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

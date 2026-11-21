using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    public class NCAAScoresService : INCAAScoresService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NCAAScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _scoreboardUrl;

        // ESPN: Men’s college basketball scoreboard
        private const string DefaultScoreboardUrl =
            "https://site.web.api.espn.com/apis/site/v2/sports/basketball/mens-college-basketball/scoreboard";

        private const string CacheKey = "ncaa:mbb:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NCAAScoresService(
            HttpClient httpClient,
            ILogger<NCAAScoresService> logger,
            IMemoryCache cache,
            IOptions<SportsTickerOptions> options)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;

            var cfg = options.Value;
            _scoreboardUrl = string.IsNullOrWhiteSpace(cfg.NcaaMensBasketballApiBaseUrl)
                ? DefaultScoreboardUrl
                : cfg.NcaaMensBasketballApiBaseUrl.TrimEnd('/');
        }

        public async Task<IReadOnlyList<NCAATickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NCAATickerView>? cached) && cached is not null)
            {
                _logger.LogDebug("Returning NCAA MBB scores from cache.");
                return cached;
            }

            try
            {
                // Use the US Eastern calendar day for "today". The scoreboard rolls over at
                // midnight ET (ESPN's schedule day), not UTC midnight, so computing the date in
                // UTC would incorrectly show the next day's slate during evening games.
                var easternToday = EasternNow().Date;

                // Fetch only today's games (no fall-forward) so the CBB top scoreboard always
                // reflects the current day. groups=50 restricts to the full Division I slate
                // (ESPN otherwise returns only a small featured subset).
                var events = await EspnScoreboardFetcher.GetEventsForExactDateAsync(
                    _httpClient, _scoreboardUrl, easternToday, cancellationToken, "groups=50&limit=300");

                if (events.Count == 0)
                {
                    _logger.LogInformation("ESPN NCAA MBB scoreboard: no games today.");
                    var empty = Array.Empty<NCAATickerView>();
                    _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                    return empty;
                }

                var games = new List<NCAATickerView>();
                foreach (var ev in events)
                {
                    var game = MapEventToTickerGame(ev);
                    if (game is not null)
                    {
                        games.Add(game.Value);
                    }
                }

                // Live games first, then upcoming (in original start-time order), then finals.
                // OrderBy is stable, so the ESPN date ordering is preserved within each group.
                var ordered = games
                    .OrderBy(g => g.IsLive ? 0 : g.IsFinal ? 2 : 1)
                    .ToList();

                var result = (IReadOnlyList<NCAATickerView>)ordered;
                _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NCAA MBB scoreboard from ESPN.");
                var empty = Array.Empty<NCAATickerView>();
                _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return empty;
            }
        }

        public async Task<IReadOnlyList<NCAATickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                // Fetch the exact requested date with the full Division I slate (groups=50).
                // GetEventsForExactDateAsync uses the correct ESPN "dates=yyyyMMdd" parameter,
                // so upcoming days (e.g. tomorrow's schedule) populate correctly.
                var events = await EspnScoreboardFetcher.GetEventsForExactDateAsync(
                    _httpClient, _scoreboardUrl, date, cancellationToken, "groups=50&limit=300");

                return events.Select(MapEventToTickerGame)
                             .Where(game => game is not null)
                             .Select(game => game.Value)
                             .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NCAA MBB scoreboard for date {Date}", date);
                return Array.Empty<NCAATickerView>();
            }
        }

        private static NCAATickerView? MapEventToTickerGame(JsonElement ev)
        {
            if (!ev.TryGetProperty("competitions", out var compsElement) ||
                compsElement.ValueKind != JsonValueKind.Array ||
                compsElement.GetArrayLength() == 0)
            {
                return null;
            }

            var comp = compsElement[0];

            string state = "";
            string shortDetail = "";
            if (comp.TryGetProperty("status", out var statusElement) &&
                statusElement.TryGetProperty("type", out var typeElement))
            {
                state = typeElement.GetProperty("state").GetString() ?? "";
                shortDetail = typeElement.GetProperty("shortDetail").GetString() ?? "";
            }

            string awayTeam = "";
            string homeTeam = "";
            int? awayScore = null;
            int? homeScore = null;
            string awayLogo = "";
            string homeLogo = "";
            string awayConf = "";
            string homeConf = "";

            if (comp.TryGetProperty("competitors", out var competitorsElement) &&
                competitorsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var teamElement in competitorsElement.EnumerateArray())
                {
                    var homeAway = teamElement.GetProperty("homeAway").GetString();
                    var teamNode = teamElement.GetProperty("team");

                    // For NCAA, abbreviation may be missing or not meaningful; fall back to shortDisplayName
                    var teamAbbr = teamNode.TryGetProperty("abbreviation", out var abbrEl)
                        ? (abbrEl.GetString() ?? "")
                        : "";

                    var displayName = teamNode.TryGetProperty("shortDisplayName", out var nameEl)
                        ? (nameEl.GetString() ?? "")
                        : "";

                    var teamName = !string.IsNullOrWhiteSpace(teamAbbr) ? teamAbbr : displayName;
                    var logoUrl = GetLogoUrl(teamNode);
                    var conference = ConferenceAbbr(teamNode);

                    int? score = null;
                    if (teamElement.TryGetProperty("score", out var scoreElement) &&
                        scoreElement.ValueKind == JsonValueKind.String &&
                        int.TryParse(scoreElement.GetString(), out var parsedScore))
                    {
                        score = parsedScore;
                    }

                    if (homeAway == "away")
                    {
                        awayTeam = teamName;
                        awayScore = score;
                        awayLogo = logoUrl;
                        awayConf = conference;
                    }
                    else
                    {
                        homeTeam = teamName;
                        homeScore = score;
                        homeLogo = logoUrl;
                        homeConf = conference;
                    }
                }
            }

            string? eventId = ev.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : null;

            bool isLive = state == "in";
            bool isFinal = state == "post";

            // For scheduled games, show the tip-off time in the venue's local timezone with a
            // short zone label (e.g. "7:00 PM ET"). Live/final games keep ESPN's short detail
            // ("7:41 - 1st", "Final") which does not include a start time.
            string statusText;
            if (state == "pre")
            {
                var venueTz = ResolveVenueTimeZone(comp);
                if (ev.TryGetProperty("date", out var dateEl) && dateEl.TryGetDateTime(out var utcDate))
                {
                    var local = TimeZoneInfo.ConvertTimeFromUtc(utcDate.ToUniversalTime(), venueTz);
                    statusText = $"{local:h:mm tt} {ZoneLabel(venueTz, local)}";
                }
                else
                {
                    statusText = "Scheduled";
                }
            }
            else
            {
                statusText = !string.IsNullOrWhiteSpace(shortDetail)
                    ? shortDetail
                    : state switch
                    {
                        "in" => "Live",
                        "post" => "Final",
                        _ => state
                    };
            }

            return new NCAATickerView(
                awayTeam,
                homeTeam,
                awayScore,
                homeScore,
                awayLogo,
                homeLogo,
                statusText,
                isLive,
                isFinal,
                eventId,
                CombineConferences(awayConf, homeConf)
            );
        }

        private static DateTime EasternNow()
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZone("America/New_York", "Eastern Standard Time"));

        /// <summary>
        /// Resolves the timezone of the game's venue from the competition's venue address state.
        /// Falls back to US Eastern when the state is missing or unmapped.
        /// </summary>
        private static TimeZoneInfo ResolveVenueTimeZone(JsonElement comp)
        {
            string? stateCode = null;
            if (comp.TryGetProperty("venue", out var venue) &&
                venue.TryGetProperty("address", out var address) &&
                address.TryGetProperty("state", out var stateEl))
            {
                stateCode = stateEl.GetString();
            }

            if (!string.IsNullOrWhiteSpace(stateCode) &&
                StateTimeZones.TryGetValue(stateCode.Trim().ToUpperInvariant(), out var ids))
            {
                return GetTimeZone(ids.Iana, ids.Windows);
            }

            return GetTimeZone("America/New_York", "Eastern Standard Time");
        }

        /// <summary>
        /// Short, human-friendly zone label (e.g. "ET", "CT") honoring daylight saving at the
        /// supplied local time.
        /// </summary>
        private static string ZoneLabel(TimeZoneInfo tz, DateTime local)
        {
            var dst = tz.IsDaylightSavingTime(local);
            return tz.Id switch
            {
                "America/New_York" or "Eastern Standard Time" => dst ? "EDT" : "EST",
                "America/Chicago" or "Central Standard Time" => dst ? "CDT" : "CST",
                "America/Denver" or "Mountain Standard Time" => dst ? "MDT" : "MST",
                "America/Phoenix" or "US Mountain Standard Time" => "MST",
                "America/Los_Angeles" or "Pacific Standard Time" => dst ? "PDT" : "PST",
                "America/Anchorage" or "Alaskan Standard Time" => dst ? "AKDT" : "AKST",
                "Pacific/Honolulu" or "Hawaiian Standard Time" => "HST",
                _ => dst ? "EDT" : "EST"
            };
        }

        private static TimeZoneInfo GetTimeZone(string ianaId, string windowsId)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(ianaId); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(windowsId); }
                catch { return TimeZoneInfo.Utc; }
            }
        }

        // US state/territory code -> representative timezone (IANA + Windows fallback).
        private static readonly Dictionary<string, (string Iana, string Windows)> StateTimeZones = new()
        {
            // Eastern
            ["CT"] = ("America/New_York", "Eastern Standard Time"),
            ["DE"] = ("America/New_York", "Eastern Standard Time"),
            ["DC"] = ("America/New_York", "Eastern Standard Time"),
            ["FL"] = ("America/New_York", "Eastern Standard Time"),
            ["GA"] = ("America/New_York", "Eastern Standard Time"),
            ["IN"] = ("America/New_York", "Eastern Standard Time"),
            ["KY"] = ("America/New_York", "Eastern Standard Time"),
            ["ME"] = ("America/New_York", "Eastern Standard Time"),
            ["MD"] = ("America/New_York", "Eastern Standard Time"),
            ["MA"] = ("America/New_York", "Eastern Standard Time"),
            ["MI"] = ("America/New_York", "Eastern Standard Time"),
            ["NH"] = ("America/New_York", "Eastern Standard Time"),
            ["NJ"] = ("America/New_York", "Eastern Standard Time"),
            ["NY"] = ("America/New_York", "Eastern Standard Time"),
            ["NC"] = ("America/New_York", "Eastern Standard Time"),
            ["OH"] = ("America/New_York", "Eastern Standard Time"),
            ["PA"] = ("America/New_York", "Eastern Standard Time"),
            ["RI"] = ("America/New_York", "Eastern Standard Time"),
            ["SC"] = ("America/New_York", "Eastern Standard Time"),
            ["VT"] = ("America/New_York", "Eastern Standard Time"),
            ["VA"] = ("America/New_York", "Eastern Standard Time"),
            ["WV"] = ("America/New_York", "Eastern Standard Time"),
            // Central
            ["AL"] = ("America/Chicago", "Central Standard Time"),
            ["AR"] = ("America/Chicago", "Central Standard Time"),
            ["IL"] = ("America/Chicago", "Central Standard Time"),
            ["IA"] = ("America/Chicago", "Central Standard Time"),
            ["KS"] = ("America/Chicago", "Central Standard Time"),
            ["LA"] = ("America/Chicago", "Central Standard Time"),
            ["MN"] = ("America/Chicago", "Central Standard Time"),
            ["MS"] = ("America/Chicago", "Central Standard Time"),
            ["MO"] = ("America/Chicago", "Central Standard Time"),
            ["NE"] = ("America/Chicago", "Central Standard Time"),
            ["ND"] = ("America/Chicago", "Central Standard Time"),
            ["OK"] = ("America/Chicago", "Central Standard Time"),
            ["SD"] = ("America/Chicago", "Central Standard Time"),
            ["TN"] = ("America/Chicago", "Central Standard Time"),
            ["TX"] = ("America/Chicago", "Central Standard Time"),
            ["WI"] = ("America/Chicago", "Central Standard Time"),
            // Mountain
            ["CO"] = ("America/Denver", "Mountain Standard Time"),
            ["ID"] = ("America/Denver", "Mountain Standard Time"),
            ["MT"] = ("America/Denver", "Mountain Standard Time"),
            ["NM"] = ("America/Denver", "Mountain Standard Time"),
            ["UT"] = ("America/Denver", "Mountain Standard Time"),
            ["WY"] = ("America/Denver", "Mountain Standard Time"),
            // Arizona (no DST)
            ["AZ"] = ("America/Phoenix", "US Mountain Standard Time"),
            // Pacific
            ["CA"] = ("America/Los_Angeles", "Pacific Standard Time"),
            ["NV"] = ("America/Los_Angeles", "Pacific Standard Time"),
            ["OR"] = ("America/Los_Angeles", "Pacific Standard Time"),
            ["WA"] = ("America/Los_Angeles", "Pacific Standard Time"),
            // Alaska / Hawaii
            ["AK"] = ("America/Anchorage", "Alaskan Standard Time"),
            ["HI"] = ("Pacific/Honolulu", "Hawaiian Standard Time"),
        };

        // Men's college basketball ESPN conference (group) id -> short label.
        private static readonly Dictionary<string, string> MbbConferences = new()
        {
            ["2"] = "ACC",
            ["4"] = "Big East",
            ["7"] = "Big Ten",
            ["8"] = "Big 12",
            ["23"] = "SEC",
            ["21"] = "Pac-12",
            ["46"] = "WCC",
            ["44"] = "Mountain West",
            ["62"] = "American",
            ["6"] = "A-10",
            ["1"] = "America East",
            ["3"] = "ASUN",
            ["9"] = "Big Sky",
            ["10"] = "Big South",
            ["11"] = "Big West",
            ["12"] = "CAA",
            ["13"] = "Conference USA",
            ["14"] = "Horizon",
            ["15"] = "Ivy",
            ["16"] = "MAAC",
            ["17"] = "MAC",
            ["18"] = "MEAC",
            ["19"] = "MVC",
            ["22"] = "NEC",
            ["24"] = "OVC",
            ["25"] = "Patriot",
            ["26"] = "Southern",
            ["27"] = "Southland",
            ["28"] = "SWAC",
            ["29"] = "Summit",
            ["30"] = "Sun Belt",
            ["31"] = "WAC",
        };

        /// <summary>Returns the team's conference short label, or empty when unknown.</summary>
        private static string ConferenceAbbr(JsonElement teamNode)
        {
            var id = ReadConferenceId(teamNode);
            if (string.IsNullOrWhiteSpace(id))
            {
                return "";
            }

            return MbbConferences.TryGetValue(id, out var abbr) ? abbr : $"Conf {id}";
        }

        /// <summary>Reads the ESPN conference id from the various shapes it can appear in.</summary>
        private static string ReadConferenceId(JsonElement teamNode)
        {
            if (teamNode.TryGetProperty("conferenceId", out var confEl))
            {
                var id = confEl.ValueKind == JsonValueKind.String
                    ? confEl.GetString()
                    : confEl.ValueKind == JsonValueKind.Number ? confEl.GetRawText() : null;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id!;
                }
            }

            // Some payloads nest conference metadata under a "conference" object.
            if (teamNode.TryGetProperty("conference", out var confObj) &&
                confObj.ValueKind == JsonValueKind.Object &&
                confObj.TryGetProperty("id", out var nestedId))
            {
                var id = nestedId.ValueKind == JsonValueKind.String
                    ? nestedId.GetString()
                    : nestedId.ValueKind == JsonValueKind.Number ? nestedId.GetRawText() : null;
                if (!string.IsNullOrWhiteSpace(id))
                {
                    return id!;
                }
            }

            return "";
        }

        /// <summary>Space-joined distinct conference labels for a matchup (e.g. "SEC ACC").</summary>
        private static string CombineConferences(string away, string home)
        {
            var set = new List<string>();
            if (!string.IsNullOrWhiteSpace(home)) set.Add(home);
            if (!string.IsNullOrWhiteSpace(away) && !set.Contains(away)) set.Add(away);
            return string.Join(" ", set);
        }

        private static string GetLogoUrl(JsonElement teamNode)
        {
            if (teamNode.TryGetProperty("logos", out var logosElement) &&
                logosElement.ValueKind == JsonValueKind.Array &&
                logosElement.GetArrayLength() > 0)
            {
                var href = logosElement[0].GetProperty("href").GetString();
                if (!string.IsNullOrWhiteSpace(href))
                {
                    return href!;
                }
            }

            if (teamNode.TryGetProperty("logo", out var logoElement) &&
                logoElement.ValueKind == JsonValueKind.String)
            {
                var href = logoElement.GetString();
                if (!string.IsNullOrWhiteSpace(href))
                {
                    return href!;
                }
            }

            // Generic fallback (optional asset)
            return "/img/ncaa/NCAALogoSmall.png";
        }
    }
}

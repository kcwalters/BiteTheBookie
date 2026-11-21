using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    public class CFBScoresService : ICFBScoresService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<CFBScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _scoreboardUrl;

        // ESPN: College football scoreboard
        private const string DefaultScoreboardUrl =
            "https://site.web.api.espn.com/apis/site/v2/sports/football/college-football/scoreboard";

        private const string CacheKey = "ncaa:fbs:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public CFBScoresService(
            HttpClient httpClient,
            ILogger<CFBScoresService> logger,
            IMemoryCache cache,
            IOptions<SportsTickerOptions> options)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;

            var cfg = options.Value;
            _scoreboardUrl = string.IsNullOrWhiteSpace(cfg.NcaaFootballApiBaseUrl)
                ? DefaultScoreboardUrl
                : cfg.NcaaFootballApiBaseUrl.TrimEnd('/');
        }

        public async Task<IReadOnlyList<CFBTickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<CFBTickerView>? cached) && cached is not null)
            {
                _logger.LogDebug("Returning NCAA FBS scores from cache.");
                return cached;
            }

            try
            {
                // Fetch today's games, falling forward to the next scheduled day when the
                // league is off today (off-day or out of season) so the ticker stays populated.
                // groups=80 restricts to the full FBS slate (ESPN otherwise returns only a
                // small featured subset).
                var events = await EspnScoreboardFetcher.GetEarliestDayEventsAsync(
                    _httpClient, _scoreboardUrl, cancellationToken, "groups=80&limit=300");

                if (events.Count == 0)
                {
                    _logger.LogInformation("ESPN NCAA FBS scoreboard: no games today or upcoming.");
                    var empty = Array.Empty<CFBTickerView>();
                    _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                    return empty;
                }

                var games = new List<CFBTickerView>();
                foreach (var ev in events)
                {
                    var game = MapEventToTickerGame(ev);
                    if (game is not null)
                    {
                        games.Add(game.Value);
                    }
                }

                var result = (IReadOnlyList<CFBTickerView>)games;
                _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NCAA FBS scoreboard from ESPN.");
                var empty = Array.Empty<CFBTickerView>();
                _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return empty;
            }
        }

        public async Task<IReadOnlyList<CFBTickerView>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            return Array.Empty<CFBTickerView>();
        }

        private static CFBTickerView? MapEventToTickerGame(JsonElement ev)
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

            var statusText = !string.IsNullOrWhiteSpace(shortDetail)
                ? shortDetail
                : state switch
                {
                    "pre" => "Scheduled",
                    "in" => "Live",
                    "post" => "Final",
                    _ => state
                };

            return new CFBTickerView(
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

        // College football ESPN conference (group) id -> short label.
        private static readonly Dictionary<string, string> FbsConferences = new()
        {
            ["1"] = "ACC",
            ["4"] = "Big 12",
            ["5"] = "Big Ten",
            ["8"] = "SEC",
            ["9"] = "Pac-12",
            ["12"] = "Conference USA",
            ["15"] = "MAC",
            ["17"] = "Mountain West",
            ["18"] = "FBS Indep",
            ["20"] = "FBS Indep",
            ["37"] = "Sun Belt",
            ["151"] = "American",
        };

        /// <summary>Returns the team's conference short label, or empty when unknown.</summary>
        private static string ConferenceAbbr(JsonElement teamNode)
        {
            var id = ReadConferenceId(teamNode);
            if (string.IsNullOrWhiteSpace(id))
            {
                return "";
            }

            return FbsConferences.TryGetValue(id, out var abbr) ? abbr : $"Conf {id}";
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

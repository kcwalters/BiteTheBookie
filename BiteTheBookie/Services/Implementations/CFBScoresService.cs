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
            "https://site.api.espn.com/apis/site/v2/sports/football/college-football/scoreboard";

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
                using var response = await _httpClient.GetAsync(_scoreboardUrl, cancellationToken);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                var root = doc.RootElement;

                if (!root.TryGetProperty("events", out var eventsElement) ||
                    eventsElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("ESPN NCAA FBS scoreboard: 'events' array missing or invalid.");
                    var empty = Array.Empty<CFBTickerView>();
                    _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                    return empty;
                }

                var games = new List<CFBTickerView>();
                foreach (var ev in eventsElement.EnumerateArray())
                {
                    try
                    {
                        var game = MapEventToTickerGame(ev);
                        if (game is not null)
                        {
                            games.Add(game.Value);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Skipping malformed NCAA FBS event while mapping ticker game.");
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

            if (comp.TryGetProperty("competitors", out var competitorsElement) &&
                competitorsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var teamElement in competitorsElement.EnumerateArray())
                {
                    var homeAway = teamElement.TryGetProperty("homeAway", out var homeAwayEl)
                        ? homeAwayEl.GetString()
                        : null;

                    if (!teamElement.TryGetProperty("team", out var teamNode))
                    {
                        continue;
                    }

                    // For NCAA, abbreviation may be missing or not meaningful; fall back to shortDisplayName
                    var teamAbbr = teamNode.TryGetProperty("abbreviation", out var abbrEl)
                        ? (abbrEl.GetString() ?? "")
                        : "";

                    var displayName = teamNode.TryGetProperty("shortDisplayName", out var nameEl)
                        ? (nameEl.GetString() ?? "")
                        : "";

                    var teamName = !string.IsNullOrWhiteSpace(teamAbbr) ? teamAbbr : displayName;
                    var logoUrl = GetLogoUrl(teamNode);

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
                    }
                    else
                    {
                        homeTeam = teamName;
                        homeScore = score;
                        homeLogo = logoUrl;
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
                eventId
            );
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

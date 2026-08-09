using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{

    public class NFLScoresService : INFLScoresService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<NFLScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly string _scoreboardUrl;

        private const string DefaultScoreboardUrl =
            "https://site.web.api.espn.com/apis/site/v2/sports/football/nfl/scoreboard";
        private const string CacheKey = "nfl:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NFLScoresService(HttpClient httpClient, ILogger<NFLScoresService> logger, IMemoryCache cache, IOptions<SportsTickerOptions> options)
        {
            _httpClient = httpClient;
            _logger = logger;
            _cache = cache;
            var cfg = options.Value;
            _scoreboardUrl = string.IsNullOrWhiteSpace(cfg.NflApiBaseUrl) ? DefaultScoreboardUrl : cfg.NflApiBaseUrl.TrimEnd('/');
        }

        public async Task<IReadOnlyList<NFLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NFLTickerView>? cached) && cached is not null)
            {
                _logger.LogDebug("Returning NFL scores from cache.");
                return cached;
            }

            try
            {
                // Fetch today's games, falling forward to the next scheduled day when the
                // league is off today (off-day or out of season) so the ticker stays populated.
                var events = await EspnScoreboardFetcher.GetEarliestDayEventsAsync(
                    _httpClient, _scoreboardUrl, cancellationToken);

                if (events.Count == 0)
                {
                    _logger.LogInformation("ESPN NFL scoreboard: no games today or upcoming.");
                    var empty = Array.Empty<NFLTickerView>();
                    _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                    return empty;
                }

                var games = new List<NFLTickerView>();
                foreach (var ev in events)
                {
                    var game = MapEventToTickerGame(ev);
                    if (game is not null)
                    {
                        games.Add(game.Value);
                    }
                }

                var result = (IReadOnlyList<NFLTickerView>)games;
                _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NFL scoreboard from ESPN.");
                var empty = Array.Empty<NFLTickerView>();
                _cache.Set(CacheKey, empty, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
                return empty;
            }
        }

        private static NFLTickerView? MapEventToTickerGame(JsonElement ev)
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
                    var homeAway = teamElement.GetProperty("homeAway").GetString();
                    var teamNode = teamElement.GetProperty("team");

                    var teamAbbr = teamNode.GetProperty("abbreviation").GetString() ?? "";
                    var logoUrl = GetLogoUrl(teamNode, teamAbbr);

                    int? score = null;
                    if (teamElement.TryGetProperty("score", out var scoreElement) &&
                        scoreElement.ValueKind == JsonValueKind.String &&
                        int.TryParse(scoreElement.GetString(), out var parsedScore))
                    {
                        score = parsedScore;
                    }

                    if (homeAway == "away")
                    {
                        awayTeam = teamAbbr;
                        awayScore = score;
                        awayLogo = logoUrl;
                    }
                    else
                    {
                        homeTeam = teamAbbr;
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

            return new NFLTickerView(
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

        private static string GetLogoUrl(JsonElement teamNode, string teamAbbr)
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

            return $"/img/nfl/{teamAbbr.ToLowerInvariant()}.svg";
        }
    }
}
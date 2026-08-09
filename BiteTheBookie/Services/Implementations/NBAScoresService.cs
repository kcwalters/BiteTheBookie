using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>Provides NBA ticker data from The Odds API (real games and scores).</summary>
    public class NBAScoresService : INBAScoresService
    {
        private readonly ILogger<NBAScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly TheOddsApiClient _oddsApi;

        private const string CacheKey = "nba:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NBAScoresService(ILogger<NBAScoresService> logger, IMemoryCache cache, TheOddsApiClient oddsApi)
        {
            _logger = logger;
            _cache = cache;
            _oddsApi = oddsApi;
        }

        public async Task<IReadOnlyList<NBATickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NBATickerView>? cached) && cached is not null)
            {
                return cached;
            }

            IReadOnlyList<NBATickerView> result;
            try
            {
                var games = await OddsApiScoresFallback.GetGamesAsync(_oddsApi, "NBA", cancellationToken);
                var dated = games
                    .Select(g => ((DateTime?)g.CommenceTime.UtcDateTime, new NBATickerView(
                        g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore,
                        TeamLogoResolver.Resolve("NBA", g.AwayTeam),
                        TeamLogoResolver.Resolve("NBA", g.HomeTeam),
                        OddsApiScoresFallback.StatusText(g),
                        !g.Completed && (g.AwayScore.HasValue || g.HomeScore.HasValue),
                        g.Completed, g.Id)))
                    .ToList();
                result = TickerScheduleHelper.SelectNextGameDay(dated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NBA scores from The Odds API.");
                result = Array.Empty<NBATickerView>();
            }

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
            return result;
        }
    }
}

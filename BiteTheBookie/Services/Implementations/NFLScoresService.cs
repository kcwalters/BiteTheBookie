using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>Provides NFL ticker data from The Odds API (real games and scores).</summary>
    public class NFLScoresService : INFLScoresService
    {
        private readonly ILogger<NFLScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly TheOddsApiClient _oddsApi;

        private const string CacheKey = "nfl:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NFLScoresService(ILogger<NFLScoresService> logger, IMemoryCache cache, TheOddsApiClient oddsApi)
        {
            _logger = logger;
            _cache = cache;
            _oddsApi = oddsApi;
        }

        public async Task<IReadOnlyList<NFLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NFLTickerView>? cached) && cached is not null)
            {
                return cached;
            }

            IReadOnlyList<NFLTickerView> result;
            try
            {
                var games = await OddsApiScoresFallback.GetGamesAsync(_oddsApi, "NFL", cancellationToken);
                var dated = games
                    .Select(g => ((DateTime?)g.CommenceTime.UtcDateTime, new NFLTickerView(
                        g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore,
                        TeamLogoResolver.Resolve("NFL", g.AwayTeam),
                        TeamLogoResolver.Resolve("NFL", g.HomeTeam),
                        OddsApiScoresFallback.StatusText(g),
                        !g.Completed && (g.AwayScore.HasValue || g.HomeScore.HasValue),
                        g.Completed, g.Id)))
                    .ToList();
                result = TickerScheduleHelper.SelectNextGameDay(dated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NFL scores from The Odds API.");
                result = Array.Empty<NFLTickerView>();
            }

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
            return result;
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>Provides NCAA men's basketball ticker data from The Odds API.</summary>
    public class NCAAScoresService : INCAAScoresService
    {
        private readonly ILogger<NCAAScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly TheOddsApiClient _oddsApi;

        private const string CacheKey = "ncaa:mbb:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NCAAScoresService(ILogger<NCAAScoresService> logger, IMemoryCache cache, TheOddsApiClient oddsApi)
        {
            _logger = logger;
            _cache = cache;
            _oddsApi = oddsApi;
        }

        public async Task<IReadOnlyList<NCAATickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NCAATickerView>? cached) && cached is not null)
            {
                return cached;
            }

            IReadOnlyList<NCAATickerView> result;
            try
            {
                var games = await OddsApiScoresFallback.GetGamesAsync(_oddsApi, "CBB", cancellationToken);
                var dated = games
                    .Select(g => ((DateTime?)g.CommenceTime.UtcDateTime, new NCAATickerView(
                        g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore,
                        TeamLogoResolver.Resolve("CBB", g.AwayTeam),
                        TeamLogoResolver.Resolve("CBB", g.HomeTeam),
                        OddsApiScoresFallback.StatusText(g),
                        !g.Completed && (g.AwayScore.HasValue || g.HomeScore.HasValue),
                        g.Completed, g.Id)))
                    .ToList();
                result = TickerScheduleHelper.SelectNextGameDay(dated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NCAA MBB scores from The Odds API.");
                result = Array.Empty<NCAATickerView>();
            }

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
            return result;
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>Provides NHL ticker data from The Odds API (real games and scores).</summary>
    public class NHLScoresService : INHLScoresService
    {
        private readonly ILogger<NHLScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly TheOddsApiClient _oddsApi;

        private const string CacheKey = "nhl:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public NHLScoresService(ILogger<NHLScoresService> logger, IMemoryCache cache, TheOddsApiClient oddsApi)
        {
            _logger = logger;
            _cache = cache;
            _oddsApi = oddsApi;
        }

        public async Task<IReadOnlyList<NHLTickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<NHLTickerView>? cached) && cached is not null)
            {
                return cached;
            }

            IReadOnlyList<NHLTickerView> result;
            try
            {
                var games = await OddsApiScoresFallback.GetGamesAsync(_oddsApi, "NHL", cancellationToken);
                var dated = games
                    .Select(g => ((DateTime?)g.CommenceTime.UtcDateTime, new NHLTickerView(
                        g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore,
                        TeamLogoResolver.Resolve("NHL", g.AwayTeam),
                        TeamLogoResolver.Resolve("NHL", g.HomeTeam),
                        OddsApiScoresFallback.StatusText(g),
                        !g.Completed && (g.AwayScore.HasValue || g.HomeScore.HasValue),
                        g.Completed, g.Id)))
                    .ToList();
                result = TickerScheduleHelper.SelectNextGameDay(dated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NHL scores from The Odds API.");
                result = Array.Empty<NHLTickerView>();
            }

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
            return result;
        }
    }
}

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>Provides NCAA football ticker data from The Odds API.</summary>
    public class CFBScoresService : ICFBScoresService
    {
        private readonly ILogger<CFBScoresService> _logger;
        private readonly IMemoryCache _cache;
        private readonly TheOddsApiClient _oddsApi;

        private const string CacheKey = "ncaa:fbs:scoreboard";
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        public CFBScoresService(ILogger<CFBScoresService> logger, IMemoryCache cache, TheOddsApiClient oddsApi)
        {
            _logger = logger;
            _cache = cache;
            _oddsApi = oddsApi;
        }

        public async Task<IReadOnlyList<CFBTickerView>> GetGamesAsync(CancellationToken cancellationToken = default)
        {
            if (_cache.TryGetValue(CacheKey, out IReadOnlyList<CFBTickerView>? cached) && cached is not null)
            {
                return cached;
            }

            IReadOnlyList<CFBTickerView> result;
            try
            {
                var games = await OddsApiScoresFallback.GetGamesAsync(_oddsApi, "CFB", cancellationToken);
                var dated = games
                    .Select(g => ((DateTime?)g.CommenceTime.UtcDateTime, new CFBTickerView(
                        g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore,
                        TeamLogoResolver.Resolve("CFB", g.AwayTeam),
                        TeamLogoResolver.Resolve("CFB", g.HomeTeam),
                        OddsApiScoresFallback.StatusText(g),
                        !g.Completed && (g.AwayScore.HasValue || g.HomeScore.HasValue),
                        g.Completed, g.Id)))
                    .ToList();
                result = TickerScheduleHelper.SelectNextGameDay(dated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching NCAA FBS scores from The Odds API.");
                result = Array.Empty<CFBTickerView>();
            }

            _cache.Set(CacheKey, result, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl });
            return result;
        }
    }
}

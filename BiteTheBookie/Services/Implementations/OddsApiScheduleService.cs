using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Provides a league schedule for a specific date using The Odds API as the sole
    /// data source. Delegates to the per-league games services (NBA/CFB/CBB) so game IDs
    /// and team codes match what the simulation Detail page expects; builds compatible
    /// IDs directly for NFL/NHL/MLB.
    /// </summary>
    public class OddsApiScheduleService : ILeagueScheduleService
    {
        private readonly TheOddsApiClient _oddsApi;
        private readonly ICFBGamesService _cfbGames;
        private readonly ICBBGamesService _cbbGames;
        private readonly ILogger<OddsApiScheduleService> _logger;

        public OddsApiScheduleService(
            TheOddsApiClient oddsApi,
            ICFBGamesService cfbGames,
            ICBBGamesService cbbGames,
            ILogger<OddsApiScheduleService> logger)
        {
            _oddsApi = oddsApi;
            _cfbGames = cfbGames;
            _cbbGames = cbbGames;
            _logger = logger;
        }

        public bool IsSupported(string league) => OddsApiScoresFallback.GetSportKey(league) is not null;

        public async Task<List<NBAGameMatchup>> GetGamesForDateAsync(string league, DateTime date, CancellationToken cancellationToken = default)
        {
            league = (league ?? string.Empty).ToUpperInvariant();
            if (!IsSupported(league))
            {
                _logger.LogWarning("Unsupported league '{League}' requested for schedule", league);
                return new List<NBAGameMatchup>();
            }

            try
            {
                var games = league switch
                {
                    "CFB" => FilterByDate((await _cfbGames.GetUpcomingCFBGamesAsync(cancellationToken)).Select(ToMatchup).ToList(), date),
                    "CBB" or "NCAA" => FilterByDate((await _cbbGames.GetUpcomingCBBGamesAsync(cancellationToken)).Select(ToMatchup).ToList(), date),
                    _ => await BuildFromOddsScoresAsync(league, date, cancellationToken), // NFL, NBA, NHL, MLB
                };

                _logger.LogInformation("Odds API {League} schedule for {Date}: {Count} games", league, date.ToString("yyyyMMdd"), games.Count);
                return games;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch {League} schedule for {Date}", league, date.ToString("yyyyMMdd"));
                return new List<NBAGameMatchup>();
            }
        }

        // Builds Detail-compatible IDs (awaycode-homecode-yyyyMMdd) for leagues whose logo
        // abbreviations match ESPN/Detail codes (NFL, NHL, MLB).
        private async Task<List<NBAGameMatchup>> BuildFromOddsScoresAsync(string league, DateTime date, CancellationToken cancellationToken)
        {
            // NBA should always show the next scheduled game even when the selected date
            // has none (e.g. off day or out of season), so enable the next-game fallback.
            var fallbackToNext = league.Equals("NBA", StringComparison.OrdinalIgnoreCase);
            var games = await OddsApiScoresFallback.GetScheduleAsync(
                _oddsApi, league, date, cancellationToken, fallbackToNext);
            foreach (var g in games)
            {
                var awayCode = TeamLogoResolver.ResolveCode(league, g.AwayTeamName);
                var homeCode = TeamLogoResolver.ResolveCode(league, g.HomeTeamName);
                g.AwayTeamCode = awayCode;
                g.HomeTeamCode = homeCode;
                if (!string.IsNullOrEmpty(awayCode) && !string.IsNullOrEmpty(homeCode))
                {
                    g.GameId = $"{awayCode.ToLower()}-{homeCode.ToLower()}-{g.GameTime:yyyyMMdd}";
                }
                g.AwayTeamLogo = TeamLogoResolver.Resolve(league, g.AwayTeamName);
                g.HomeTeamLogo = TeamLogoResolver.Resolve(league, g.HomeTeamName);
            }
            return games;
        }

        private static List<NBAGameMatchup> FilterByDate(List<NBAGameMatchup> games, DateTime date)
        {
            var target = date.Date;
            return games.Where(g => TickerScheduleHelper.ToEasternDate(
                new DateTimeOffset(DateTime.SpecifyKind(g.GameTime, DateTimeKind.Utc))) == target).ToList();
        }

        private static NBAGameMatchup ToMatchup(CFBGameMatchup g) => new()
        {
            GameId = g.GameId,
            AwayTeamCode = g.AwayTeamCode, AwayTeamName = g.AwayTeamName, AwayTeamLogo = g.AwayTeamLogo,
            HomeTeamCode = g.HomeTeamCode, HomeTeamName = g.HomeTeamName, HomeTeamLogo = g.HomeTeamLogo,
            GameTime = g.GameTime, Spread = g.Spread, OverUnder = g.OverUnder,
            AwayMoneyline = g.AwayMoneyline, HomeMoneyline = g.HomeMoneyline,
            AwayScore = g.AwayScore, HomeScore = g.HomeScore, Status = g.Status,
        };

        private static NBAGameMatchup ToMatchup(CBBGameMatchup g) => new()
        {
            GameId = g.GameId,
            AwayTeamCode = g.AwayTeamCode, AwayTeamName = g.AwayTeamName, AwayTeamLogo = g.AwayTeamLogo,
            HomeTeamCode = g.HomeTeamCode, HomeTeamName = g.HomeTeamName, HomeTeamLogo = g.HomeTeamLogo,
            GameTime = g.GameTime, Spread = g.Spread, OverUnder = g.OverUnder,
            AwayMoneyline = g.AwayMoneyline, HomeMoneyline = g.HomeMoneyline,
            AwayScore = g.AwayScore, HomeScore = g.HomeScore, Status = g.Status,
        };
    }
}


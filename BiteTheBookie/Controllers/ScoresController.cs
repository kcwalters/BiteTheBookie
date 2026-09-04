using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    public class ScoresController : Controller
    {
        private readonly INFLScoresService _nFlScoresService;
        private readonly INBAScoresService _nBAScoresService;
        private readonly INHLScoresService _nHLScoresService;
        private readonly INCAAScoresService _nCAAScoresService;
        private readonly ICFBScoresService _cFBScoresService;
        private readonly IMLBGamesService _mLBGamesService;

        public ScoresController(
            INFLScoresService nFLScoresService,
            INBAScoresService nBAScoresService,
            INHLScoresService nHLSScoresService,
            INCAAScoresService nCAAScoresService,
            ICFBScoresService cFBScoresService,
            IMLBGamesService mLBGamesService)
        {
            _nFlScoresService = nFLScoresService;
            _nBAScoresService = nBAScoresService;
            _nHLScoresService = nHLSScoresService;
            _nCAAScoresService = nCAAScoresService;
            _cFBScoresService = cFBScoresService;
            _mLBGamesService = mLBGamesService; 
        }

        // Landing: default to NFL scores.
        [HttpGet]
        public Task<IActionResult> Index(CancellationToken cancellationToken = default)
            => League("NFL", cancellationToken);

        // Full per-league scoreboard page (all sports).
        [HttpGet]
        public async Task<IActionResult> League(string league, CancellationToken cancellationToken = default)
        {
            var code = (league ?? "NFL").Trim().ToUpperInvariant();

            var model = new ScoresPageViewModel
            {
                LeagueCode = code,
                LeagueName = LeagueDisplayName(code),
                LeagueLogo = LeagueLogo(code),
                TeamController = TeamController(code),
                OddsController = "Odds",
                OddsAction = code,
                ExpertPicksLeague = code
            };

            try
            {
                model.LastDayGames = await LoadGamesAsync(code, cancellationToken, DateTime.Today.AddDays(-1));
                model.TodayGames = await LoadGamesAsync(code, cancellationToken, DateTime.Today);
                model.NextGameDayGames = await LoadGamesAsync(code, cancellationToken, DateTime.Today.AddDays(1));
            }
            catch
            {
                model.ErrorMessage = $"Live {model.LeagueName} scores are unavailable right now.";
            }

            return View("League", model);
        }

        private async Task<List<NBAGameMatchup>> LoadGamesAsync(string code, CancellationToken cancellationToken, DateTime gameDate)
        {
            switch (code)
            {
                case "NFL":
                    return MapTicker(await _nFlScoresService.GetGamesForDateAsync(gameDate, cancellationToken),
                        g => (g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore, g.AwayLogo, g.HomeLogo, g.StatusText, g.IsLive, g.IsFinal, g.EventId));
                case "NBA":
                    return MapTicker(await _nBAScoresService.GetGamesForDateAsync(gameDate, cancellationToken),
                        g => (g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore, g.AwayLogo, g.HomeLogo, g.StatusText, g.IsLive, g.IsFinal, g.EventId));
                case "NHL":
                    return MapTicker(await _nHLScoresService.GetGamesForDateAsync(gameDate, cancellationToken),
                        g => (g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore, g.AwayLogo, g.HomeLogo, g.StatusText, g.IsLive, g.IsFinal, g.EventId));
                case "CFB":
                    return MapTicker(await _cFBScoresService.GetGamesForDateAsync(gameDate, cancellationToken),
                        g => (g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore, g.AwayLogo, g.HomeLogo, g.StatusText, g.IsLive, g.IsFinal, g.EventId));
                case "CBB":
                    return MapTicker(await _nCAAScoresService.GetGamesForDateAsync(gameDate, cancellationToken),
                        g => (g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore, g.AwayLogo, g.HomeLogo, g.StatusText, g.IsLive, g.IsFinal, g.EventId));
                case "MLB":
                    var mlbGames = await _mLBGamesService.GetGamesForDateAsync(gameDate, cancellationToken);
                    return mlbGames.Select(g => new NBAGameMatchup
                    {
                        AwayTeamName = g.AwayTeam,
                        AwayTeamLogo = g.AwayTeamLogoUrl ?? string.Empty,
                        AwayScore = g.AwayScore,
                        HomeTeamName = g.HomeTeam,
                        HomeTeamLogo = g.HomeTeamLogoUrl ?? string.Empty,
                        HomeScore = g.HomeScore,
                        GameTime = g.GameTime.HasValue ? g.GameTime.Value : default,
                        StatusDetail = g.Status,
                        Status = MapMlbStatus(g.Status)
                    }).ToList();
                default:
                    return MapTicker(await _nFlScoresService.GetGamesForDateAsync(gameDate, cancellationToken),
                        g => (g.AwayTeam, g.HomeTeam, g.AwayScore, g.HomeScore, g.AwayLogo, g.HomeLogo, g.StatusText, g.IsLive, g.IsFinal, g.EventId));
            }
        }

        private static List<NBAGameMatchup> MapTicker<T>(
            IEnumerable<T> games,
            Func<T, (string AwayTeam, string HomeTeam, int? AwayScore, int? HomeScore, string AwayLogo, string HomeLogo, string StatusText, bool IsLive, bool IsFinal, string? EventId)> selector)
        {
            return games.Select(g =>
            {
                var v = selector(g);
                return new NBAGameMatchup
                {
                    GameId = v.EventId ?? string.Empty,
                    AwayTeamName = v.AwayTeam,
                    AwayTeamLogo = v.AwayLogo,
                    AwayScore = v.AwayScore,
                    HomeTeamName = v.HomeTeam,
                    HomeTeamLogo = v.HomeLogo,
                    HomeScore = v.HomeScore,
                    StatusDetail = v.StatusText,
                    Status = v.IsLive ? "Live" : v.IsFinal ? "Final" : "Scheduled"
                };
            }).ToList();
        }

        private static string MapMlbStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "Scheduled";
            if (status.Contains("Final", StringComparison.OrdinalIgnoreCase))
                return "Final";
            if (status.Contains("In Progress", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Live", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Top", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Bottom", StringComparison.OrdinalIgnoreCase)
                || status.Contains("Mid", StringComparison.OrdinalIgnoreCase))
                return "Live";
            return "Scheduled";
        }

        private static string LeagueDisplayName(string code) => code switch
        {
            "CFB" => "College Football",
            "CBB" => "College Basketball",
            _ => code
        };

        private static string LeagueLogo(string code) => code switch
        {
            "NFL" => "https://a.espncdn.com/i/teamlogos/leagues/500/nfl.png",
            "NBA" => "https://a.espncdn.com/i/teamlogos/leagues/500/nba.png",
            "MLB" => "https://a.espncdn.com/i/teamlogos/leagues/500/mlb.png",
            "NHL" => "https://a.espncdn.com/i/teamlogos/leagues/500/nhl.png",
            "CFB" => "/img/NCAAMens_med.png",
            "CBB" => "/img/NCAAMens_med.png",
            _ => "https://a.espncdn.com/i/teamlogos/leagues/500/nfl.png"
        };

        private static string TeamController(string code) => code switch
        {
            "NFL" => "NFL",
            "NBA" => "NBA",
            "MLB" => "MLB",
            "NHL" => "NHL",
            "CFB" => "CollegeFootball",
            "CBB" => "CollegeBasketball",
            _ => "NFL"
        };

        [HttpGet]
        public async Task<IActionResult> NFLTickerInner()
        {
            var games = await _nFlScoresService.GetGamesAsync();
            return PartialView("_NFLTickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> NBATickerInner()
        {
            var games = await _nBAScoresService.GetGamesAsync();
            return PartialView("_NBATickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> NHLTickerInner()
        {
            var games = await _nHLScoresService.GetGamesAsync();
            return PartialView("_NHLTickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> NCAATickerInner()
        {
            var games = await _nCAAScoresService.GetGamesAsync();
            return PartialView("_NCAATickerInner", games);
        }

        [HttpGet]
        public async Task<IActionResult> CFBTickerInner()
        {
            var games = await _cFBScoresService.GetGamesAsync();
            return PartialView("_CFBTickerInner", games);
        }
    }
}


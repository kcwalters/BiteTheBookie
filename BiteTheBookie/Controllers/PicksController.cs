using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    /// <summary>
    /// Game center for scores, schedules, simulations, and expert picks across all
    /// supported leagues. Backs every <c>asp-controller="Picks"</c> link in the app.
    /// </summary>
    public class PicksController : Controller
    {
        private const string NflNewsFeedUrl = "https://www.espn.com/espn/rss/nfl/news";

        private readonly ILeagueScheduleService _scheduleService;
        private readonly INFLScoresService _nflScoresService;
        private readonly ICBBGamesService _cbbGamesService;
        private readonly ICFBGamesService _cfbGamesService;
        private readonly ISpreadAnalysisService _spreadAnalysisService;
        private readonly IGameSimulationService _simulationService;
        private readonly INewsService _newsService;
        private readonly ApplicationDbContext _db;

        public PicksController(
            ILeagueScheduleService scheduleService,
            INFLScoresService nflScoresService,
            ICBBGamesService cbbGamesService,
            ICFBGamesService cfbGamesService,
            ISpreadAnalysisService spreadAnalysisService,
            IGameSimulationService simulationService,
            INewsService newsService,
            ApplicationDbContext db)
        {
            _scheduleService = scheduleService;
            _nflScoresService = nflScoresService;
            _cbbGamesService = cbbGamesService;
            _cfbGamesService = cfbGamesService;
            _spreadAnalysisService = spreadAnalysisService;
            _simulationService = simulationService;
            _newsService = newsService;
            _db = db;
        }

        /// <summary>
        /// Default pick sheet. Shows today's schedule for the requested league (NBA by default).
        /// </summary>
        [HttpGet]
        public Task<IActionResult> Index(string league = "NBA", CancellationToken cancellationToken = default)
            => Schedule(league, null, cancellationToken);

        /// <summary>
        /// Pick sheet for a specific league and date. Powers the date navigation on the Index view.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Schedule(string league = "NBA", DateTime? date = null, CancellationToken cancellationToken = default)
        {
            var code = (league ?? "NBA").Trim().ToUpperInvariant();
            var selectedDate = date?.Date ?? DateTime.Today;

            var model = new PicksIndexViewModel
            {
                League = code,
                SelectedDate = selectedDate
            };

            try
            {
                model.Games = await _scheduleService.GetGamesForDateAsync(code, selectedDate, cancellationToken);
            }
            catch
            {
                model.ErrorMessage = $"Live {model.LeagueDisplayName} schedule is unavailable right now.";
            }

            return View("Index", model);
        }

        /// <summary>
        /// ESPN-style NFL landing page: today's schedule plus the latest NFL headlines.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> NFL(CancellationToken cancellationToken = default)
        {
            var model = new NFLLandingViewModel();

            try
            {
                var games = await _nflScoresService.GetGamesAsync(cancellationToken);
                model.Games = games.Select(g => new NBAGameMatchup
                {
                    GameId = g.EventId ?? string.Empty,
                    AwayTeamName = g.AwayTeam,
                    AwayTeamLogo = g.AwayLogo,
                    AwayScore = g.AwayScore,
                    HomeTeamName = g.HomeTeam,
                    HomeTeamLogo = g.HomeLogo,
                    HomeScore = g.HomeScore,
                    StatusDetail = g.StatusText,
                    Status = g.IsLive ? "Live" : g.IsFinal ? "Final" : "Scheduled"
                }).ToList();
            }
            catch
            {
                model.ErrorMessage = "Live NFL scores are unavailable right now.";
            }

            try
            {
                model.Headlines = (await _newsService.GetLatestNewsAsync(NflNewsFeedUrl, 9)).ToList();
            }
            catch
            {
                // Headlines are optional; the view handles an empty list.
            }

            return View(model);
        }

        /// <summary>NBA pick sheet.</summary>
        [HttpGet]
        public async Task<IActionResult> NBA(CancellationToken cancellationToken = default)
            => View(await BuildLeagueScheduleAsync("NBA", cancellationToken));

        /// <summary>MLB pick sheet.</summary>
        [HttpGet]
        public async Task<IActionResult> MLB(CancellationToken cancellationToken = default)
            => View(await BuildLeagueScheduleAsync("MLB", cancellationToken));

        /// <summary>NHL pick sheet (static placeholder view).</summary>
        [HttpGet]
        public IActionResult NHL() => View();

        /// <summary>College basketball pick sheet.</summary>
        [HttpGet]
        public async Task<IActionResult> CBB(CancellationToken cancellationToken = default)
        {
            var model = new CBBPicksIndexViewModel { League = "CBB" };

            try
            {
                model.Games = await _cbbGamesService.GetUpcomingCBBGamesAsync(cancellationToken);
            }
            catch
            {
                // View handles an empty list.
            }

            return View(model);
        }

        /// <summary>College football pick sheet.</summary>
        [HttpGet]
        public async Task<IActionResult> CFB(CancellationToken cancellationToken = default)
        {
            var model = new CFBPicksIndexViewModel { League = "CFB" };

            try
            {
                model.Games = await _cfbGamesService.GetUpcomingCFBGamesAsync(cancellationToken);
            }
            catch
            {
                // View handles an empty list.
            }

            return View(model);
        }

        /// <summary>
        /// NCAA expert picks entry point linked from the CBB pick sheet.
        /// </summary>
        [HttpGet]
        public IActionResult NCAA() => RedirectToAction("Index", "ExpertPicks", new { league = "CBB" });

        /// <summary>
        /// Contrarian "against the spread" opportunities for the requested league.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AgainstTheSpread(string league = "NBA", CancellationToken cancellationToken = default)
        {
            var code = (league ?? "NBA").Trim().ToUpperInvariant();
            var model = new AgainstTheSpreadViewModel { League = code };

            try
            {
                var games = await _scheduleService.GetGamesForDateAsync(code, DateTime.Today, cancellationToken);
                model.Opportunities = await _spreadAnalysisService.AnalyzeSpreadOpportunitiesAsync(games, cancellationToken);
            }
            catch
            {
                // View handles an empty list.
            }

            return View(model);
        }

        /// <summary>
        /// AI game simulation detail for a single matchup. Team names are resolved from
        /// an existing simulation or expert pick when only the game id is supplied.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Detail(string gameId, string? homeTeam = null, string? awayTeam = null, string? league = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam) || string.IsNullOrWhiteSpace(league))
            {
                var existing = await _db.GameSimulations
                    .Where(g => g.GameId == gameId)
                    .OrderByDescending(g => g.GeneratedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (existing != null)
                {
                    return View(new GameSimulationViewModel
                    {
                        GameId = existing.GameId,
                        HomeTeam = existing.HomeTeamName,
                        AwayTeam = existing.AwayTeamName,
                        League = existing.League,
                        SimulationContent = existing.SimulationContent,
                        SimulationId = existing.Id,
                        IsFromCache = true,
                        CachedAt = existing.GeneratedAt
                    });
                }

                var pick = await _db.ExpertPicks
                    .Where(p => p.GameId == gameId)
                    .OrderByDescending(p => p.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (pick != null)
                {
                    homeTeam = pick.HomeTeamName;
                    awayTeam = pick.AwayTeamName;
                    league = pick.League;
                }
            }

            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam) || string.IsNullOrWhiteSpace(league))
            {
                return View(new GameSimulationViewModel
                {
                    GameId = gameId ?? string.Empty,
                    League = "NBA",
                    ErrorMessage = "We couldn't find that matchup. Please start a simulation from the scoreboard."
                });
            }

            try
            {
                var content = await _simulationService.GenerateGameSimulationAsync(
                    homeTeam: homeTeam,
                    awayTeam: awayTeam,
                    league: league,
                    cancellationToken: cancellationToken);

                return View(new GameSimulationViewModel
                {
                    GameId = gameId ?? string.Empty,
                    HomeTeam = homeTeam,
                    AwayTeam = awayTeam,
                    League = league,
                    SimulationContent = content
                });
            }
            catch
            {
                return View(new GameSimulationViewModel
                {
                    GameId = gameId ?? string.Empty,
                    HomeTeam = homeTeam,
                    AwayTeam = awayTeam,
                    League = league,
                    ErrorMessage = "The game simulation is unavailable right now. Please try again later."
                });
            }
        }

        /// <summary>
        /// Expert picks entered for a single game.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ViewPicks(string gameId, CancellationToken cancellationToken = default)
        {
            var picks = await _db.ExpertPicks
                .Where(p => p.GameId == gameId)
                .OrderByDescending(p => p.Confidence)
                .ToListAsync(cancellationToken);

            var first = picks.FirstOrDefault();

            var model = new GamePicksViewModel
            {
                GameId = gameId ?? string.Empty,
                League = first?.League ?? string.Empty,
                AwayTeamName = first?.AwayTeamName ?? string.Empty,
                HomeTeamName = first?.HomeTeamName ?? string.Empty,
                GameTime = first?.GameTime ?? default,
                Picks = picks.Select(p => new PickDetail
                {
                    PickType = p.PickType,
                    PickSelection = p.PickSelection,
                    Confidence = p.Confidence,
                    Analysis = p.Analysis,
                    EnteredBy = p.EnteredBy,
                    CreatedAt = p.CreatedAt
                }).ToList()
            };

            return View(model);
        }

        private async Task<PicksIndexViewModel> BuildLeagueScheduleAsync(string league, CancellationToken cancellationToken)
        {
            var model = new PicksIndexViewModel
            {
                League = league,
                SelectedDate = DateTime.Today
            };

            try
            {
                model.Games = await _scheduleService.GetGamesForDateAsync(league, DateTime.Today, cancellationToken);
            }
            catch
            {
                model.ErrorMessage = $"Live {model.LeagueDisplayName} schedule is unavailable right now.";
            }

            return model;
        }
    }
}

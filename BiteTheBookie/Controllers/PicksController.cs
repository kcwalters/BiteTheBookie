using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Controllers
{
    public class PicksController : Controller
    {
        private readonly IGameSimulationService _simulationService;
        private readonly INBARosterService _rosterService;
        private readonly INBAGamesService _gamesService;
        private readonly ICBBGamesService _cbbGamesService;
        private readonly ICBBRosterService _cbbRosterService;
        private readonly ISpreadAnalysisService _spreadAnalysisService;
        private readonly IInjuryReportService _injuryReportService;
        private readonly ILogger<PicksController> _logger;
        private readonly IMLBGamesService _mlbService;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PicksController(
            IGameSimulationService simulationService,
            INBARosterService rosterService,
            INBAGamesService gamesService,
            ICBBGamesService cbbGamesService,
            ICBBRosterService cbbRosterService,
            ISpreadAnalysisService spreadAnalysisService,
            IInjuryReportService injuryReportService,
            IMLBGamesService mlbService,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<PicksController> logger)
        {
            _simulationService   = simulationService;
            _rosterService       = rosterService;
            _gamesService        = gamesService;
            _cbbGamesService     = cbbGamesService;
            _cbbRosterService    = cbbRosterService;
            _spreadAnalysisService = spreadAnalysisService;
            _injuryReportService = injuryReportService;
            _mlbService          = mlbService;
            _db                  = db;
            _userManager         = userManager;
            _logger              = logger;
        }

        [Authorize(Policy = "PremiumOnly")]
        public async Task<IActionResult> ViewPicks(string gameId, string league = "NBA")
        {
            var picks = await _db.ExpertPicks
                .Where(p => p.GameId == gameId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Try to find the game info from the first pick, or default
            var firstPick = picks.FirstOrDefault();

            var vm = new GamePicksViewModel
            {
                GameId = gameId,
                League = league,
                AwayTeamName = firstPick?.AwayTeamName ?? "Away",
                HomeTeamName = firstPick?.HomeTeamName ?? "Home",
                GameTime = firstPick?.GameTime ?? DateTime.UtcNow,
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

            return View(vm);
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            try
            {
                // Fetch upcoming NBA games dynamically
                var games = await _gamesService.GetUpcomingNBAGamesAsync(cancellationToken);
                
                var viewModel = new PicksIndexViewModel
                {
                    League = "NBA",
                    Games = games
                };
                                                        
                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error
                _logger?.LogError(ex, "Error loading NBA picks");
                
                // Return empty view with error message
                var viewModel = new PicksIndexViewModel
                {
                    League = "NBA",
                    Games = new List<NBAGameMatchup>(),
                    ErrorMessage = $"Unable to load games: {ex.Message}"
                };
                
                return View(viewModel);
            }
        }

        public async Task<IActionResult> AgainstTheSpread(CancellationToken cancellationToken)
        {
            // Fetch upcoming games
            var games = await _gamesService.GetUpcomingNBAGamesAsync(cancellationToken);
            
            // Analyze for contrarian betting opportunities
            var opportunities = await _spreadAnalysisService.AnalyzeSpreadOpportunitiesAsync(games, cancellationToken);
            
            var viewModel = new AgainstTheSpreadViewModel
            {
                League = "NBA",
                Opportunities = opportunities
            };

            return View(viewModel);
        }

        public async Task<IActionResult> NBA(CancellationToken cancellationToken)
        {
            try
            {
                // Fetch today's NBA games (scheduled, live, and finished)
                var games = await _gamesService.GetUpcomingNBAGamesAsync(cancellationToken);
                
                var viewModel = new PicksIndexViewModel
                {
                    League = "NBA",
                    Games = games
                };
                            
                return View(viewModel);
            }
            catch (Exception ex)
            {
                // Log the error
                _logger?.LogError(ex, "Error loading NBA picks");
                
                // Return empty view with error message
                var viewModel = new PicksIndexViewModel
                {
                    League = "NBA",
                    Games = new List<NBAGameMatchup>(),
                    ErrorMessage = $"Unable to load games: {ex.Message}"
                };
                
                return View(viewModel);
            }
        }

        public IActionResult NFL()
        {
            return View();
        }

        public IActionResult NHL()
        {
            return View();
        }

        public async Task<IActionResult> CBB(CancellationToken cancellationToken)
        {
            // Fetch upcoming CBB games dynamically
            var games = await _cbbGamesService.GetUpcomingCBBGamesAsync(cancellationToken);

            // SHOW ALL GAMES - NO FILTERING
            var viewModel = new CBBPicksIndexViewModel
            {
                League = "CBB",
                Games = games
            };

            return View(viewModel);
        }

        public async Task<IActionResult> MLB()
        {
            try
            {
                // Get MLB games for today and tomorrow
                var games = await _mlbService.GetTodayGamesAsync();

                // Map MLB.Game to NBAGameMatchup for the view model
                var mappedGames = games.Select(g => new NBAGameMatchup
                {
                    GameId = $"{g.AwayTeam}-{g.HomeTeam}-{g.GameTime:yyyyMMdd}",
                    AwayTeamCode = g.AwayTeam,
                    AwayTeamName = g.AwayTeam,
                    AwayTeamLogo = g.AwayTeamLogoUrl ?? string.Empty,
                    HomeTeamCode = g.HomeTeam,
                    HomeTeamName = g.HomeTeam,
                    HomeTeamLogo = g.HomeTeamLogoUrl ?? string.Empty,
                    GameTime = g.GameTime,
                    Status = g.Status,
                    AwayScore = g.AwayScore,
                    HomeScore = g.HomeScore
                    // Spread, OverUnder, Moneyline fields can be mapped if available in MLB.Game
                }).ToList();

                var viewModel = new PicksIndexViewModel
                {
                    League = "MLB",
                    Games = mappedGames
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading MLB picks page");

                return View(new PicksIndexViewModel
                {
                    League = "MLB",
                    Games = new List<NBAGameMatchup>(),
                    ErrorMessage = "Unable to load MLB games. Please try again later."
                });
            }
        }

        [Authorize(Policy = "PremiumOnly")]
        public async Task<IActionResult> Detail(string gameId, CancellationToken cancellationToken)
        {
            var parts = gameId?.Split('-') ?? Array.Empty<string>();

            if (parts.Length < 2)
                return BadRequest("Invalid game ID format");

            var awayTeamCode = parts[0].ToUpper();
            var homeTeamCode = parts[1].ToUpper();

            var teamNames = new Dictionary<string, (string FullName, string LogoId)>
            {
                { "ATL", ("Atlanta Hawks",           "333") },
                { "BOS", ("Boston Celtics",           "334") },
                { "BKN", ("Brooklyn Nets",            "335") },
                { "CHA", ("Charlotte Hornets",        "336") },
                { "CHI", ("Chicago Bulls",            "337") },
                { "CLE", ("Cleveland Cavaliers",      "338") },
                { "DAL", ("Dallas Mavericks",         "338") },
                { "DEN", ("Denver Nuggets",           "339") },
                { "DET", ("Detroit Pistons",          "340") },
                { "GSW", ("Golden State Warriors",    "341") },
                { "HOU", ("Houston Rockets",          "342") },
                { "IND", ("Indiana Pacers",           "343") },
                { "LAC", ("LA Clippers",              "344") },
                { "LAL", ("Los Angeles Lakers",       "343") },
                { "MEM", ("Memphis Grizzlies",        "345") },
                { "MIA", ("Miami Heat",               "346") },
                { "MIL", ("Milwaukee Bucks",          "347") },
                { "MIN", ("Minnesota Timberwolves",   "348") },
                { "NOP", ("New Orleans Pelicans",     "349") },
                { "NYK", ("New York Knicks",          "350") },
                { "OKC", ("Oklahoma City Thunder",    "351") },
                { "ORL", ("Orlando Magic",            "352") },
                { "PHI", ("Philadelphia 76ers",       "352") },
                { "PHX", ("Phoenix Suns",             "353") },
                { "POR", ("Portland Trail Blazers",   "354") },
                { "SAC", ("Sacramento Kings",         "355") },
                { "SAS", ("San Antonio Spurs",        "356") },
                { "TOR", ("Toronto Raptors",          "357") },
                { "UTA", ("Utah Jazz",                "359") },
                { "WAS", ("Washington Wizards",       "361") }
            };

            var awayTeamInfo = teamNames.GetValueOrDefault(awayTeamCode, ("Unknown", ""));
            var homeTeamInfo = teamNames.GetValueOrDefault(homeTeamCode, ("Unknown", ""));

            var awayRoster = await _rosterService.GetTeamRosterAsync(awayTeamCode, cancellationToken);
            var homeRoster = await _rosterService.GetTeamRosterAsync(homeTeamCode, cancellationToken);

            var gameTime = DateTime.UtcNow.AddHours(6);
            var injuries = await _injuryReportService.GetCurrentInjuriesForGameAsync(
                awayTeamCode, homeTeamCode, gameTime, cancellationToken);

            var awayTeamName = string.IsNullOrEmpty(awayRoster.TeamName)
                || awayRoster.TeamName is "Unknown" or "Unknown Team"
                ? awayTeamInfo.Item1 : awayRoster.TeamName;

            var homeTeamName = string.IsNullOrEmpty(homeRoster.TeamName)
                || homeRoster.TeamName is "Unknown" or "Unknown Team"
                ? homeTeamInfo.Item1 : homeRoster.TeamName;

            var viewModel = new GameSimulationViewModel
            {
                GameId    = gameId ?? string.Empty,
                AwayTeam  = awayTeamName,
                HomeTeam  = homeTeamName,
                League    = "NBA",
                AwayTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{awayTeamInfo.Item2}.svg",
                HomeTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{homeTeamInfo.Item2}.svg",
                IsLoading = false
            };

            try
            {
                // Return today's existing simulation unless the user explicitly requests a fresh one
                bool forceRegenerate = Request.Query.ContainsKey("regenerate");
                GameSimulation? existing = null;

                if (!forceRegenerate)
                {
                    var todayStart = DateTime.UtcNow.Date;
                    var todayEnd   = todayStart.AddDays(1);

                    existing = await _db.GameSimulations
                        .Where(s => s.GameId == gameId
                                 && s.GeneratedAt >= todayStart
                                 && s.GeneratedAt < todayEnd)
                        .OrderByDescending(s => s.GeneratedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                if (existing != null)
                {
                    viewModel.SimulationContent = existing.SimulationContent;
                    viewModel.SimulationId      = existing.Id;
                    viewModel.IsFromCache       = true;
                    viewModel.CachedAt          = existing.GeneratedAt;
                }
                else
                {
                    viewModel.SimulationContent = await _simulationService.GenerateGameSimulationAsync(
                        viewModel.HomeTeam,
                        viewModel.AwayTeam,
                        viewModel.League,
                        homeRoster,
                        awayRoster,
                        injuries,
                        gameTime,
                        cancellationToken);

                    var simulation = new GameSimulation
                    {
                        GameId            = gameId ?? string.Empty,
                        League            = "NBA",
                        AwayTeamName      = awayTeamName,
                        HomeTeamName      = homeTeamName,
                        GameDate          = gameTime.Date,
                        SimulationContent = viewModel.SimulationContent,
                        GeneratedAt       = DateTime.UtcNow,
                        GeneratedByUserId = User.Identity?.IsAuthenticated == true
                                           ? _userManager.GetUserId(User)
                                           : null
                    };

                    _db.GameSimulations.Add(simulation);
                    await _db.SaveChangesAsync(cancellationToken);

                    viewModel.SimulationId = simulation.Id;
                    _logger.LogInformation(
                        "Saved simulation #{Id} for {GameId}", simulation.Id, simulation.GameId);
                }
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage  = "Unable to generate simulation at this time. Please try again later.";
                viewModel.SimulationContent = $"Error: {ex.Message}";
            }

            return View(viewModel);
        }

        [HttpGet("/api/test-odds-api")]
        public async Task<IActionResult> TestOddsApi([FromServices] IOptions<OddsApiOptions> options, [FromServices] TheOddsApiClient client)
        {
            var opts = options.Value;
            
            try
            {
                var result = await client.GetAsync("/v4/sports", CancellationToken.None);
                
                return Ok(new
                {
                    Success = true,
                    HasApiKey = !string.IsNullOrEmpty(opts.ApiKey),
                    ApiKeyLength = opts.ApiKey?.Length ?? 0,
                    BaseUrl = opts.BaseUrl,
                    SportsCount = result.GetArrayLength()
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    Success = false,
                    Error = ex.Message,
                    HasApiKey = !string.IsNullOrEmpty(opts.ApiKey),
                    ApiKeyLength = opts.ApiKey?.Length ?? 0,
                    BaseUrl = opts.BaseUrl
                });
            }
        }
    }
}
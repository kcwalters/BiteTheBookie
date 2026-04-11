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

        // MLB team codes used to detect whether a gameId belongs to MLB
        private static readonly HashSet<string> MlbTeamCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            "ARI","ATL","BAL","BOS","CHC","CHW","CIN","CLE","COL","DET",
            "HOU","KC","LAA","LAD","MIA","MIL","MIN","NYM","NYY","OAK",
            "PHI","PIT","SD","SF","SEA","STL","TB","TEX","TOR","WSH",
            // ESPN-style alternates
            "Arizona Diamondbacks","Atlanta Braves","Baltimore Orioles","Boston Red Sox",
            "Chicago Cubs","Chicago White Sox","Cincinnati Reds","Cleveland Guardians",
            "Colorado Rockies","Detroit Tigers","Houston Astros","Kansas City Royals",
            "Los Angeles Angels","Los Angeles Dodgers","Miami Marlins","Milwaukee Brewers",
            "Minnesota Twins","New York Mets","New York Yankees","Athletics",
            "Philadelphia Phillies","Pittsburgh Pirates","San Diego Padres",
            "San Francisco Giants","Seattle Mariners","St. Louis Cardinals",
            "Tampa Bay Rays","Texas Rangers","Toronto Blue Jays","Washington Nationals"
        };

        private static readonly Dictionary<string, (string FullName, string LogoId)> NbaTeamNames = new()
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
            _simulationService     = simulationService;
            _rosterService         = rosterService;
            _gamesService          = gamesService;
            _cbbGamesService       = cbbGamesService;
            _cbbRosterService      = cbbRosterService;
            _spreadAnalysisService = spreadAnalysisService;
            _injuryReportService   = injuryReportService;
            _mlbService            = mlbService;
            _db                    = db;
            _userManager           = userManager;
            _logger                = logger;
        }

        [Authorize(Policy = "PremiumOnly")]
        public async Task<IActionResult> ViewPicks(string gameId, string league = "NBA")
        {
            var picks = await _db.ExpertPicks
                .Where(p => p.GameId == gameId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

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
                _logger?.LogError(ex, "Error loading NBA picks");

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
            var games = await _gamesService.GetUpcomingNBAGamesAsync(cancellationToken);

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
                _logger?.LogError(ex, "Error loading NBA picks");

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
            var games = await _cbbGamesService.GetUpcomingCBBGamesAsync(cancellationToken);

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
                var games = await _mlbService.GetTodayGamesAsync();

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

            var awayTeamCode = parts[0];
            var homeTeamCode = parts[1];

            // Detect league from the team codes in the gameId
            bool isMlb = MlbTeamCodes.Contains(awayTeamCode) || MlbTeamCodes.Contains(homeTeamCode);
            string league = isMlb ? "MLB" : "NBA";

            GameSimulationViewModel viewModel;

            if (isMlb)
            {
                viewModel = BuildMlbViewModel(gameId!, awayTeamCode, homeTeamCode);
            }
            else
            {
                viewModel = await BuildNbaViewModelAsync(gameId!, awayTeamCode.ToUpper(), homeTeamCode.ToUpper(), cancellationToken);
            }

            // ── Step 1: Try to load a cached simulation from the DB ───────────────
            bool forceRegenerate = Request.Query.ContainsKey("regenerate");
            GameSimulation? existing = null;

            if (!forceRegenerate)
            {
                try
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
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx,
                        "GameSimulations table unavailable — generating fresh simulation for {GameId}", gameId);
                }
            }

            // ── Step 2a: Serve cached simulation ─────────────────────────────────
            if (existing != null)
            {
                viewModel.SimulationContent = existing.SimulationContent;
                viewModel.SimulationId      = existing.Id;
                viewModel.IsFromCache       = true;
                viewModel.CachedAt          = existing.GeneratedAt;

                _logger.LogInformation(
                    "Serving cached simulation #{Id} for {GameId}", existing.Id, gameId);
            }
            else
            {
                // ── Step 2b: Generate via AI ──────────────────────────────────────
                try
                {
                    if (isMlb)
                    {
                        viewModel.SimulationContent = await _simulationService.GenerateGameSimulationAsync(
                            viewModel.HomeTeam,
                            viewModel.AwayTeam,
                            "MLB",
                            homeRoster: null,
                            awayRoster: null,
                            injuries: null,
                            DateTime.UtcNow,
                            cancellationToken);
                    }
                    else
                    {
                        var awayRoster = await _rosterService.GetTeamRosterAsync(awayTeamCode.ToUpper(), cancellationToken);
                        var homeRoster = await _rosterService.GetTeamRosterAsync(homeTeamCode.ToUpper(), cancellationToken);
                        var gameTime   = DateTime.UtcNow.AddHours(6);
                        var injuries   = await _injuryReportService.GetCurrentInjuriesForGameAsync(
                            awayTeamCode.ToUpper(), homeTeamCode.ToUpper(), gameTime, cancellationToken);

                        viewModel.SimulationContent = await _simulationService.GenerateGameSimulationAsync(
                            viewModel.HomeTeam,
                            viewModel.AwayTeam,
                            "NBA",
                            homeRoster,
                            awayRoster,
                            injuries,
                            gameTime,
                            cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    viewModel.ErrorMessage      = "Unable to generate simulation at this time. Please try again later.";
                    viewModel.SimulationContent  = $"Error: {ex.Message}";
                    return View(viewModel);
                }

                // ── Step 2c: Persist to DB (non-fatal) ───────────────────────────
                try
                {
                    var simulation = new GameSimulation
                    {
                        GameId            = gameId ?? string.Empty,
                        League            = league,
                        AwayTeamName      = viewModel.AwayTeam,
                        HomeTeamName      = viewModel.HomeTeam,
                        GameDate          = DateTime.UtcNow.Date,
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
                catch (Exception dbEx)
                {
                    _logger.LogWarning(dbEx,
                        "Could not persist simulation for {GameId} — migration may be pending", gameId);
                }
            }

            return View(viewModel);
        }

        private GameSimulationViewModel BuildMlbViewModel(string gameId, string awayTeamCode, string homeTeamCode)
        {
            return new GameSimulationViewModel
            {
                GameId       = gameId,
                AwayTeam     = awayTeamCode,
                HomeTeam     = homeTeamCode,
                League       = "MLB",
                AwayTeamLogo = $"https://www.mlbstatic.com/team-logos/{GetMlbTeamId(awayTeamCode)}.svg",
                HomeTeamLogo = $"https://www.mlbstatic.com/team-logos/{GetMlbTeamId(homeTeamCode)}.svg",
                IsLoading    = false
            };
        }

        private async Task<GameSimulationViewModel> BuildNbaViewModelAsync(
            string gameId, string awayTeamCode, string homeTeamCode, CancellationToken cancellationToken)
        {
            var awayTeamInfo = NbaTeamNames.GetValueOrDefault(awayTeamCode, ("Unknown", ""));
            var homeTeamInfo = NbaTeamNames.GetValueOrDefault(homeTeamCode, ("Unknown", ""));

            var awayRoster = await _rosterService.GetTeamRosterAsync(awayTeamCode, cancellationToken);
            var homeRoster = await _rosterService.GetTeamRosterAsync(homeTeamCode, cancellationToken);

            var awayTeamName = string.IsNullOrEmpty(awayRoster.TeamName)
                || awayRoster.TeamName is "Unknown" or "Unknown Team"
                ? awayTeamInfo.Item1 : awayRoster.TeamName;

            var homeTeamName = string.IsNullOrEmpty(homeRoster.TeamName)
                || homeRoster.TeamName is "Unknown" or "Unknown Team"
                ? homeTeamInfo.Item1 : homeRoster.TeamName;

            return new GameSimulationViewModel
            {
                GameId       = gameId,
                AwayTeam     = awayTeamName,
                HomeTeam     = homeTeamName,
                League       = "NBA",
                AwayTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{awayTeamInfo.Item2}.svg",
                HomeTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{homeTeamInfo.Item2}.svg",
                IsLoading    = false
            };
        }

        /// <summary>Maps an MLB team name to its mlbstatic.com numeric team ID for logo URLs.</summary>
        private static string GetMlbTeamId(string team)
        {
            return team switch
            {
                "Arizona Diamondbacks" => "109", "Atlanta Braves"       => "144",
                "Baltimore Orioles"    => "110", "Boston Red Sox"       => "111",
                "Chicago Cubs"         => "112", "Chicago White Sox"    => "145",
                "Cincinnati Reds"      => "113", "Cleveland Guardians"  => "114",
                "Colorado Rockies"     => "115", "Detroit Tigers"       => "116",
                "Houston Astros"       => "117", "Kansas City Royals"   => "118",
                "Los Angeles Angels"   => "108", "Los Angeles Dodgers"  => "119",
                "Miami Marlins"        => "146", "Milwaukee Brewers"    => "158",
                "Minnesota Twins"      => "142", "New York Mets"        => "121",
                "New York Yankees"     => "147", "Athletics"            => "133",
                "Philadelphia Phillies"=> "143", "Pittsburgh Pirates"   => "134",
                "San Diego Padres"     => "135", "San Francisco Giants" => "137",
                "Seattle Mariners"     => "136", "St. Louis Cardinals"  => "138",
                "Tampa Bay Rays"       => "139", "Texas Rangers"        => "140",
                "Toronto Blue Jays"    => "141", "Washington Nationals" => "120",
                _ => "1" // fallback to league logo
            };
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
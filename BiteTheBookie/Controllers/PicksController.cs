using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.ViewModels;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Controllers
{
    public class PicksController : Controller
    {
        private readonly IGameSimulationService _simulationService;
        private readonly INBARosterService _rosterService;
        private readonly INBAGamesService _gamesService;
        private readonly ISpreadAnalysisService _spreadAnalysisService;
        private readonly IInjuryReportService _injuryReportService;

        public PicksController(
            IGameSimulationService simulationService, 
            INBARosterService rosterService,
            INBAGamesService gamesService,
            ISpreadAnalysisService spreadAnalysisService,
            IInjuryReportService injuryReportService)
        {
            _simulationService = simulationService;
            _rosterService = rosterService;
            _gamesService = gamesService;
            _spreadAnalysisService = spreadAnalysisService;
            _injuryReportService = injuryReportService;
        }

        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            // Fetch upcoming NBA games dynamically
            var games = await _gamesService.GetUpcomingGamesAsync(cancellationToken);
            
            var viewModel = new PicksIndexViewModel
            {
                League = "NBA",
                Games = games
            };

            return View(viewModel);
        }

        public async Task<IActionResult> AgainstTheSpread(CancellationToken cancellationToken)
        {
            // Fetch upcoming games
            var games = await _gamesService.GetUpcomingGamesAsync(cancellationToken);
            
            // Analyze for contrarian betting opportunities
            var opportunities = await _spreadAnalysisService.AnalyzeSpreadOpportunitiesAsync(games, cancellationToken);
            
            var viewModel = new AgainstTheSpreadViewModel
            {
                League = "NBA",
                Opportunities = opportunities
            };

            return View(viewModel);
        }

        public IActionResult NBA()
        {
            return View();
        }

        public IActionResult NFL()
        {
            return View();
        }

        public IActionResult NHL()
        {
            return View();
        }

        public IActionResult CBB()
        {
            return View();
        }

        public IActionResult MLB()
        {
            return View();
        }

        public async Task<IActionResult> Detail(string gameId, CancellationToken cancellationToken)
        {
            // Parse gameId to extract team information
            // Format expected: "teamA-teamB-date" e.g., "bos-mil-20260303"
            var parts = gameId?.Split('-') ?? Array.Empty<string>();
            
            if (parts.Length < 2)
            {
                return BadRequest("Invalid game ID format");
            }

            var awayTeamCode = parts[0].ToUpper();
            var homeTeamCode = parts[1].ToUpper();

            // Map team codes to full names and logo IDs
            var teamNames = new Dictionary<string, (string FullName, string LogoId)>
            {
                { "BOS", ("Boston Celtics", "334") },
                { "MIL", ("Milwaukee Bucks", "347") },
                { "DEN", ("Denver Nuggets", "339") },
                { "UTA", ("Utah Jazz", "359") },
                { "HOU", ("Houston Rockets", "342") },
                { "WAS", ("Washington Wizards", "361") },
                { "LAL", ("Los Angeles Lakers", "343") },
                { "GSW", ("Golden State Warriors", "341") },
                { "PHI", ("Philadelphia 76ers", "352") },
                { "MIA", ("Miami Heat", "346") },
                { "BKN", ("Brooklyn Nets", "335") },
                { "DAL", ("Dallas Mavericks", "338") },
                { "PHX", ("Phoenix Suns", "353") },
                { "LAC", ("LA Clippers", "344") }
            };

            var awayTeamInfo = teamNames.GetValueOrDefault(awayTeamCode, ("Unknown", ""));
            var homeTeamInfo = teamNames.GetValueOrDefault(homeTeamCode, ("Unknown", ""));

            // Get team rosters for actual players
            var awayRoster = _rosterService.GetTeamRoster(awayTeamCode);
            var homeRoster = _rosterService.GetTeamRoster(homeTeamCode);

            // Get injury reports for both teams
            var gameTime = DateTime.UtcNow.AddHours(6); // Default game time (can be improved with actual game time from schedule)
            var injuries = await _injuryReportService.GetCurrentInjuriesForGameAsync(awayTeamCode, homeTeamCode, gameTime, cancellationToken);

            // Use roster team name if available, otherwise fall back to mapped name
            var awayTeamName = string.IsNullOrEmpty(awayRoster.TeamName) 
                || awayRoster.TeamName == "Unknown" 
                || awayRoster.TeamName == "Unknown Team"
                ? awayTeamInfo.Item1 
                : awayRoster.TeamName;
            
            var homeTeamName = string.IsNullOrEmpty(homeRoster.TeamName) 
                || homeRoster.TeamName == "Unknown" 
                || homeRoster.TeamName == "Unknown Team"
                ? homeTeamInfo.Item1 
                : homeRoster.TeamName;

            var viewModel = new GameSimulationViewModel
            {
                GameId = gameId ?? string.Empty,
                AwayTeam = awayTeamName,
                HomeTeam = homeTeamName,
                League = "NBA",
                AwayTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{awayTeamInfo.Item2}.svg",
                HomeTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{homeTeamInfo.Item2}.svg",
                IsLoading = false
            };

            try
            {
                // Generate simulation using AI with actual team rosters and injury info
                viewModel.SimulationContent = await _simulationService.GenerateGameSimulationAsync(
                    viewModel.HomeTeam, 
                    viewModel.AwayTeam, 
                    viewModel.League,
                    homeRoster,
                    awayRoster,
                    injuries,
                    gameTime,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                viewModel.ErrorMessage = "Unable to generate simulation at this time. Please try again later.";
                viewModel.SimulationContent = $"Error: {ex.Message}";
            }

            return View(viewModel);
        }
    }
}
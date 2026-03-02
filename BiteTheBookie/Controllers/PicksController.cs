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

        public PicksController(
            IGameSimulationService simulationService, 
            INBARosterService rosterService,
            INBAGamesService gamesService)
        {
            _simulationService = simulationService;
            _rosterService = rosterService;
            _gamesService = gamesService;
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

            // Get team rosters for actual players
            var awayRoster = _rosterService.GetTeamRoster(awayTeamCode);
            var homeRoster = _rosterService.GetTeamRoster(homeTeamCode);

            // Map team codes to full names and logo IDs
            var teamNames = new Dictionary<string, (string FullName, string LogoId)>
            {
                { "BOS", ("Boston Celtics", "334") },
                { "MIL", ("Milwaukee Bucks", "347") },
                { "DEN", ("Denver Nuggets", "339") },
                { "UTA", ("Utah Jazz", "359") },
                { "HOU", ("Houston Rockets", "342") },
                { "WAS", ("Washington Wizards", "361") }
            };

            var awayTeamInfo = teamNames.GetValueOrDefault(awayTeamCode, ("Unknown", ""));
            var homeTeamInfo = teamNames.GetValueOrDefault(homeTeamCode, ("Unknown", ""));

            var viewModel = new GameSimulationViewModel
            {
                GameId = gameId ?? string.Empty,
                AwayTeam = awayRoster.TeamName,
                HomeTeam = homeRoster.TeamName,
                League = "NBA",
                AwayTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{awayTeamInfo.Item2}.svg",
                HomeTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{homeTeamInfo.Item2}.svg",
                IsLoading = false
            };

            try
            {
                // Generate simulation using AI with actual team rosters
                viewModel.SimulationContent = await _simulationService.GenerateGameSimulationAsync(
                    viewModel.HomeTeam, 
                    viewModel.AwayTeam, 
                    viewModel.League,
                    homeRoster,
                    awayRoster,
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




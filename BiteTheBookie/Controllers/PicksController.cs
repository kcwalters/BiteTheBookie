using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.ViewModels;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Controllers
{
    public class PicksController : Controller
    {
        private readonly IGameSimulationService _simulationService;

        public PicksController(IGameSimulationService simulationService)
        {
            _simulationService = simulationService;
        }

        public IActionResult Index()
        {
            // Default view shows NBA picks
            return View();
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

            // Map team codes to full names (you could expand this or use a database)
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
                AwayTeam = awayTeamInfo.Item1,
                HomeTeam = homeTeamInfo.Item1,
                League = "NBA",
                AwayTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{awayTeamInfo.Item2}.svg",
                HomeTeamLogo = $"https://sports.cbsimg.net/fly/images/nba/logos/team/{homeTeamInfo.Item2}.svg",
                IsLoading = false
            };

            try
            {
                // Generate simulation using AI
                viewModel.SimulationContent = await _simulationService.GenerateGameSimulationAsync(
                    viewModel.HomeTeam, 
                    viewModel.AwayTeam, 
                    viewModel.League, 
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



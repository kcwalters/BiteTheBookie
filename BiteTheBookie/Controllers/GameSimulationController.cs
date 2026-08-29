using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;
using System.Threading.Tasks;

namespace BiteTheBookie.Controllers
{
    [Authorize]
    public class GameSimulationController : Controller
    {
        private readonly IGameSimulationService _simulationService;

        public GameSimulationController(IGameSimulationService simulationService)
        {
            _simulationService = simulationService;
        }

        [HttpGet]
        public async Task<IActionResult> Start(string homeTeam, string awayTeam, string league)
        {
            if (string.IsNullOrWhiteSpace(homeTeam)
                || string.IsNullOrWhiteSpace(awayTeam)
                || string.IsNullOrWhiteSpace(league))
            {
                return Content(
                    "<section class=\"alert alert-warning\"><h2>Simulation Unavailable</h2>" +
                    "<p>Missing game details. Please try again from the scoreboard.</p></section>",
                    "text/html");
            }

            var simulationResult = await _simulationService.GenerateGameSimulationAsync(
                homeTeam: homeTeam,
                awayTeam: awayTeam,
                league: league
            );

            // Return simulation output.
            return Content(simulationResult, "text/html");
        }
    }
}
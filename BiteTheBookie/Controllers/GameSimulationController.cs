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

        [HttpPost]
        public async Task<IActionResult> Start(string gameId)
        {
            // Lookup game details, validate input...

            var simulationResult = await _simulationService.GenerateGameSimulationAsync(
                homeTeam: "HomeTeamExample",  // Replace with actual team data
                awayTeam: "AwayTeamExample",  // Replace with actual team data
                league: "NFL"                 // Replace with actual league
            );

            // Return simulation output.
            return Content(simulationResult, "text/html");
        }
    }
}
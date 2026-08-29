using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
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
            if (string.IsNullOrWhiteSpace(homeTeam) || string.IsNullOrWhiteSpace(awayTeam) || string.IsNullOrWhiteSpace(league))
            {
                return BadRequest("Missing game information for the simulation.");
            }

            var simulationResult = await _simulationService.GenerateGameSimulationAsync(
                homeTeam: homeTeam,
                awayTeam: awayTeam,
                league: league
            );

            var model = new GameSimulationViewModel
            {
                GameId = $"{awayTeam}-at-{homeTeam}-{league}".ToLowerInvariant(),
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                League = league,
                SimulationContent = simulationResult,
                IsLoading = false
            };

            return View(model);
        }
    }
}
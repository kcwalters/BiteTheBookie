using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace BiteTheBookie.Controllers
{
    [Authorize]
    public class GameSimulationController : Controller
    {
        private readonly IGameSimulationService _simulationService;
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public GameSimulationController(
            IGameSimulationService simulationService,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager)
        {
            _simulationService = simulationService;
            _db = db;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Start(string homeTeam, string awayTeam, string league, bool regenerate = false)
        {
            if (string.IsNullOrWhiteSpace(homeTeam)
                || string.IsNullOrWhiteSpace(awayTeam)
                || string.IsNullOrWhiteSpace(league))
            {
                var invalid = new GameSimulationViewModel
                {
                    HomeTeam = homeTeam ?? string.Empty,
                    AwayTeam = awayTeam ?? string.Empty,
                    League = string.IsNullOrWhiteSpace(league) ? "NBA" : league,
                    ErrorMessage = "Missing game details. Please try again from the scoreboard."
                };
                return View(invalid);
            }

            var model = await BuildSimulationModelAsync(homeTeam, awayTeam, league, regenerate);
            return View(model);
        }

        private async Task<GameSimulationViewModel> BuildSimulationModelAsync(
            string homeTeam, string awayTeam, string league, bool regenerate = false)
        {
            var gameDate = DateTime.UtcNow.Date;
            var gameId = BuildGameId(homeTeam, awayTeam, league, gameDate);

            // Pull the most recent existing simulation for this game if one exists.
            var existing = await _db.GameSimulations
                .Where(g => g.GameId == gameId)
                .OrderByDescending(g => g.GeneratedAt)
                .FirstOrDefaultAsync();

            if (existing != null && !regenerate)
            {
                return new GameSimulationViewModel
                {
                    GameId = existing.GameId,
                    HomeTeam = existing.HomeTeamName,
                    AwayTeam = existing.AwayTeamName,
                    League = existing.League,
                    SimulationContent = existing.SimulationContent,
                    SimulationId = existing.Id,
                    IsFromCache = true,
                    CachedAt = existing.GeneratedAt,
                    IsLoading = false
                };
            }

            var simulationResult = await _simulationService.GenerateGameSimulationAsync(
                homeTeam: homeTeam,
                awayTeam: awayTeam,
                league: league
            );

            var userId = _userManager.GetUserId(User);

            GameSimulation entity;
            if (existing != null && regenerate)
            {
                existing.SimulationContent = simulationResult;
                existing.GeneratedAt = DateTime.UtcNow;
                existing.GeneratedByUserId = userId;
                entity = existing;
            }
            else
            {
                entity = new GameSimulation
                {
                    GameId = gameId,
                    League = league,
                    AwayTeamName = awayTeam,
                    HomeTeamName = homeTeam,
                    GameDate = gameDate,
                    SimulationContent = simulationResult,
                    GeneratedAt = DateTime.UtcNow,
                    GeneratedByUserId = userId
                };
                _db.GameSimulations.Add(entity);
            }

            await _db.SaveChangesAsync();

            return new GameSimulationViewModel
            {
                GameId = entity.GameId,
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                League = league,
                SimulationContent = simulationResult,
                SimulationId = entity.Id,
                IsFromCache = false,
                CachedAt = entity.GeneratedAt,
                IsLoading = false
            };
        }

        private static string BuildGameId(string homeTeam, string awayTeam, string league, DateTime gameDate)
            => $"{awayTeam}-at-{homeTeam}-{league}-{gameDate:yyyyMMdd}".ToLowerInvariant();
    }
}
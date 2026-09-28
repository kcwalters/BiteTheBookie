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

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Admin(
            string? league = null,
            string? search = null,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string timeframe = "all",
            string sortBy = "gamedate",
            string sortDir = "desc")
        {
            var query = _db.GameSimulations.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(league))
            {
                query = query.Where(g => g.League == league);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(g =>
                    g.HomeTeamName.Contains(term)
                    || g.AwayTeamName.Contains(term)
                    || g.GameId.Contains(term));
            }

            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(g => g.GameDate >= from);
            }

            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                query = query.Where(g => g.GameDate < to);
            }

            var today = DateTime.UtcNow.Date;
            if (string.Equals(timeframe, "past", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g => g.GameDate < today);
            }
            else if (string.Equals(timeframe, "future", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(g => g.GameDate >= today);
            }

            var ascending = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase);
            query = (sortBy?.ToLowerInvariant()) switch
            {
                "league" => ascending ? query.OrderBy(g => g.League) : query.OrderByDescending(g => g.League),
                "home" => ascending ? query.OrderBy(g => g.HomeTeamName) : query.OrderByDescending(g => g.HomeTeamName),
                "away" => ascending ? query.OrderBy(g => g.AwayTeamName) : query.OrderByDescending(g => g.AwayTeamName),
                "generatedat" => ascending ? query.OrderBy(g => g.GeneratedAt) : query.OrderByDescending(g => g.GeneratedAt),
                _ => ascending ? query.OrderBy(g => g.GameDate) : query.OrderByDescending(g => g.GameDate),
            };

            var simulations = await query.ToListAsync();

            var leagues = await _db.GameSimulations
                .AsNoTracking()
                .Select(g => g.League)
                .Distinct()
                .OrderBy(l => l)
                .ToListAsync();

            var model = new AdminGameSimulationsViewModel
            {
                Simulations = simulations,
                Leagues = leagues,
                League = league,
                Search = search,
                FromDate = fromDate,
                ToDate = toDate,
                Timeframe = string.IsNullOrWhiteSpace(timeframe) ? "all" : timeframe,
                SortBy = string.IsNullOrWhiteSpace(sortBy) ? "gamedate" : sortBy,
                SortDir = ascending ? "asc" : "desc",
                TotalCount = simulations.Count
            };

            return View(model);
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Details(int id)
        {
            var simulation = await _db.GameSimulations
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == id);

            if (simulation == null)
            {
                return NotFound();
            }

            return View(simulation);
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
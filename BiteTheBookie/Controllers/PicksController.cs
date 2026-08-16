using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    public class PicksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IExpertPickAccessService _pickAccess;
        private readonly IGameSimulationService _simulationService;
        private readonly ILogger<PicksController> _logger;

        public PicksController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IExpertPickAccessService pickAccess,
            IGameSimulationService simulationService,
            ILogger<PicksController> logger)
        {
            _db = db;
            _userManager = userManager;
            _pickAccess = pickAccess;
            _simulationService = simulationService;
            _logger = logger;
        }

        /// <summary>
        /// Unlocks and displays the premium expert picks for a single game, enforcing
        /// the member's weekly expert-pick limit (Free = locked, Pro = 5 games/week,
        /// All Access = unlimited). Unlocking one game counts as one weekly pick.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ViewPick(string gameId, string league)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return NotFound();
            }

            var picks = await _db.ExpertPicks
                .Where(p => p.GameId == gameId)
                .OrderByDescending(p => p.Confidence)
                .ThenByDescending(p => p.CreatedAt)
                .ToListAsync();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Join", "Membership");
            }

            var resolvedLeague = !string.IsNullOrWhiteSpace(league)
                ? league
                : picks.FirstOrDefault()?.League ?? string.Empty;

            var tier = user.IsPremium ? user.SubscriptionTier : SubscriptionTier.Free;

            var access = await _pickAccess.TryUnlockGameAsync(user.Id, tier, gameId, resolvedLeague);

            var vm = new ExpertPickViewModel
            {
                GameId = gameId,
                League = resolvedLeague,
                Picks = picks,
                Access = access
            };

            if (!access.Granted)
            {
                _logger.LogInformation(
                    "User {UserId} denied game {GameId}: tier={Tier}, used={Used}/{Limit}",
                    user.Id, gameId, tier, access.WeeklyUsed, access.WeeklyLimit);
            }

            return View(vm);
        }

        /// <summary>
        /// Displays an AI-generated game simulation for a single matchup. Simulations are
        /// cached one-per-game-per-day and reused unless <paramref name="regenerate"/> is set.
        /// Access requires a membership tier that allows simulations, and generation is
        /// capped by the member's daily simulation limit.
        /// </summary>
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Detail(
            string gameId,
            string homeTeam,
            string awayTeam,
            string league,
            string? homeLogo = null,
            string? awayLogo = null,
            bool regenerate = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Join", "Membership");
            }

            var tier = user.IsPremium ? user.SubscriptionTier : SubscriptionTier.Free;

            // Membership gate: tiers without simulation access are sent to upgrade.
            if (!MembershipFeatures.AllowsGameSimulation(tier))
            {
                return RedirectToAction("Join", "Membership");
            }

            var todayStartUtc = DateTime.UtcNow.Date;
            var tomorrowStartUtc = todayStartUtc.AddDays(1);

            // Look for a simulation already generated for this game today.
            var existing = await _db.GameSimulations
                .Where(g => g.GameId == gameId
                            && g.GeneratedAt >= todayStartUtc
                            && g.GeneratedAt < tomorrowStartUtc)
                .OrderByDescending(g => g.GeneratedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var vm = new GameSimulationViewModel
            {
                GameId = gameId,
                League = !string.IsNullOrWhiteSpace(league) ? league : existing?.League ?? string.Empty,
                HomeTeam = !string.IsNullOrWhiteSpace(homeTeam) ? homeTeam : existing?.HomeTeamName ?? string.Empty,
                AwayTeam = !string.IsNullOrWhiteSpace(awayTeam) ? awayTeam : existing?.AwayTeamName ?? string.Empty,
                HomeTeamLogo = homeLogo ?? string.Empty,
                AwayTeamLogo = awayLogo ?? string.Empty
            };

            // Serve the cached simulation unless the user explicitly asked to regenerate.
            if (existing != null && !regenerate)
            {
                vm.SimulationContent = existing.SimulationContent;
                vm.SimulationId = existing.Id;
                vm.IsFromCache = true;
                vm.CachedAt = existing.GeneratedAt;
                return View(vm);
            }

            // We need team names to generate a fresh simulation.
            if (string.IsNullOrWhiteSpace(vm.HomeTeam) || string.IsNullOrWhiteSpace(vm.AwayTeam))
            {
                vm.ErrorMessage = "We couldn't determine the teams for this matchup. Please open the simulation from a schedule or scoreboard.";
                return View(vm);
            }

            // Enforce the member's daily simulation limit (unlimited tiers skip this).
            var dailyLimit = MembershipFeatures.DailySimulationLimit(tier);
            if (dailyLimit != int.MaxValue)
            {
                var usedToday = await _db.GameSimulations
                    .CountAsync(g => g.GeneratedByUserId == user.Id
                                     && g.GeneratedAt >= todayStartUtc
                                     && g.GeneratedAt < tomorrowStartUtc,
                                cancellationToken);

                if (usedToday >= dailyLimit)
                {
                    vm.ErrorMessage = $"You've reached your daily simulation limit ({dailyLimit}). Upgrade your membership for more simulations.";
                    return View(vm);
                }
            }

            try
            {
                var content = await _simulationService.GenerateGameSimulationAsync(
                    vm.HomeTeam,
                    vm.AwayTeam,
                    vm.League,
                    cancellationToken: cancellationToken);

                var simulation = new GameSimulation
                {
                    GameId = gameId,
                    League = vm.League,
                    HomeTeamName = vm.HomeTeam,
                    AwayTeamName = vm.AwayTeam,
                    GameDate = todayStartUtc,
                    SimulationContent = content,
                    GeneratedAt = DateTime.UtcNow,
                    GeneratedByUserId = user.Id
                };

                _db.GameSimulations.Add(simulation);
                await _db.SaveChangesAsync(cancellationToken);

                vm.SimulationContent = content;
                vm.SimulationId = simulation.Id;
                vm.IsFromCache = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to generate simulation for game {GameId} ({Away} @ {Home})",
                    gameId, vm.AwayTeam, vm.HomeTeam);
                vm.ErrorMessage = "We couldn't generate the simulation right now. Please try again in a moment.";
            }

            return View(vm);
        }
    }

    public class ExpertPickViewModel
    {
        public string GameId { get; set; } = string.Empty;
        public string League { get; set; } = string.Empty;
        public List<ExpertPick> Picks { get; set; } = new();
        public ExpertPickAccessResult Access { get; set; } = default!;
    }
}

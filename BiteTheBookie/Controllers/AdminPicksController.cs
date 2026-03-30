using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminPicksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminPicksController> _logger;

        public AdminPicksController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminPicksController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string league = "NBA")
        {
            var picks = await _db.ExpertPicks
                .Where(p => p.League == league)
                .OrderByDescending(p => p.GameTime)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new ExpertPickSummary
                {
                    Id = p.Id,
                    GameId = p.GameId,
                    League = p.League,
                    AwayTeamName = p.AwayTeamName,
                    HomeTeamName = p.HomeTeamName,
                    GameTime = p.GameTime,
                    PickType = p.PickType,
                    PickSelection = p.PickSelection,
                    Confidence = p.Confidence,
                    Analysis = p.Analysis,
                    EnteredBy = p.EnteredBy,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            var vm = new AdminPicksListViewModel
            {
                SelectedLeague = league,
                Picks = picks
            };

            return View(vm);
        }

        [HttpGet]
        public IActionResult Create(string league = "NBA", string? gameId = null,
            string? awayTeam = null, string? homeTeam = null, string? gameTime = null)
        {
            var vm = new AdminPickViewModel
            {
                League = league,
                GameId = gameId ?? string.Empty,
                AwayTeamName = awayTeam ?? string.Empty,
                HomeTeamName = homeTeam ?? string.Empty,
                GameTime = DateTime.TryParse(gameTime, out var gt) ? gt : DateTime.UtcNow
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminPickViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);

            var pick = new ExpertPick
            {
                GameId = vm.GameId,
                League = vm.League,
                AwayTeamName = vm.AwayTeamName,
                HomeTeamName = vm.HomeTeamName,
                GameTime = vm.GameTime,
                PickType = vm.PickType,
                PickSelection = vm.PickSelection,
                Confidence = vm.Confidence,
                Analysis = vm.Analysis,
                EnteredBy = user?.FirstName ?? User.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow
            };

            _db.ExpertPicks.Add(pick);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Admin {User} created pick for {GameId} ({League})",
                pick.EnteredBy, pick.GameId, pick.League);

            TempData["SuccessMessage"] = $"Pick saved for {vm.AwayTeamName} @ {vm.HomeTeamName}";
            return RedirectToAction("Index", new { league = vm.League });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pick = await _db.ExpertPicks.FindAsync(id);
            if (pick == null) return NotFound();

            var vm = new AdminPickViewModel
            {
                Id = pick.Id,
                GameId = pick.GameId,
                League = pick.League,
                AwayTeamName = pick.AwayTeamName,
                HomeTeamName = pick.HomeTeamName,
                GameTime = pick.GameTime,
                PickType = pick.PickType,
                PickSelection = pick.PickSelection,
                Confidence = pick.Confidence,
                Analysis = pick.Analysis
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminPickViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var pick = await _db.ExpertPicks.FindAsync(vm.Id);
            if (pick == null) return NotFound();

            pick.PickType = vm.PickType;
            pick.PickSelection = vm.PickSelection;
            pick.Confidence = vm.Confidence;
            pick.Analysis = vm.Analysis;
            pick.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Pick updated successfully";
            return RedirectToAction("Index", new { league = vm.League });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pick = await _db.ExpertPicks.FindAsync(id);
            if (pick == null) return NotFound();

            var league = pick.League;
            _db.ExpertPicks.Remove(pick);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Pick deleted";
            return RedirectToAction("Index", new { league });
        }
    }
}
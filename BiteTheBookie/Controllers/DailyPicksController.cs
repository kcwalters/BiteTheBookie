using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    /// <summary>
    /// Surfaces the automated, AI-generated daily picks. The page is publicly
    /// accessible; non-paid visitors see a masked preview and an upgrade prompt,
    /// while paid (Pro/AllAccess/Admin) members see the full picks. An admin-only
    /// action allows triggering generation on demand instead of waiting for the
    /// scheduled 7:00 AM Eastern run.
    /// </summary>
    [AllowAnonymous]
    public class DailyPicksController : Controller
    {
        private readonly IDailyPickService _dailyPickService;
        private readonly ILogger<DailyPicksController> _logger;
        private readonly Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public DailyPicksController(
            IDailyPickService dailyPickService,
            ILogger<DailyPicksController> logger,
            Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _dailyPickService = dailyPickService;
            _logger = logger;
            _userManager = userManager;
        }

        /// <summary>Lists upcoming AI picks grouped by day and league.</summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? league, CancellationToken cancellationToken)
        {
            var fromDate = NBAGameMatchup.LeagueToday;
            var picks = await _dailyPickService.GetUpcomingPicksAsync(fromDate, league, cancellationToken);
            ViewBag.SelectedLeague = league;

            var isPaid = false;
            if (User?.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole("Admin"))
                {
                    isPaid = true;
                }
                else
                {
                    var user = await _userManager.GetUserAsync(User);
                    isPaid = user?.IsPro == true || user?.SubscriptionTier == SubscriptionTier.Admin;
                }
            }

            ViewBag.IsPaid = isPaid;
            return View(picks);
        }

        /// <summary>Admin-only manual trigger for pick generation.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GenerateNow(CancellationToken cancellationToken)
        {
            try
            {
                var count = await _dailyPickService.GenerateDailyPicksAsync(cancellationToken);
                TempData["PicksMessage"] = $"Generated or refreshed {count} picks.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Manual daily pick generation failed.");
                TempData["PicksMessage"] = "Pick generation failed. Check the logs for details.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}

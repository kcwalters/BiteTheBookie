using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

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
        private readonly ChatClient? _chatClient;

        public DailyPicksController(
            IDailyPickService dailyPickService,
            ILogger<DailyPicksController> logger,
            Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager,
            ChatClient? chatClient = null)
        {
            _dailyPickService = dailyPickService;
            _logger = logger;
            _userManager = userManager;
            _chatClient = chatClient;
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
                if (User.IsInRole("Admin") || User.IsInRole("Pro") || User.IsInRole("AllAccess"))
                {
                    isPaid = true;
                }
                else
                {
                    var user = await _userManager.GetUserAsync(User);
                    isPaid = user?.IsPro == true
                        || user?.SubscriptionTier == SubscriptionTier.AllAccess
                        || user?.SubscriptionTier == SubscriptionTier.Admin;
                }
            }

            ViewBag.IsPaid = isPaid;
            ViewBag.AiConfigured = _chatClient != null;
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
                if (_chatClient == null)
                {
                    _logger.LogWarning("Manual daily pick generation requested but Azure OpenAI ChatClient is not configured.");
                    TempData["PicksMessage"] = "Pick generation is unavailable: Azure OpenAI is not configured (set AzureOpenAI:Endpoint, ApiKey, and DeploymentName).";
                    return RedirectToAction(nameof(Index));
                }

                var count = await _dailyPickService.GenerateDailyPicksAsync(cancellationToken);
                TempData["PicksMessage"] = count > 0
                    ? $"Generated or refreshed {count} picks."
                    : "Generation ran but produced 0 picks (no upcoming games found in the look-ahead window).";
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
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Models.Fantasy;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    /// <summary>
    /// Daily Fantasy Football (DFS) salary-cap contests. Browsing and lineup building require a
    /// Pro/AllAccess membership; contest creation and scoring are admin-only. All player, slate,
    /// and scoring data comes from the real ESPN API via <see cref="INflFantasyDataService"/>.
    /// </summary>
    [Authorize(Policy = "ProOnly")]
    public class FantasyController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly INflFantasyDataService _dataService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<FantasyController> _logger;

        public FantasyController(
            ApplicationDbContext db,
            INflFantasyDataService dataService,
            UserManager<ApplicationUser> userManager,
            ILogger<FantasyController> logger)
        {
            _db = db;
            _dataService = dataService;
            _userManager = userManager;
            _logger = logger;
        }

        /// <summary>Fantasy lobby: lists upcoming and recently scored contests.</summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var contests = await _db.FantasyContests
                .OrderByDescending(c => c.LockTime)
                .Take(25)
                .ToListAsync();

            return View(new FantasyLobbyViewModel { Contests = contests });
        }

        /// <summary>Lineup builder for a single contest.</summary>
        [HttpGet]
        public async Task<IActionResult> Contest(int id)
        {
            var contest = await _db.FantasyContests
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contest is null)
                return NotFound();

            var userId = _userManager.GetUserId(User);
            var myEntry = await _db.FantasyEntries
                .Include(e => e.Slots)
                .FirstOrDefaultAsync(e => e.FantasyContestId == id && e.UserId == userId);

            var vm = new FantasyContestViewModel
            {
                Contest = contest,
                Players = contest.Players.OrderBy(p => p.Position).ThenByDescending(p => p.Salary).ToList(),
                MyEntry = myEntry
            };

            return View(vm);
        }

        /// <summary>Submit or replace a lineup for a contest.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(FantasyEntrySubmission submission)
        {
            var contest = await _db.FantasyContests
                .Include(c => c.Players)
                .FirstOrDefaultAsync(c => c.Id == submission.ContestId);

            if (contest is null)
                return NotFound();

            if (DateTime.UtcNow >= contest.LockTime)
            {
                TempData["FantasyError"] = "This contest is locked; lineups can no longer be submitted.";
                return RedirectToAction(nameof(Contest), new { id = submission.ContestId });
            }

            var (isValid, error, slots, totalSalary) = ValidateLineup(contest, submission);
            if (!isValid)
            {
                TempData["FantasyError"] = error;
                return RedirectToAction(nameof(Contest), new { id = submission.ContestId });
            }

            var userId = _userManager.GetUserId(User)!;
            var existing = await _db.FantasyEntries
                .Include(e => e.Slots)
                .FirstOrDefaultAsync(e => e.FantasyContestId == contest.Id && e.UserId == userId);

            if (existing is not null)
            {
                _db.FantasyEntrySlots.RemoveRange(existing.Slots);
                existing.Slots = slots;
                existing.TotalSalary = totalSalary;
                existing.SubmittedAt = DateTime.UtcNow;
                existing.TotalPoints = null;
            }
            else
            {
                _db.FantasyEntries.Add(new FantasyEntry
                {
                    FantasyContestId = contest.Id,
                    UserId = userId,
                    TotalSalary = totalSalary,
                    Slots = slots
                });
            }

            await _db.SaveChangesAsync();
            TempData["FantasyMessage"] = "Your lineup has been saved.";
            return RedirectToAction(nameof(Contest), new { id = submission.ContestId });
        }

        /// <summary>Leaderboard for a contest (most useful once scored).</summary>
        [HttpGet]
        public async Task<IActionResult> Leaderboard(int id)
        {
            var contest = await _db.FantasyContests
                .Include(c => c.Entries)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contest is null)
                return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var userIds = contest.Entries.Select(e => e.UserId).Distinct().ToList();
            var users = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? "Player");

            var rows = contest.Entries
                .OrderByDescending(e => e.TotalPoints ?? decimal.MinValue)
                .ThenBy(e => e.SubmittedAt)
                .Select((e, i) => new FantasyLeaderboardRow
                {
                    Rank = i + 1,
                    UserName = users.TryGetValue(e.UserId, out var name) ? name : "Player",
                    TotalPoints = e.TotalPoints,
                    TotalSalary = e.TotalSalary,
                    IsCurrentUser = e.UserId == currentUserId
                })
                .ToList();

            return View(new FantasyLeaderboardViewModel { Contest = contest, Rows = rows });
        }

        // -----------------------------------------------------------------
        // Admin: create a contest from a real slate and score completed ones.
        // -----------------------------------------------------------------

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public IActionResult Create()
        {
            return View(model: DateTime.Today);
        }

        /// <summary>
        /// Returns the real ESPN NFL games for a given date as JSON, used to populate the
        /// slate-games dropdown on the contest creation page.
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SlateGames(DateTime date)
        {
            var slate = await _dataService.GetSlateGamesAsync(date);
            var games = slate.Select(g => new
            {
                gameId = g.GameId,
                label = $"{g.AwayTeamName} @ {g.HomeTeamName} — {g.GameTime.ToLocalTime():h:mm tt}",
                kickoff = g.GameTime.ToLocalTime().ToString("ddd MMM d, h:mm tt"),
                status = g.Status
            });

            return Json(games);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DateTime slateDate, string? name)
        {
            var slate = await _dataService.GetSlateGamesAsync(slateDate);
            if (slate.Count == 0)
            {
                TempData["FantasyError"] = "No NFL games found for that date. Pick a slate date with scheduled games.";
                return View(model: slateDate);
            }

            var pool = await _dataService.BuildPlayerPoolAsync(slateDate);
            if (pool.Count == 0)
            {
                TempData["FantasyError"] = "Could not build a player pool from the ESPN API for that slate.";
                return View(model: slateDate);
            }

            var contest = new FantasyContest
            {
                Name = string.IsNullOrWhiteSpace(name)
                    ? $"NFL DFS - {slateDate:ddd MMM d, yyyy}"
                    : name.Trim(),
                League = "NFL",
                SlateKey = slateDate.ToString("yyyyMMdd"),
                SalaryCap = FantasyRoster.DefaultSalaryCap,
                LockTime = slate.Min(g => g.GameTime),
                Players = pool.ToList()
            };

            _db.FantasyContests.Add(contest);
            await _db.SaveChangesAsync();

            TempData["FantasyMessage"] = $"Created contest with {pool.Count} players across {slate.Count} games.";
            return RedirectToAction(nameof(Contest), new { id = contest.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Score(int id)
        {
            var contest = await _db.FantasyContests
                .Include(c => c.Players)
                .Include(c => c.Entries)
                    .ThenInclude(e => e.Slots)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (contest is null)
                return NotFound();

            var actualPoints = await _dataService.GetActualFantasyPointsAsync(contest);

            // Apply points to the player pool.
            foreach (var player in contest.Players)
            {
                if (actualPoints.TryGetValue(player.PlayerId, out var pts))
                    player.FantasyPoints = pts;
            }

            // Roll up each entry's total.
            var playersById = contest.Players.ToDictionary(p => p.Id);
            foreach (var entry in contest.Entries)
            {
                decimal total = 0m;
                foreach (var slot in entry.Slots)
                {
                    if (playersById.TryGetValue(slot.FantasyPlayerId, out var p) && p.FantasyPoints.HasValue)
                        total += p.FantasyPoints.Value;
                }
                entry.TotalPoints = total;
            }

            contest.IsScored = true;
            await _db.SaveChangesAsync();

            TempData["FantasyMessage"] = "Contest scored from live box-score data.";
            return RedirectToAction(nameof(Leaderboard), new { id });
        }

        // -----------------------------------------------------------------
        // Lineup validation
        // -----------------------------------------------------------------
        private static (bool IsValid, string Error, List<FantasyEntrySlot> Slots, int TotalSalary) ValidateLineup(
            FantasyContest contest, FantasyEntrySubmission submission)
        {
            var slots = new List<FantasyEntrySlot>();
            var usedPlayerIds = new HashSet<int>();
            int totalSalary = 0;
            var playersById = contest.Players.ToDictionary(p => p.Id);

            foreach (var (slotLabel, _) in FantasyRoster.Slots)
            {
                if (!submission.Selections.TryGetValue(slotLabel, out var playerId) || playerId <= 0)
                    return (false, $"Please select a player for the {slotLabel} slot.", slots, 0);

                if (!playersById.TryGetValue(playerId, out var player))
                    return (false, $"Invalid player selected for {slotLabel}.", slots, 0);

                if (!FantasyRoster.IsEligible(slotLabel, player.Position))
                    return (false, $"{player.PlayerName} is not eligible for the {slotLabel} slot.", slots, 0);

                if (!usedPlayerIds.Add(playerId))
                    return (false, $"{player.PlayerName} is used in more than one slot.", slots, 0);

                totalSalary += player.Salary;
                slots.Add(new FantasyEntrySlot { SlotLabel = slotLabel, FantasyPlayerId = playerId });
            }

            if (totalSalary > contest.SalaryCap)
                return (false, $"Lineup salary ${totalSalary:N0} exceeds the cap of ${contest.SalaryCap:N0}.", slots, totalSalary);

            return (true, string.Empty, slots, totalSalary);
        }
    }
}

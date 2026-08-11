using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminVideosController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminVideosController> _logger;

        public AdminVideosController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminVideosController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var videos = await _db.SiteVideos
                .OrderBy(v => v.SortOrder)
                .ThenByDescending(v => v.CreatedAt)
                .Select(v => new VideoListItemViewModel
                {
                    Id = v.Id,
                    Title = v.Title,
                    Description = v.Description,
                    YouTubeId = v.YouTubeId,
                    Category = v.Category,
                    IsPublished = v.IsPublished,
                    IsFeatured = v.IsFeatured,
                    SortOrder = v.SortOrder,
                    EnteredBy = v.EnteredBy,
                    CreatedAt = v.CreatedAt
                })
                .ToListAsync();

            var vm = new AdminVideosListViewModel { Videos = videos };
            return View(vm);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new VideoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VideoViewModel vm)
        {
            var videoId = YouTubeHelper.ExtractVideoId(vm.YouTubeUrl);
            if (videoId == null)
            {
                ModelState.AddModelError(nameof(vm.YouTubeUrl),
                    "Could not read a YouTube video ID from that value. Paste a full YouTube link or the 11-character video ID.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);

            if (vm.IsFeatured)
            {
                await ClearExistingFeaturedAsync();
            }

            var video = new SiteVideo
            {
                Title = vm.Title,
                Description = vm.Description,
                YouTubeUrl = vm.YouTubeUrl,
                YouTubeId = videoId!,
                Category = vm.Category,
                IsPublished = vm.IsPublished,
                IsFeatured = vm.IsFeatured,
                SortOrder = vm.SortOrder,
                EnteredBy = user?.FirstName ?? User.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow
            };

            _db.SiteVideos.Add(video);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Admin {User} added video {VideoId} ({Title})",
                video.EnteredBy, video.YouTubeId, video.Title);

            TempData["SuccessMessage"] = $"Video \"{vm.Title}\" saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var video = await _db.SiteVideos.FindAsync(id);
            if (video == null) return NotFound();

            var vm = new VideoViewModel
            {
                Id = video.Id,
                Title = video.Title,
                Description = video.Description,
                YouTubeUrl = video.YouTubeUrl,
                Category = video.Category,
                IsPublished = video.IsPublished,
                IsFeatured = video.IsFeatured,
                SortOrder = video.SortOrder
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(VideoViewModel vm)
        {
            var videoId = YouTubeHelper.ExtractVideoId(vm.YouTubeUrl);
            if (videoId == null)
            {
                ModelState.AddModelError(nameof(vm.YouTubeUrl),
                    "Could not read a YouTube video ID from that value. Paste a full YouTube link or the 11-character video ID.");
            }

            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var video = await _db.SiteVideos.FindAsync(vm.Id);
            if (video == null) return NotFound();

            if (vm.IsFeatured && !video.IsFeatured)
            {
                await ClearExistingFeaturedAsync();
            }

            video.Title = vm.Title;
            video.Description = vm.Description;
            video.YouTubeUrl = vm.YouTubeUrl;
            video.YouTubeId = videoId!;
            video.Category = vm.Category;
            video.IsPublished = vm.IsPublished;
            video.IsFeatured = vm.IsFeatured;
            video.SortOrder = vm.SortOrder;
            video.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Video updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var video = await _db.SiteVideos.FindAsync(id);
            if (video == null) return NotFound();

            _db.SiteVideos.Remove(video);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Video deleted.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Ensures only one video is flagged as featured at a time.
        /// </summary>
        private async Task ClearExistingFeaturedAsync()
        {
            var currentlyFeatured = await _db.SiteVideos
                .Where(v => v.IsFeatured)
                .ToListAsync();

            foreach (var v in currentlyFeatured)
            {
                v.IsFeatured = false;
            }
        }
    }
}

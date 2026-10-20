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
    public class AdminContentBlocksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<AdminContentBlocksController> _logger;

        public AdminContentBlocksController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<AdminContentBlocksController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var blocks = await _db.ContentBlocks
                .OrderBy(b => b.PageKey)
                .ThenBy(b => b.RegionKey)
                .ThenBy(b => b.SortOrder)
                .Select(b => new ContentBlockListItemViewModel
                {
                    Id = b.Id,
                    Name = b.Name,
                    PageKey = b.PageKey,
                    RegionKey = b.RegionKey,
                    Sport = b.Sport,
                    Team = b.Team,
                    IsPublished = b.IsPublished,
                    SortOrder = b.SortOrder,
                    EnteredBy = b.EnteredBy,
                    CreatedAt = b.CreatedAt
                })
                .ToListAsync();

            var vm = new AdminContentBlocksListViewModel { Blocks = blocks };
            return View(vm);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new ContentBlockViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ContentBlockViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);

            var block = new ContentBlock
            {
                Name = vm.Name,
                PageKey = vm.PageKey.Trim(),
                RegionKey = vm.RegionKey.Trim(),
                Sport = string.IsNullOrWhiteSpace(vm.Sport) ? null : vm.Sport.Trim(),
                Team = string.IsNullOrWhiteSpace(vm.Team) ? null : vm.Team.Trim(),
                HtmlContent = vm.HtmlContent,
                IsPublished = vm.IsPublished,
                SortOrder = vm.SortOrder,
                EnteredBy = user?.FirstName ?? User.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow
            };

            _db.ContentBlocks.Add(block);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Admin {User} added content block {Id} ({Name}) for {Page}/{Region}",
                block.EnteredBy, block.Id, block.Name, block.PageKey, block.RegionKey);

            TempData["SuccessMessage"] = $"Content block \"{vm.Name}\" saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var block = await _db.ContentBlocks.FindAsync(id);
            if (block == null) return NotFound();

            var vm = new ContentBlockViewModel
            {
                Id = block.Id,
                Name = block.Name,
                PageKey = block.PageKey,
                RegionKey = block.RegionKey,
                Sport = block.Sport,
                Team = block.Team,
                HtmlContent = block.HtmlContent,
                IsPublished = block.IsPublished,
                SortOrder = block.SortOrder
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ContentBlockViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var block = await _db.ContentBlocks.FindAsync(vm.Id);
            if (block == null) return NotFound();

            block.Name = vm.Name;
            block.PageKey = vm.PageKey.Trim();
            block.RegionKey = vm.RegionKey.Trim();
            block.Sport = string.IsNullOrWhiteSpace(vm.Sport) ? null : vm.Sport.Trim();
            block.Team = string.IsNullOrWhiteSpace(vm.Team) ? null : vm.Team.Trim();
            block.HtmlContent = vm.HtmlContent;
            block.IsPublished = vm.IsPublished;
            block.SortOrder = vm.SortOrder;
            block.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Content block updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var block = await _db.ContentBlocks.FindAsync(id);
            if (block == null) return NotFound();

            _db.ContentBlocks.Remove(block);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Content block deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}

using BiteTheBookie.Data;
using BiteTheBookie.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    public class ExpertsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExpertsController> _logger;

        public ExpertsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<ExpertsController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: Experts
        public async Task<IActionResult> Index()
        {
            try
            {
                var posts = await _db.ExpertPosts
                    .Where(p => p.IsPublished)
                    .Include(p => p.Author)
                    .OrderByDescending(p => p.CreatedAt)
                    .ToListAsync();

                return View(posts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expert posts");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: Experts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var post = await _db.ExpertPosts
                    .Include(p => p.Author)
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsPublished);

                if (post == null)
                {
                    return NotFound();
                }

                // Increment view count
                post.ViewCount++;
                _db.ExpertPosts.Update(post);
                await _db.SaveChangesAsync();

                return View(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expert post details");
                return RedirectToAction("Error", "Home");
            }
        }

        // GET: Experts/Create
        [Authorize(Roles = "Expert,Admin")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Experts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Expert,Admin")]
        public async Task<IActionResult> Create([Bind("Title,Content,Summary,Category")] ExpertPost post)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await _userManager.GetUserAsync(User);
                    if (user == null)
                    {
                        return Unauthorized();
                    }

                    post.AuthorId = user.Id;
                    post.CreatedAt = DateTime.UtcNow;
                    post.IsPublished = true;

                    _db.Add(post);
                    await _db.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Article created successfully!";
                    return RedirectToAction(nameof(Details), new { id = post.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating expert post");
                ModelState.AddModelError("", "An error occurred while creating the article.");
            }

            return View(post);
        }

        // GET: Experts/Edit/5
        [Authorize(Roles = "Expert,Admin")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            try
            {
                var post = await _db.ExpertPosts.FindAsync(id);

                if (post == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Only allow authors and admins to edit
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (post.AuthorId != user.Id && !isAdmin)
                {
                    return Forbid();
                }

                return View(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading expert post for edit");
                return RedirectToAction("Error", "Home");
            }
        }

        // POST: Experts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Expert,Admin")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Content,Summary,Category,IsPublished")] ExpertPost post)
        {
            if (id != post.Id)
            {
                return NotFound();
            }

            try
            {
                var existingPost = await _db.ExpertPosts.FindAsync(id);
                if (existingPost == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Only allow authors and admins to edit
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (existingPost.AuthorId != user.Id && !isAdmin)
                {
                    return Forbid();
                }

                if (ModelState.IsValid)
                {
                    try
                    {
                        existingPost.Title = post.Title;
                        existingPost.Content = post.Content;
                        existingPost.Summary = post.Summary;
                        existingPost.Category = post.Category;
                        existingPost.IsPublished = post.IsPublished;
                        existingPost.UpdatedAt = DateTime.UtcNow;

                        _db.Update(existingPost);
                        await _db.SaveChangesAsync();

                        TempData["SuccessMessage"] = "Article updated successfully!";
                        return RedirectToAction(nameof(Details), new { id = existingPost.Id });
                    }
                    catch (DbUpdateConcurrencyException ex)
                    {
                        _logger.LogError(ex, "Concurrency error updating expert post");
                        if (!ExpertPostExists(post.Id))
                        {
                            return NotFound();
                        }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating expert post");
                ModelState.AddModelError("", "An error occurred while updating the article.");
            }

            return View(post);
        }

        // POST: Experts/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Expert,Admin")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var post = await _db.ExpertPosts.FindAsync(id);
                if (post == null)
                {
                    return NotFound();
                }

                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized();
                }

                // Only allow authors and admins to delete
                var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
                if (post.AuthorId != user.Id && !isAdmin)
                {
                    return Forbid();
                }

                _db.ExpertPosts.Remove(post);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Article deleted successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting expert post");
                TempData["ErrorMessage"] = "An error occurred while deleting the article.";
                return RedirectToAction(nameof(Index));
            }
        }

        private bool ExpertPostExists(int id)
        {
            return _db.ExpertPosts.Any(e => e.Id == id);
        }
    }
}

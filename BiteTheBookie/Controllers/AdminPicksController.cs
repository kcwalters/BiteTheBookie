using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    // Legacy route shim: the admin picks management was consolidated into
    // ExpertPicksController. This controller preserves existing /AdminPicks
    // URLs/bookmarks by redirecting to the corresponding ExpertPicks actions.
    [Authorize(Roles = "Admin")]
    public class AdminPicksController : Controller
    {
        [HttpGet]
        public IActionResult Index(string league = "NBA")
            => RedirectToActionPermanent("Manage", "ExpertPicks", new { league });

        [HttpGet]
        public IActionResult Manage(string league = "NBA")
            => RedirectToActionPermanent("Manage", "ExpertPicks", new { league });

        [HttpGet]
        public IActionResult Create(string league = "NBA")
            => RedirectToActionPermanent("Create", "ExpertPicks", new { league });

        [HttpGet]
        public IActionResult Edit(int id)
            => RedirectToActionPermanent("Edit", "ExpertPicks", new { id });
    }
}

using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.ViewModels;

namespace BiteTheBookie.Controllers
{
    public class AdminPicksController : Controller
    {
        // GET: AdminPicks
        public IActionResult Index()
        {
            var viewModel = new AdminPicksListViewModel();
            // Populate the viewModel as needed
            return View(viewModel);
        }

        // GET: AdminPicks/Create
        public IActionResult Create()
        {
            var model = new AdminPickViewModel();
            // Optionally, initialize model properties here if necessary
            return View(model);
        }

        // POST: AdminPicks/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(AdminPickViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Save model
                return RedirectToAction("Index");
            }
            return View(model);
        }

        // Any other actions related to AdminPicks can be added here
    }
}
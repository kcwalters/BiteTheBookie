using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.ViewModels;

namespace BiteTheBookie.Controllers
{
    public class PicksController : Controller
    {
        public IActionResult Index()
        {
            // Default view shows NBA picks
            return View();
        }

        public IActionResult NBA()
        {
            return View();
        }

        public IActionResult NFL()
        {
            return View();
        }

        public IActionResult NHL()
        {
            return View();
        }

        public IActionResult CBB()
        {
            return View();
        }

        public IActionResult MLB()
        {
            return View();
        }
    }
}

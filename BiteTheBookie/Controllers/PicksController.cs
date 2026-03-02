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

        public IActionResult Detail(string gameId)
        {
            // In a real app, you would fetch game data based on gameId
            // For now, we'll pass the gameId to the view
            ViewBag.GameId = gameId;
            return View();
        }
    }
}


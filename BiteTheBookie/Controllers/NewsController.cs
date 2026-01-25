using Microsoft.AspNetCore.Mvc;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Controllers
{
    public class NewsController : Controller
    {
        private readonly INewsService _service;
        public NewsController(INewsService service) => _service = service;

        public async Task<IActionResult> Index(int count = 10)
            => View(await _service.GetLatestNewsAsync(count));

        public async Task<IActionResult> Feed(int count = 5)
            => PartialView("Partials/_NewsFeed", await _service.GetLatestNewsAsync(count));

        public async Task<IActionResult> Details(int id)
        {
            var item = await _service.GetNewsByIdAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
    }
}

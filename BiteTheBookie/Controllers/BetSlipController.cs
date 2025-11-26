using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace BiteTheBookie.Controllers
{
    namespace BiteTheBookie.Controllers
    {
        public class BetSlipController : Controller
        {
            private readonly IBetSlipService _service;
            public BetSlipController(IBetSlipService service) => _service = service;

            public async Task<IActionResult> ViewSlip()
                => PartialView("_BetSlipView", await _service.GetBetSlipAsync());

            [HttpPost]
            public async Task<IActionResult> Add(AddBetRequest req)
            {
                if (!ModelState.IsValid) return BadRequest();
                await _service.AddBetAsync(req);
                return Json(new { success = true });
            }

            [HttpPost]
            public async Task<IActionResult> Remove(int gameId)
            {
                await _service.RemoveBetAsync(gameId);
                return Json(new { success = true });
            }

            [HttpPost]
            public async Task<IActionResult> Clear()
            {
                await _service.ClearAsync();
                return Json(new { success = true });
            }
        }
    }

}

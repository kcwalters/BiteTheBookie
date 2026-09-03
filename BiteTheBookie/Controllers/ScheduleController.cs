using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BiteTheBookie.Controllers
{
    /// <summary>
    /// On-site full schedule browser. Users pick a league and date and see the upcoming
    /// games for that date, sourced from the real ESPN scoreboard API.
    /// </summary>
    public class ScheduleController : Controller
    {
        private readonly ILeagueScheduleService _scheduleService;

        private static readonly Dictionary<string, string> LeagueNames =
            new(StringComparer.OrdinalIgnoreCase)
        {
            ["NFL"] = "NFL",
            ["NBA"] = "NBA",
            ["NHL"] = "NHL",
            ["MLB"] = "MLB",
            ["CFB"] = "College Football",
            ["CBB"] = "College Basketball",
        };

        public ScheduleController(ILeagueScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string league = "NFL", DateTime? date = null, CancellationToken cancellationToken = default)
        {
            var code = (league ?? "NFL").Trim().ToUpperInvariant();
            if (!_scheduleService.IsSupported(code))
                code = "NFL";

            var selectedDate = (date ?? DateTime.Today).Date;

            var model = new ScheduleViewModel
            {
                LeagueCode = code,
                LeagueName = LeagueNames.TryGetValue(code, out var name) ? name : code,
                SelectedDate = selectedDate
            };

            try
            {
                model.Games = await _scheduleService.GetGamesForDateAsync(code, selectedDate, cancellationToken);
            }
            catch
            {
                model.ErrorMessage = $"The {model.LeagueName} schedule is unavailable right now. Please try again.";
            }

            return View(model);
        }
    }
}

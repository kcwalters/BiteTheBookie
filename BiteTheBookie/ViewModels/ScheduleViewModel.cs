using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Backing model for the on-site Full Schedule page. Lets the user pick a league and
    /// date and shows the upcoming games for that date, sourced from the real ESPN API.
    /// </summary>
    public class ScheduleViewModel
    {
        public string LeagueCode { get; set; } = "NFL";
        public string LeagueName { get; set; } = "NFL";

        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string SelectedDateDisplay => SelectedDate.ToString("dddd, MMMM d, yyyy");

        public List<NBAGameMatchup> Games { get; set; } = new();

        public string? ErrorMessage { get; set; }

        /// <summary>Supported leagues for the picker (code -> display name).</summary>
        public IReadOnlyList<(string Code, string Name)> Leagues { get; } = new List<(string, string)>
        {
            ("NFL", "NFL"),
            ("NBA", "NBA"),
            ("NHL", "NHL"),
            ("MLB", "MLB"),
            ("CFB", "College Football"),
            ("CBB", "College Basketball"),
        };
    }
}

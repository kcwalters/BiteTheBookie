using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Backing model for a full-page, per-league scoreboard (all sports).
    /// Games are normalized into the shared <see cref="NBAGameMatchup"/> shape.
    /// </summary>
    public class ScoresPageViewModel
    {
        public string LeagueName { get; set; } = string.Empty;
        public string LeagueCode { get; set; } = string.Empty;
        public string LeagueLogo { get; set; } = string.Empty;

        // Routing targets for the subnav.
        public string TeamController { get; set; } = string.Empty;
        public string OddsController { get; set; } = "Odds";
        public string OddsAction { get; set; } = string.Empty;
        public string ExpertPicksLeague { get; set; } = string.Empty;

        public List<NBAGameMatchup> Games { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string SelectedDateDisplay => SelectedDate.ToString("dddd, MMMM d, yyyy");

        // Every league the scores page can switch between.
        public IReadOnlyList<(string Code, string Name)> AvailableLeagues { get; } = new List<(string, string)>
        {
            ("NFL", "NFL"),
            ("NBA", "NBA"),
            ("MLB", "MLB"),
            ("NHL", "NHL"),
            ("CFB", "College Football"),
            ("CBB", "College Basketball"),
        };

        public IEnumerable<NBAGameMatchup> LiveGames => Games.Where(g => g.IsLive);
        public IEnumerable<NBAGameMatchup> ScheduledGames => Games.Where(g => g.IsScheduled);
        public IEnumerable<NBAGameMatchup> FinalGames => Games.Where(g => g.IsFinal);
    }
}

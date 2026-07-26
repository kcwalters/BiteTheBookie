using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class PicksIndexViewModel
    {
        public string League { get; set; } = "NBA";
        public List<NBAGameMatchup> Games { get; set; } = new();
        public string? ErrorMessage { get; set; }

        /// <summary>The date whose schedule is being displayed (defaults to today).</summary>
        public DateTime SelectedDate { get; set; } = DateTime.Today;

        public string SelectedDateInput => SelectedDate.ToString("yyyy-MM-dd");
        public string SelectedDateDisplay => SelectedDate.ToString("dddd, MMMM d, yyyy");
        public string PreviousDateInput => SelectedDate.AddDays(-1).ToString("yyyy-MM-dd");
        public string NextDateInput => SelectedDate.AddDays(1).ToString("yyyy-MM-dd");
        public bool IsToday => SelectedDate.Date == DateTime.Today;

        public IEnumerable<NBAGameMatchup> ScheduledGames => Games.Where(g => g.IsScheduled);
        public IEnumerable<NBAGameMatchup> LiveGames => Games.Where(g => g.IsLive);
        public IEnumerable<NBAGameMatchup> FinalGames => Games.Where(g => g.IsFinal);
    }
}


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

        /// <summary>Friendly league name for headings.</summary>
        public string LeagueDisplayName => League.ToUpperInvariant() switch
        {
            "NBA" => "NBA",
            "NFL" => "NFL",
            "NHL" => "NHL",
            "MLB" => "MLB",
            "CFB" => "College Football",
            "CBB" => "College Basketball",
            _ => League
        };

        /// <summary>League logo shown next to the heading.</summary>
        public string LeagueLogo => League.ToUpperInvariant() switch
        {
            "NBA" => "https://a.espncdn.com/i/teamlogos/leagues/500/nba.png",
            "NFL" => "https://a.espncdn.com/i/teamlogos/leagues/500/nfl.png",
            "NHL" => "https://a.espncdn.com/i/teamlogos/leagues/500/nhl.png",
            "MLB" => "https://a.espncdn.com/i/teamlogos/leagues/500/mlb.png",
            "CFB" => "https://a.espncdn.com/i/teamlogos/leagues/500/ncaa.png",
            "CBB" => "https://a.espncdn.com/i/teamlogos/leagues/500/ncaa.png",
            _ => string.Empty
        };
    }
}



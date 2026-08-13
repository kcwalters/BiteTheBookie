using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Backing model for the ESPN-style NFL landing page: today's schedule plus the
    /// latest NFL headlines, all sourced from live ESPN feeds.
    /// </summary>
    public class NFLLandingViewModel
    {
        public List<NBAGameMatchup> Games { get; set; } = new();

        public IReadOnlyList<NewsItemViewModel> Headlines { get; set; } = new List<NewsItemViewModel>();

        public string? ErrorMessage { get; set; }

        public DateTime SelectedDate { get; set; } = DateTime.Today;

        public string SelectedDateDisplay => SelectedDate.ToString("dddd, MMMM d, yyyy");

        public string LeagueLogo => "https://a.espncdn.com/i/teamlogos/leagues/500/nfl.png";

        public IEnumerable<NBAGameMatchup> ScheduledGames => Games.Where(g => g.IsScheduled);
        public IEnumerable<NBAGameMatchup> LiveGames => Games.Where(g => g.IsLive);
        public IEnumerable<NBAGameMatchup> FinalGames => Games.Where(g => g.IsFinal);

        /// <summary>The lead headline used for the hero feature block.</summary>
        public NewsItemViewModel? FeaturedHeadline => Headlines.FirstOrDefault();

        /// <summary>Remaining headlines shown in the "Top Headlines" list.</summary>
        public IEnumerable<NewsItemViewModel> SecondaryHeadlines => Headlines.Skip(1);
    }
}

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

        /// <summary>
        /// Upcoming NFL games for the current week (today through the next six days),
        /// sourced live from ESPN. Finished games are excluded.
        /// </summary>
        public List<NBAGameMatchup> UpcomingGames { get; set; } = new();

        /// <summary>Upcoming games grouped by their local calendar day, ordered by start time.</summary>
        public IEnumerable<IGrouping<DateTime, NBAGameMatchup>> UpcomingGamesByDay =>
            UpcomingGames
                .OrderBy(g => g.GameTime)
                .GroupBy(g => g.GameTime.ToLocalTime().Date);

        /// <summary>
        /// Regular-season games grouped by NFL week, ordered for the landing-page scoreboard:
        /// the upcoming (next) NFL week is shown first, then the last official NFL week (the most
        /// recent week that has been played / is in progress), then every remaining week in
        /// descending week-number order. Games within each week are ordered by start time.
        /// </summary>
        public IEnumerable<IGrouping<int, NBAGameMatchup>> GamesByWeek
        {
            get
            {
                var weekGroups = UpcomingGames
                    .OrderBy(g => g.GameTime)
                    .GroupBy(g => g.Week)
                    .ToList();

                if (weekGroups.Count == 0)
                {
                    return weekGroups;
                }

                // Last official NFL week: the most recent week that has already started (final or live).
                // If nothing has started yet, fall back to the earliest scheduled week.
                var startedWeeks = UpcomingGames
                    .Where(g => g.IsFinal || g.IsLive)
                    .Select(g => g.Week)
                    .ToList();

                int lastWeek = startedWeeks.Count > 0
                    ? startedWeeks.Max()
                    : weekGroups.Min(w => w.Key);
                int thisWeek = lastWeek + 1;

                // Priority 0: this (upcoming) week, 1: last official week, 2: everything else
                // (earlier/other weeks) in descending week order.
                return weekGroups
                    .OrderBy(w => w.Key == thisWeek ? 0 : (w.Key == lastWeek ? 1 : 2))
                    .ThenByDescending(w => w.Key);
            }
        }

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

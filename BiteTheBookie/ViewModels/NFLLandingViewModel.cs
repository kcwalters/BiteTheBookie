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
                .GroupBy(g => g.GameDate);

        /// <summary>
        /// Regular-season games grouped by NFL week, ordered for the landing-page scoreboard:
        /// the current NFL week is shown first, followed by future weeks in ascending order,
        /// then past weeks in descending order (e.g. 4, 5, 3, 2, 1). Games within each week are
        /// ordered by start time.
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

                // Current NFL week: the earliest week that still has a game yet to finish
                // (scheduled or live). Once every game has gone final (end of season), fall
                // back to the most recent week.
                var unfinishedWeeks = UpcomingGames
                    .Where(g => !g.IsFinal)
                    .Select(g => g.Week)
                    .ToList();

                int currentWeek = unfinishedWeeks.Count > 0
                    ? unfinishedWeeks.Min()
                    : weekGroups.Max(w => w.Key);

                // Priority 0: current week, 1: future weeks (ascending), 2: past weeks (descending).
                return weekGroups
                    .OrderBy(w => w.Key == currentWeek ? 0 : (w.Key > currentWeek ? 1 : 2))
                    .ThenBy(w => w.Key > currentWeek ? w.Key : -w.Key);
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

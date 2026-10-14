using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Shared backing model for the ESPN-style league landing pages (MLB, NBA, NHL,
    /// CBB, CFB). Mirrors <see cref="NFLLandingViewModel"/> but carries the per-league
    /// metadata so a single shared view can render every league identically.
    /// </summary>
    public class LeagueHomeViewModel
    {
        // League identity / branding
        public string LeagueName { get; set; } = string.Empty;
        public string LeagueLogo { get; set; } = string.Empty;

        // Routing targets used by the shared view's subnav and quick links.
        public string TeamController { get; set; } = string.Empty;   // controller hosting Team/Teams actions
        public string TeamsAction { get; set; } = "Teams";
        public string GameCenterController { get; set; } = "Picks";
        public string GameCenterAction { get; set; } = string.Empty;
        public string OddsController { get; set; } = "Odds";
        public string OddsAction { get; set; } = string.Empty;
        public string ExpertPicksLeague { get; set; } = string.Empty;

        // External ESPN quick links
        public string EspnScheduleUrl { get; set; } = string.Empty;
        public string EspnStandingsUrl { get; set; } = string.Empty;

        // Data
        public List<NBAGameMatchup> Games { get; set; } = new();

        /// <summary>
        /// Upcoming games for the current week (today through the next six days),
        /// sourced live from ESPN. Finished games are excluded.
        /// </summary>
        public List<NBAGameMatchup> UpcomingGames { get; set; } = new();

        /// <summary>Upcoming games grouped by their local calendar day, ordered by start time.</summary>
        public IEnumerable<IGrouping<DateTime, NBAGameMatchup>> UpcomingGamesByDay =>
            UpcomingGames
                .OrderBy(g => g.GameTime)
                .GroupBy(g => g.GameDate);

        /// <summary>
        /// Games grouped by their start day (league/US Eastern time), ordered for the landing-page
        /// scoreboard: today first, then upcoming days in ascending order, then past days in
        /// descending order. Games within each day are ordered by start time.
        /// </summary>
        public IEnumerable<IGrouping<DateTime, NBAGameMatchup>> GamesByDay
        {
            get
            {
                var today = NBAGameMatchup.LeagueToday;
                return UpcomingGames
                    .OrderBy(g => g.GameTime)
                    .GroupBy(g => g.GameDate)
                    // Priority 0: today, 1: future days (ascending), 2: past days (descending).
                    .OrderBy(grp => grp.Key == today ? 0 : (grp.Key > today ? 1 : 2))
                    .ThenBy(grp => grp.Key >= today ? grp.Key.Ticks : -grp.Key.Ticks);
            }
        }

        public IReadOnlyList<NewsItemViewModel> Headlines { get; set; } = new List<NewsItemViewModel>();

        public string? ErrorMessage { get; set; }

        public DateTime SelectedDate { get; set; } = DateTime.Today;
        public string SelectedDateDisplay => SelectedDate.ToString("dddd, MMMM d, yyyy");

        public IEnumerable<NBAGameMatchup> ScheduledGames => Games.Where(g => g.IsScheduled);
        public IEnumerable<NBAGameMatchup> LiveGames => Games.Where(g => g.IsLive);
        public IEnumerable<NBAGameMatchup> FinalGames => Games.Where(g => g.IsFinal);

        /// <summary>The lead headline used for the hero feature block.</summary>
        public NewsItemViewModel? FeaturedHeadline => Headlines.FirstOrDefault();

        /// <summary>Remaining headlines shown in the "Top Headlines" list.</summary>
        public IEnumerable<NewsItemViewModel> SecondaryHeadlines => Headlines.Skip(1);
    }
}

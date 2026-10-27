namespace BiteTheBookie.Models
{
    /// <summary>
    /// Current-season NFL scoring averages for a single team, derived from the ESPN
    /// standings endpoint. Used to ground game-simulation score predictions in real
    /// production so the AI does not invent inflated totals. Any field may be zero/empty
    /// when ESPN omits it or the season has not started.
    /// </summary>
    public class NflTeamScoringStats
    {
        public string TeamId { get; set; } = string.Empty;
        public string Abbreviation { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Games played so far this season.</summary>
        public int GamesPlayed { get; set; }

        /// <summary>Average points scored per game this season.</summary>
        public double PointsPerGame { get; set; }

        /// <summary>Average points allowed per game this season.</summary>
        public double PointsAllowedPerGame { get; set; }

        /// <summary>Overall win-loss record summary (e.g. "10-4").</summary>
        public string RecordSummary { get; set; } = string.Empty;

        /// <summary>True when at least one game has been played and scoring data is usable.</summary>
        public bool HasData => GamesPlayed > 0 && (PointsPerGame > 0 || PointsAllowedPerGame > 0);
    }
}

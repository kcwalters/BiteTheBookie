namespace BiteTheBookie.Models.Fantasy
{
    /// <summary>
    /// Configurable PPR-style scoring constants used to convert real box-score stats
    /// into fantasy points. Bind from configuration section "Fantasy:Scoring".
    /// </summary>
    public class FantasyScoringOptions
    {
        public const string SectionName = "Fantasy:Scoring";

        public decimal PassingYardsPerPoint { get; set; } = 25m;   // 1 pt per 25 yds
        public decimal PassingTouchdown { get; set; } = 4m;
        public decimal Interception { get; set; } = -2m;
        public decimal RushingYardsPerPoint { get; set; } = 10m;   // 1 pt per 10 yds
        public decimal RushingTouchdown { get; set; } = 6m;
        public decimal ReceivingYardsPerPoint { get; set; } = 10m; // 1 pt per 10 yds
        public decimal ReceivingTouchdown { get; set; } = 6m;
        public decimal Reception { get; set; } = 1m;               // PPR
        public decimal FumbleLost { get; set; } = -2m;
        public decimal TwoPointConversion { get; set; } = 2m;

        // Defense / Special Teams (DST)
        public decimal Sack { get; set; } = 1m;
        public decimal DefInterception { get; set; } = 2m;
        public decimal FumbleRecovery { get; set; } = 2m;
        public decimal DefensiveTouchdown { get; set; } = 6m;
        public decimal Safety { get; set; } = 2m;
        public decimal ShutoutBonus { get; set; } = 10m;           // 0 points allowed
    }

    /// <summary>
    /// Defines the roster slots and salary cap for a DFS lineup.
    /// </summary>
    public static class FantasyRoster
    {
        public const int DefaultSalaryCap = 50000;

        /// <summary>Slot label -> eligible positions for that slot.</summary>
        public static readonly IReadOnlyList<(string Slot, string[] EligiblePositions)> Slots =
            new List<(string, string[])>
            {
                ("QB",   new[] { "QB" }),
                ("RB1",  new[] { "RB" }),
                ("RB2",  new[] { "RB" }),
                ("WR1",  new[] { "WR" }),
                ("WR2",  new[] { "WR" }),
                ("TE",   new[] { "TE" }),
                ("FLEX", new[] { "RB", "WR", "TE" }),
                ("DST",  new[] { "DST" }),
            };

        public static bool IsEligible(string slotLabel, string position)
        {
            foreach (var (slot, positions) in Slots)
            {
                if (string.Equals(slot, slotLabel, StringComparison.OrdinalIgnoreCase))
                {
                    return positions.Contains(position, StringComparer.OrdinalIgnoreCase);
                }
            }
            return false;
        }
    }
}

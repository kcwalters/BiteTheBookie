namespace BiteTheBookie.Models.Fantasy
{
    /// <summary>
    /// A user's submitted lineup for a contest.
    /// </summary>
    public class FantasyEntry
    {
        public int Id { get; set; }

        public int FantasyContestId { get; set; }
        public FantasyContest? FantasyContest { get; set; }

        /// <summary>Owning user's Identity id.</summary>
        public string UserId { get; set; } = string.Empty;

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        /// <summary>Total salary used by the lineup (must be &lt;= cap).</summary>
        public int TotalSalary { get; set; }

        /// <summary>Total fantasy points once the contest is scored.</summary>
        public decimal? TotalPoints { get; set; }

        public ICollection<FantasyEntrySlot> Slots { get; set; } = new List<FantasyEntrySlot>();
    }
}

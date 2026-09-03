using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.Models.Fantasy
{
    /// <summary>
    /// A single Daily Fantasy (DFS) salary-cap contest tied to an NFL weekly slate.
    /// </summary>
    public class FantasyContest
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>League for the slate (NFL for v1).</summary>
        public string League { get; set; } = "NFL";

        /// <summary>ESPN season week identifier for the slate (e.g. 2026-week-5).</summary>
        public string SlateKey { get; set; } = string.Empty;

        /// <summary>Total salary cap available to build a lineup.</summary>
        public int SalaryCap { get; set; } = 50000;

        /// <summary>Entries lock at this time (earliest kickoff of the slate).</summary>
        public DateTime LockTime { get; set; }

        /// <summary>True once player fantasy points have been finalized for the slate.</summary>
        public bool IsScored { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<FantasyPlayer> Players { get; set; } = new List<FantasyPlayer>();
        public ICollection<FantasyEntry> Entries { get; set; } = new List<FantasyEntry>();
    }
}

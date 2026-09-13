using System;

namespace BiteTheBookie.Models
{
    /// <summary>
    /// A single game on a team's season schedule, parsed from the ESPN Site API schedule
    /// endpoint. Score/result fields are only populated for completed games.
    /// </summary>
    public class EspnScheduleEntry
    {
        public DateTime Date { get; set; }
        public string OpponentName { get; set; } = string.Empty;
        public string OpponentLogo { get; set; } = string.Empty;
        public bool IsHome { get; set; }
        public bool IsCompleted { get; set; }
        public string ResultText { get; set; } = string.Empty;   // e.g. "W 24-17", "L 3-5"
        public string StatusDetail { get; set; } = string.Empty;  // e.g. "8:00 PM ET" for upcoming
        public string Venue { get; set; } = string.Empty;

        public string DateDisplay => Date == default ? string.Empty : Date.ToLocalTime().ToString("MMM d");
        public string OpponentDisplay => IsHome ? $"vs {OpponentName}" : $"@ {OpponentName}";
    }
}

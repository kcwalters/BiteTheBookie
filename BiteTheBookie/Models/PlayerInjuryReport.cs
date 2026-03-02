namespace BiteTheBookie.Models
{
    public class PlayerInjuryReport
    {
        public string PlayerName { get; set; } = string.Empty;
        public string TeamCode { get; set; } = string.Empty;
        public string InjuryStatus { get; set; } = string.Empty; // "Out", "Questionable", "Doubtful", "Day-to-Day"
        public string InjuryDescription { get; set; } = string.Empty;
        public DateTime ReportedTime { get; set; }
        public DateTime? EstimatedReturn { get; set; }
    }

    public class TeamInjuryReport
    {
        public string TeamCode { get; set; } = string.Empty;
        public List<PlayerInjuryReport> Injuries { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }
}


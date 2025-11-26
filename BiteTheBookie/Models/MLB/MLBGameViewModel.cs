namespace BiteTheBookie.Models.MLB
{
    public class TeamInfo
    {
        public string Name { get; set; }
        public string LogoUrl { get; set; }
    }

    public class MLBGameViewModel
    {
        public DateTime OffsetDateTime { get; set; }
        public TeamInfo Away { get; set; }
        public TeamInfo Home { get; set; }
        public string Score { get; set; }
        public string Status { get; set; }

        // Helper for display
        public string DisplayTime => OffsetDateTime.ToLocalTime()
                                              .ToString("h:mm tt");
    }
}

namespace BiteTheBookie.Models
{
    public class NBATeamRoster
    {
        public string TeamCode { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public List<NBAPlayer> Players { get; set; } = new();
    }

    public class NBAPlayer
    {
        public string Name { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty;
        public bool IsStarter { get; set; }
    }
}

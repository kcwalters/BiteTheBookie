namespace BiteTheBookie.Models.MLB
{
    // Models/MLB/Game.cs
    public class Game
    {
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public DateTime GameTime { get; set; }
        public string Status { get; set; }        
        public string? HomeTeamLogoUrl { get; set; }
        public string? AwayTeamLogoUrl { get; set; }
    }
}

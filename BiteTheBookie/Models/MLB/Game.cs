namespace BiteTheBookie.Models.MLB
{
    // Models/MLB/Game.cs
    public class Game
    {
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public DateTime GameTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? HomeTeamLogoUrl { get; set; }
        public string? AwayTeamLogoUrl { get; set; }

        /// <summary>Scheduled starting pitcher for the home team (e.g. "Tarik Skubal").</summary>
        public string? HomeProbablePitcher { get; set; }

        /// <summary>Scheduled starting pitcher for the away team (e.g. "Corbin Burnes").</summary>
        public string? AwayProbablePitcher { get; set; }
    }
}

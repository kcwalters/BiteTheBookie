using System.ComponentModel.DataAnnotations;

namespace BiteTheBookie.Models.Fantasy
{
    /// <summary>
    /// A snapshot of a selectable player in a contest's player pool. Salary is derived
    /// from recent real production (ESPN has no DFS salary field). Fantasy points are
    /// populated after games complete.
    /// </summary>
    public class FantasyPlayer
    {
        public int Id { get; set; }

        public int FantasyContestId { get; set; }
        public FantasyContest? FantasyContest { get; set; }

        /// <summary>ESPN athlete id (or team id for DST).</summary>
        public string PlayerId { get; set; } = string.Empty;

        public string PlayerName { get; set; } = string.Empty;

        /// <summary>Roster position: QB, RB, WR, TE, DST.</summary>
        public string Position { get; set; } = string.Empty;

        public string TeamCode { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string OpponentCode { get; set; } = string.Empty;

        /// <summary>ESPN game id this player's slate game maps to.</summary>
        public string GameId { get; set; } = string.Empty;
        public DateTime GameTime { get; set; }

        /// <summary>Derived salary that counts against the cap.</summary>
        public int Salary { get; set; }

        /// <summary>Fantasy points earned once the game is scored.</summary>
        public decimal? FantasyPoints { get; set; }

        public string? ImageUrl { get; set; }
    }
}

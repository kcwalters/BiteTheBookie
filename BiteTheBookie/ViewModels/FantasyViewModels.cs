using BiteTheBookie.Models.Fantasy;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Lists the open/available DFS contests for the fantasy lobby.
    /// </summary>
    public class FantasyLobbyViewModel
    {
        public List<FantasyContest> Contests { get; set; } = new();
    }

    /// <summary>
    /// Lineup-builder view for a single contest, exposing the player pool grouped by position
    /// and the roster slots that must be filled.
    /// </summary>
    public class FantasyContestViewModel
    {
        public FantasyContest Contest { get; set; } = new();

        /// <summary>All selectable players in the contest pool.</summary>
        public List<FantasyPlayer> Players { get; set; } = new();

        /// <summary>Roster slot labels and eligible positions.</summary>
        public IReadOnlyList<(string Slot, string[] EligiblePositions)> Slots => FantasyRoster.Slots;

        /// <summary>The current user's existing entry, if any.</summary>
        public FantasyEntry? MyEntry { get; set; }

        public bool IsLocked => DateTime.UtcNow >= Contest.LockTime;
    }

    /// <summary>
    /// Payload posted when a user submits a lineup: slot label -> selected FantasyPlayer id.
    /// </summary>
    public class FantasyEntrySubmission
    {
        public int ContestId { get; set; }

        /// <summary>Keyed by slot label (QB, RB1, ...) with the chosen FantasyPlayer.Id.</summary>
        public Dictionary<string, int> Selections { get; set; } = new();
    }

    /// <summary>
    /// A scored contest's leaderboard.
    /// </summary>
    public class FantasyLeaderboardViewModel
    {
        public FantasyContest Contest { get; set; } = new();
        public List<FantasyLeaderboardRow> Rows { get; set; } = new();
    }

    public class FantasyLeaderboardRow
    {
        public int Rank { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal? TotalPoints { get; set; }
        public int TotalSalary { get; set; }
        public bool IsCurrentUser { get; set; }
    }
}

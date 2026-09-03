namespace BiteTheBookie.Models.Fantasy
{
    /// <summary>
    /// One roster spot within a fantasy entry, linking a lineup slot to a selected player.
    /// </summary>
    public class FantasyEntrySlot
    {
        public int Id { get; set; }

        public int FantasyEntryId { get; set; }
        public FantasyEntry? FantasyEntry { get; set; }

        /// <summary>Lineup slot label: QB, RB1, RB2, WR1, WR2, TE, FLEX, DST.</summary>
        public string SlotLabel { get; set; } = string.Empty;

        public int FantasyPlayerId { get; set; }
        public FantasyPlayer? FantasyPlayer { get; set; }
    }
}

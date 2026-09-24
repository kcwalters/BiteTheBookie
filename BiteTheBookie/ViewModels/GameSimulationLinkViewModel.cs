namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Backing model for the shared Game Simulation link partial.
    /// Drives whether a functional "Game Simulation" link (paid tiers) or an
    /// upsell "$ Game Simulation" link (anonymous / free tier) is rendered.
    /// </summary>
    public class GameSimulationLinkViewModel
    {
        public string HomeTeam { get; set; } = string.Empty;
        public string AwayTeam { get; set; } = string.Empty;
        public string League { get; set; } = string.Empty;

        /// <summary>
        /// True when the game has already finished. A completed game can't be simulated,
        /// so the link is suppressed.
        /// </summary>
        public bool IsFinal { get; set; }
    }
}

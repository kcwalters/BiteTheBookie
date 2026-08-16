namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Backing model for the shared _SimulationLink partial that renders a
    /// membership-gated "Simulation" link (or an upgrade prompt for non-premium users).
    /// </summary>
    public class SimulationLinkViewModel
    {
        public string GameId { get; set; } = string.Empty;

        public string League { get; set; } = string.Empty;

        public string HomeTeam { get; set; } = string.Empty;

        public string AwayTeam { get; set; } = string.Empty;

        public string? HomeLogo { get; set; }

        public string? AwayLogo { get; set; }

        /// <summary>Optional CSS classes for the premium simulation link button.</summary>
        public string CssClass { get; set; } = "btn btn-sm btn-outline-secondary";

        /// <summary>Text shown on the premium simulation link.</summary>
        public string LinkText { get; set; } = "Simulation";
    }
}

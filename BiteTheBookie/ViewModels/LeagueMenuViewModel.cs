using System.Collections.Generic;

namespace BiteTheBookie.ViewModels
{
    /// <summary>
    /// Data for a single team entry inside a league navigation dropdown.
    /// </summary>
    public class LeagueMenuTeam
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
    }

    /// <summary>
    /// A named group (division/conference) of teams shown as one column in the dropdown.
    /// </summary>
    public class LeagueMenuGroup
    {
        public string Title { get; set; } = string.Empty;
        public List<LeagueMenuTeam> Teams { get; set; } = new();
    }

    /// <summary>
    /// View model driving the shared LeagueMenu view component so every league
    /// dropdown renders with a uniform layout, team logos, and internal team links.
    /// </summary>
    public class LeagueMenuViewModel
    {
        /// <summary>Lowercase league key used for the ticker switcher (e.g. "nfl").</summary>
        public string LeagueKey { get; set; } = string.Empty;

        /// <summary>Short label shown in the nav bar (e.g. "NFL").</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>MVC controller that owns the league's Index/Team actions.</summary>
        public string Controller { get; set; } = string.Empty;

        /// <summary>League icon shown next to the nav label.</summary>
        public string LeagueIcon { get; set; } = string.Empty;

        public List<LeagueMenuGroup> Groups { get; set; } = new();
    }
}

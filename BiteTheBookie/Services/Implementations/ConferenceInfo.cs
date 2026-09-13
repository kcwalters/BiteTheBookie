using System.Collections.Generic;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Curated, static descriptions for the leagues/conferences shown on team landing
    /// pages. Keys are matched case-insensitively against the conference/division name.
    /// </summary>
    public static class ConferenceInfo
    {
        private static readonly Dictionary<string, string> Descriptions =
            new(System.StringComparer.OrdinalIgnoreCase)
        {
            // College Football / Basketball conferences
            ["SEC"] = "The Southeastern Conference (SEC) is widely regarded as the premier college football conference, known for its passionate fan bases, powerhouse programs, and consistent national championship contenders across the South.",
            ["Big Ten"] = "The Big Ten is one of the oldest and most storied college athletic conferences, spanning the Midwest and both coasts, with historic football and basketball traditions and some of the largest stadiums in the sport.",
            ["Big 12"] = "The Big 12 is a high-scoring, offense-driven conference centered in the Plains and Southwest, featuring fierce rivalries and a reputation for wide-open, entertaining football.",
            ["ACC"] = "The Atlantic Coast Conference (ACC) blends strong football with elite basketball tradition, stretching along the Eastern Seaboard and into the South with academically prominent institutions.",
            ["Pac-12"] = "The Pac-12, the 'Conference of Champions,' represents the West Coast with a legacy of athletic excellence across many sports and a distinctive late-night football window.",
            ["Big East"] = "The Big East is a basketball-first conference concentrated in major Eastern cities, renowned for its intense rivalries and a rich history of March Madness success.",
            ["American Athletic"] = "The American Athletic Conference (AAC) is a competitive Group of Five conference that has produced New Year's Six bowl teams and serves as a proving ground for rising programs.",
            ["Conference USA"] = "Conference USA (C-USA) is a geographically broad Group of Five conference featuring developing programs and competitive mid-major matchups.",
            ["Mid-American"] = "The Mid-American Conference (MAC) is known for its weeknight '#MACtion' football and scrappy programs across the Great Lakes region.",
            ["Mountain West"] = "The Mountain West Conference showcases Western programs with strong Group of Five football and a history of bowl and tournament appearances.",
            ["Sun Belt"] = "The Sun Belt Conference is a rising Group of Five league across the South that has increasingly pulled off high-profile upsets in recent seasons.",
            ["Independents"] = "Independent programs compete without conference affiliation, scheduling opponents nationwide — most notably Notre Dame in football.",

            // NFL divisions
            ["AFC East"] = "The AFC East features a competitive mix of franchises along the Northeast and Mid-Atlantic, historically one of the NFL's most closely contested divisions.",
            ["AFC North"] = "The AFC North is known for physical, defense-oriented football and hard-fought rivalries among its Rust Belt and Mid-Atlantic franchises.",
            ["AFC South"] = "The AFC South spans the South and Texas, blending emerging young cores with established playoff contenders.",
            ["AFC West"] = "The AFC West is a high-powered division featuring explosive offenses and perennial Super Bowl contenders across the Western United States.",
            ["NFC East"] = "The NFC East is one of the NFL's oldest and most bitter divisions, packed with historic franchises and marquee rivalries.",
            ["NFC North"] = "The NFC North, often called the 'Black and Blue Division,' is defined by cold-weather, physical football among its Midwestern clubs.",
            ["NFC South"] = "The NFC South is a competitive, offense-friendly division across the Southeast where division titles frequently change hands.",
            ["NFC West"] = "The NFC West is a talent-rich division on the West Coast known for strong defenses and recent Super Bowl representation.",
        };

        /// <summary>
        /// Returns a curated description for the given conference/division name, or a
        /// generic sentence when the name is unknown.
        /// </summary>
        public static string GetDescription(string conference)
        {
            if (!string.IsNullOrWhiteSpace(conference) &&
                Descriptions.TryGetValue(conference.Trim(), out var description))
            {
                return description;
            }

            return string.IsNullOrWhiteSpace(conference)
                ? string.Empty
                : $"{conference} is one of the leagues featured on BiteTheBookie. Explore its teams, matchups, odds, and simulations across the game center.";
        }
    }
}

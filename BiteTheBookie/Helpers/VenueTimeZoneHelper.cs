namespace BiteTheBookie.Helpers
{
    public static class VenueTimeZoneHelper
    {
        private static readonly Dictionary<string, string> _nfl = new(StringComparer.OrdinalIgnoreCase)
        {
            // Eastern
            { "Baltimore Ravens",        "Eastern Standard Time" },
            { "Buffalo Bills",           "Eastern Standard Time" },
            { "Carolina Panthers",       "Eastern Standard Time" },
            { "Cincinnati Bengals",      "Eastern Standard Time" },
            { "Cleveland Browns",        "Eastern Standard Time" },
            { "Detroit Lions",           "Eastern Standard Time" },
            { "Indianapolis Colts",      "Eastern Standard Time" },
            { "Jacksonville Jaguars",    "Eastern Standard Time" },
            { "Miami Dolphins",          "Eastern Standard Time" },
            { "New England Patriots",    "Eastern Standard Time" },
            { "New York Giants",         "Eastern Standard Time" },
            { "New York Jets",           "Eastern Standard Time" },
            { "Philadelphia Eagles",     "Eastern Standard Time" },
            { "Pittsburgh Steelers",     "Eastern Standard Time" },
            { "Tampa Bay Buccaneers",    "Eastern Standard Time" },
            { "Washington Commanders",   "Eastern Standard Time" },
            { "Atlanta Falcons",         "Eastern Standard Time" },
            // Central
            { "Chicago Bears",           "Central Standard Time" },
            { "Dallas Cowboys",          "Central Standard Time" },
            { "Green Bay Packers",       "Central Standard Time" },
            { "Houston Texans",          "Central Standard Time" },
            { "Kansas City Chiefs",      "Central Standard Time" },
            { "Minnesota Vikings",       "Central Standard Time" },
            { "New Orleans Saints",      "Central Standard Time" },
            { "Tennessee Titans",        "Central Standard Time" },
            // Mountain
            { "Denver Broncos",          "Mountain Standard Time" },
            // Pacific
            { "Las Vegas Raiders",       "Pacific Standard Time" },
            { "Los Angeles Chargers",    "Pacific Standard Time" },
            { "Los Angeles Rams",        "Pacific Standard Time" },
            { "San Francisco 49ers",     "Pacific Standard Time" },
            { "Seattle Seahawks",        "Pacific Standard Time" },
            { "Arizona Cardinals",       "US Mountain Standard Time" }, // AZ — no DST
        };

        private static readonly Dictionary<string, string> _nba = new(StringComparer.OrdinalIgnoreCase)
        {
            // Eastern
            { "Atlanta Hawks",           "Eastern Standard Time" },
            { "Boston Celtics",          "Eastern Standard Time" },
            { "Brooklyn Nets",           "Eastern Standard Time" },
            { "Charlotte Hornets",       "Eastern Standard Time" },
            { "Cleveland Cavaliers",     "Eastern Standard Time" },
            { "Detroit Pistons",         "Eastern Standard Time" },
            { "Indiana Pacers",          "Eastern Standard Time" },
            { "Miami Heat",              "Eastern Standard Time" },
            { "New York Knicks",         "Eastern Standard Time" },
            { "Orlando Magic",           "Eastern Standard Time" },
            { "Philadelphia 76ers",      "Eastern Standard Time" },
            { "Toronto Raptors",         "Eastern Standard Time" },
            { "Washington Wizards",      "Eastern Standard Time" },
            // Central
            { "Chicago Bulls",           "Central Standard Time" },
            { "Dallas Mavericks",        "Central Standard Time" },
            { "Houston Rockets",         "Central Standard Time" },
            { "Memphis Grizzlies",       "Central Standard Time" },
            { "Milwaukee Bucks",         "Central Standard Time" },
            { "Minnesota Timberwolves",  "Central Standard Time" },
            { "New Orleans Pelicans",    "Central Standard Time" },
            { "Oklahoma City Thunder",   "Central Standard Time" },
            { "San Antonio Spurs",       "Central Standard Time" },
            // Mountain
            { "Denver Nuggets",          "Mountain Standard Time" },
            { "Utah Jazz",               "Mountain Standard Time" },
            // Pacific
            { "Golden State Warriors",   "Pacific Standard Time" },
            { "Los Angeles Clippers",    "Pacific Standard Time" },
            { "Los Angeles Lakers",      "Pacific Standard Time" },
            { "Portland Trail Blazers",  "Pacific Standard Time" },
            { "Sacramento Kings",        "Pacific Standard Time" },
            { "Phoenix Suns",            "US Mountain Standard Time" }, // AZ — no DST
        };

        private static readonly Dictionary<string, string> _nhl = new(StringComparer.OrdinalIgnoreCase)
        {
            // Eastern
            { "Boston Bruins",           "Eastern Standard Time" },
            { "Buffalo Sabres",          "Eastern Standard Time" },
            { "Carolina Hurricanes",     "Eastern Standard Time" },
            { "Columbus Blue Jackets",   "Eastern Standard Time" },
            { "Detroit Red Wings",       "Eastern Standard Time" },
            { "Florida Panthers",        "Eastern Standard Time" },
            { "Montreal Canadiens",      "Eastern Standard Time" },
            { "New Jersey Devils",       "Eastern Standard Time" },
            { "New York Islanders",      "Eastern Standard Time" },
            { "New York Rangers",        "Eastern Standard Time" },
            { "Ottawa Senators",         "Eastern Standard Time" },
            { "Philadelphia Flyers",     "Eastern Standard Time" },
            { "Pittsburgh Penguins",     "Eastern Standard Time" },
            { "Tampa Bay Lightning",     "Eastern Standard Time" },
            { "Toronto Maple Leafs",     "Eastern Standard Time" },
            { "Washington Capitals",     "Eastern Standard Time" },
            // Central
            { "Chicago Blackhawks",      "Central Standard Time" },
            { "Dallas Stars",            "Central Standard Time" },
            { "Minnesota Wild",          "Central Standard Time" },
            { "Nashville Predators",     "Central Standard Time" },
            { "St. Louis Blues",         "Central Standard Time" },
            { "Winnipeg Jets",           "Central Standard Time" },
            // Mountain
            { "Calgary Flames",          "Mountain Standard Time" },
            { "Colorado Avalanche",      "Mountain Standard Time" },
            { "Edmonton Oilers",         "Mountain Standard Time" },
            // Pacific
            { "Anaheim Ducks",           "Pacific Standard Time" },
            { "Los Angeles Kings",       "Pacific Standard Time" },
            { "San Jose Sharks",         "Pacific Standard Time" },
            { "Seattle Kraken",          "Pacific Standard Time" },
            { "Vancouver Canucks",       "Pacific Standard Time" },
            { "Vegas Golden Knights",    "Pacific Standard Time" },
            { "Utah Hockey Club",        "US Mountain Standard Time" }, // UT — observes MDT, Mountain Standard Time is fine
        };

        private static readonly Dictionary<string, string> _mlb = new(StringComparer.OrdinalIgnoreCase)
        {
            // Eastern
            { "Baltimore Orioles",       "Eastern Standard Time" },
            { "Boston Red Sox",          "Eastern Standard Time" },
            { "New York Yankees",        "Eastern Standard Time" },
            { "New York Mets",           "Eastern Standard Time" },
            { "Toronto Blue Jays",       "Eastern Standard Time" },
            { "Tampa Bay Rays",          "Eastern Standard Time" },
            { "Atlanta Braves",          "Eastern Standard Time" },
            { "Miami Marlins",           "Eastern Standard Time" },
            { "Philadelphia Phillies",   "Eastern Standard Time" },
            { "Washington Nationals",    "Eastern Standard Time" },
            { "Pittsburgh Pirates",      "Eastern Standard Time" },
            { "Cincinnati Reds",         "Eastern Standard Time" },
            { "Cleveland Guardians",     "Eastern Standard Time" },
            { "Detroit Tigers",          "Eastern Standard Time" },
            // Central
            { "Chicago White Sox",       "Central Standard Time" },
            { "Chicago Cubs",            "Central Standard Time" },
            { "Kansas City Royals",      "Central Standard Time" },
            { "Minnesota Twins",         "Central Standard Time" },
            { "Milwaukee Brewers",       "Central Standard Time" },
            { "St. Louis Cardinals",     "Central Standard Time" },
            { "Houston Astros",          "Central Standard Time" },
            { "Texas Rangers",           "Central Standard Time" },
            // Mountain
            { "Colorado Rockies",        "Mountain Standard Time" },
            { "Arizona Diamondbacks",    "US Mountain Standard Time" }, // AZ — no DST
            // Pacific
            { "Los Angeles Dodgers",     "Pacific Standard Time" },
            { "Los Angeles Angels",      "Pacific Standard Time" },
            { "San Francisco Giants",    "Pacific Standard Time" },
            { "Oakland Athletics",       "Pacific Standard Time" },
            { "Seattle Mariners",        "Pacific Standard Time" },
            { "San Diego Padres",        "Pacific Standard Time" },
        };

        private static readonly Dictionary<string, Dictionary<string, string>> _leagueMaps =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "NFL", _nfl },
                { "NBA", _nba },
                { "NHL", _nhl },
                { "MLB", _mlb },
            };

        /// <summary>
        /// Returns the Windows timezone ID for the home team's venue.
        /// Falls back to Eastern Standard Time when unknown.
        /// </summary>
        public static string GetTimeZoneId(string league, string homeTeam)
        {
            if (_leagueMaps.TryGetValue(league, out var map) &&
                map.TryGetValue(homeTeam, out var tzId))
            {
                return tzId;
            }

            return "Eastern Standard Time";
        }

        /// <summary>
        /// Converts a UTC DateTime to the venue's local time and returns a formatted display
        /// string with the timezone abbreviation, e.g. "Apr 19, 7:10 PM ET".
        /// </summary>
        public static string FormatVenueTime(DateTime utcTime, string venueTimeZoneId)
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(venueTimeZoneId);
            var local = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), tz);

            var abbr = local.IsDaylightSavingTime()
                ? GetDaylightAbbr(venueTimeZoneId)
                : GetStandardAbbr(venueTimeZoneId);

            return $"{local:MMM dd, h:mm tt} {abbr}";
        }

        private static string GetStandardAbbr(string tzId) => tzId switch
        {
            "Eastern Standard Time"      => "ET",
            "Central Standard Time"      => "CT",
            "Mountain Standard Time"     => "MT",
            "US Mountain Standard Time"  => "MT",
            "Pacific Standard Time"      => "PT",
            _                            => "ET"
        };

        private static string GetDaylightAbbr(string tzId) => tzId switch
        {
            "Eastern Standard Time"      => "ET",
            "Central Standard Time"      => "CT",
            "Mountain Standard Time"     => "MT",
            "US Mountain Standard Time"  => "MT", // AZ never observes DST
            "Pacific Standard Time"      => "PT",
            _                            => "ET"
        };
    }
}
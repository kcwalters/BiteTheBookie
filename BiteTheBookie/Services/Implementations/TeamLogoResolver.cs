namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Resolves team logo image URLs (served by ESPN's public CDN, loaded client-side)
    /// from the full team names returned by The Odds API. Returns an empty string when
    /// no mapping exists so views can gracefully omit the image.
    /// </summary>
    internal static class TeamLogoResolver
    {
        private const string Nfl = "https://a.espncdn.com/i/teamlogos/nfl/500/{0}.png";
        private const string Nba = "https://a.espncdn.com/i/teamlogos/nba/500/{0}.png";
        private const string Nhl = "https://a.espncdn.com/i/teamlogos/nhl/500/{0}.png";
        private const string Mlb = "https://a.espncdn.com/i/teamlogos/mlb/500/{0}.png";

        private static readonly Dictionary<string, string> NflAbbr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arizona Cardinals"] = "ari", ["Atlanta Falcons"] = "atl", ["Baltimore Ravens"] = "bal",
            ["Buffalo Bills"] = "buf", ["Carolina Panthers"] = "car", ["Chicago Bears"] = "chi",
            ["Cincinnati Bengals"] = "cin", ["Cleveland Browns"] = "cle", ["Dallas Cowboys"] = "dal",
            ["Denver Broncos"] = "den", ["Detroit Lions"] = "det", ["Green Bay Packers"] = "gb",
            ["Houston Texans"] = "hou", ["Indianapolis Colts"] = "ind", ["Jacksonville Jaguars"] = "jax",
            ["Kansas City Chiefs"] = "kc", ["Las Vegas Raiders"] = "lv", ["Los Angeles Chargers"] = "lac",
            ["Los Angeles Rams"] = "lar", ["Miami Dolphins"] = "mia", ["Minnesota Vikings"] = "min",
            ["New England Patriots"] = "ne", ["New Orleans Saints"] = "no", ["New York Giants"] = "nyg",
            ["New York Jets"] = "nyj", ["Philadelphia Eagles"] = "phi", ["Pittsburgh Steelers"] = "pit",
            ["San Francisco 49ers"] = "sf", ["Seattle Seahawks"] = "sea", ["Tampa Bay Buccaneers"] = "tb",
            ["Tennessee Titans"] = "ten", ["Washington Commanders"] = "wsh",
        };

        private static readonly Dictionary<string, string> NbaAbbr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Atlanta Hawks"] = "atl", ["Boston Celtics"] = "bos", ["Brooklyn Nets"] = "bkn",
            ["Charlotte Hornets"] = "cha", ["Chicago Bulls"] = "chi", ["Cleveland Cavaliers"] = "cle",
            ["Dallas Mavericks"] = "dal", ["Denver Nuggets"] = "den", ["Detroit Pistons"] = "det",
            ["Golden State Warriors"] = "gs", ["Houston Rockets"] = "hou", ["Indiana Pacers"] = "ind",
            ["Los Angeles Clippers"] = "lac", ["LA Clippers"] = "lac", ["Los Angeles Lakers"] = "lal",
            ["Memphis Grizzlies"] = "mem", ["Miami Heat"] = "mia", ["Milwaukee Bucks"] = "mil",
            ["Minnesota Timberwolves"] = "min", ["New Orleans Pelicans"] = "no", ["New York Knicks"] = "ny",
            ["Oklahoma City Thunder"] = "okc", ["Orlando Magic"] = "orl", ["Philadelphia 76ers"] = "phi",
            ["Phoenix Suns"] = "phx", ["Portland Trail Blazers"] = "por", ["Sacramento Kings"] = "sac",
            ["San Antonio Spurs"] = "sa", ["Toronto Raptors"] = "tor", ["Utah Jazz"] = "utah",
            ["Washington Wizards"] = "wsh",
        };

        private static readonly Dictionary<string, string> NhlAbbr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Anaheim Ducks"] = "ana", ["Boston Bruins"] = "bos", ["Buffalo Sabres"] = "buf",
            ["Calgary Flames"] = "cgy", ["Carolina Hurricanes"] = "car", ["Chicago Blackhawks"] = "chi",
            ["Colorado Avalanche"] = "col", ["Columbus Blue Jackets"] = "cbj", ["Dallas Stars"] = "dal",
            ["Detroit Red Wings"] = "det", ["Edmonton Oilers"] = "edm", ["Florida Panthers"] = "fla",
            ["Los Angeles Kings"] = "la", ["Minnesota Wild"] = "min", ["Montreal Canadiens"] = "mtl",
            ["Montréal Canadiens"] = "mtl", ["Nashville Predators"] = "nsh", ["New Jersey Devils"] = "nj",
            ["New York Islanders"] = "nyi", ["New York Rangers"] = "nyr", ["Ottawa Senators"] = "ott",
            ["Philadelphia Flyers"] = "phi", ["Pittsburgh Penguins"] = "pit", ["San Jose Sharks"] = "sj",
            ["Seattle Kraken"] = "sea", ["St Louis Blues"] = "stl", ["St. Louis Blues"] = "stl",
            ["Tampa Bay Lightning"] = "tb", ["Toronto Maple Leafs"] = "tor", ["Utah Hockey Club"] = "utah",
            ["Utah Mammoth"] = "utah", ["Vancouver Canucks"] = "van", ["Vegas Golden Knights"] = "vgk",
            ["Washington Capitals"] = "wsh", ["Winnipeg Jets"] = "wpg",
        };

        public static string Resolve(string league, string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return string.Empty;



            return league?.ToUpperInvariant() switch
            {
                "NFL" => Format(Nfl, NflAbbr, teamName),
                "NBA" => Format(Nba, NbaAbbr, teamName),
                "NHL" => Format(Nhl, NhlAbbr, teamName),
                "MLB" => ResolveMlbLogo(teamName),
                "CFB" or "CBB" or "NCAA" => ResolveCollege(teamName),
                _ => string.Empty,
            };
        }

        private static string Format(string template, Dictionary<string, string> map, string teamName) =>
            map.TryGetValue(teamName, out var abbr) ? string.Format(template, abbr) : string.Empty;

        // Resolves an MLB logo URL. Reuses MlbAbbr for the name->code mapping, then adjusts
        // the two codes whose ESPN CDN abbreviation differs from the Detail-page code
        // (White Sox: cws->chw, Athletics: ath->oak).
        private static string ResolveMlbLogo(string teamName)
        {
            if (!MlbAbbr.TryGetValue(teamName, out var abbr))
                return string.Empty;

            var espnAbbr = abbr.ToLowerInvariant() switch
            {
                "cws" => "chw",
                "ath" => "oak",
                _ => abbr.ToLowerInvariant(),
            };
            return string.Format(Mlb, espnAbbr);
        }

        // MLB team name -> ESPN abbreviation (matches Detail's MlbAbbrevCodes).
        private static readonly Dictionary<string, string> MlbAbbr = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arizona Diamondbacks"] = "ari", ["Atlanta Braves"] = "atl", ["Baltimore Orioles"] = "bal",
            ["Boston Red Sox"] = "bos", ["Chicago Cubs"] = "chc", ["Chicago White Sox"] = "cws",
            ["Cincinnati Reds"] = "cin", ["Cleveland Guardians"] = "cle", ["Colorado Rockies"] = "col",
            ["Detroit Tigers"] = "det", ["Houston Astros"] = "hou", ["Kansas City Royals"] = "kc",
            ["Los Angeles Angels"] = "laa", ["Los Angeles Dodgers"] = "lad", ["Miami Marlins"] = "mia",
            ["Milwaukee Brewers"] = "mil", ["Minnesota Twins"] = "min", ["New York Mets"] = "nym",
            ["New York Yankees"] = "nyy", ["Oakland Athletics"] = "oak", ["Athletics"] = "ath",
            ["Philadelphia Phillies"] = "phi", ["Pittsburgh Pirates"] = "pit", ["San Diego Padres"] = "sd",
            ["San Francisco Giants"] = "sf", ["Seattle Mariners"] = "sea", ["St. Louis Cardinals"] = "stl",
            ["Tampa Bay Rays"] = "tb", ["Texas Rangers"] = "tex", ["Toronto Blue Jays"] = "tor",
            ["Washington Nationals"] = "wsh",
        };

        /// <summary>
        /// Resolves a team's short code (as used by the simulation Detail page) from its
        /// full name. Returns an uppercase code, or empty when unmapped.
        /// </summary>
        public static string ResolveCode(string league, string teamName)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return string.Empty;

            var map = league?.ToUpperInvariant() switch
            {
                "NFL" => NflAbbr,
                "NHL" => NhlAbbr,
                "MLB" => MlbAbbr,
                "NBA" => NbaCode,
                _ => null,
            };

            return map is not null && map.TryGetValue(teamName, out var code) ? code.ToUpperInvariant() : string.Empty;
        }

        // NBA team name -> standard team code used by the simulation Detail page
        // (matches PicksController.NbaTeamCodes). Distinct from the ESPN logo abbreviations.
        private static readonly Dictionary<string, string> NbaCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Atlanta Hawks"] = "ATL", ["Boston Celtics"] = "BOS", ["Brooklyn Nets"] = "BKN",
            ["Charlotte Hornets"] = "CHA", ["Chicago Bulls"] = "CHI", ["Cleveland Cavaliers"] = "CLE",
            ["Dallas Mavericks"] = "DAL", ["Denver Nuggets"] = "DEN", ["Detroit Pistons"] = "DET",
            ["Golden State Warriors"] = "GSW", ["Houston Rockets"] = "HOU", ["Indiana Pacers"] = "IND",
            ["Los Angeles Clippers"] = "LAC", ["LA Clippers"] = "LAC", ["Los Angeles Lakers"] = "LAL",
            ["Memphis Grizzlies"] = "MEM", ["Miami Heat"] = "MIA", ["Milwaukee Bucks"] = "MIL",
            ["Minnesota Timberwolves"] = "MIN", ["New Orleans Pelicans"] = "NOP", ["New York Knicks"] = "NYK",
            ["Oklahoma City Thunder"] = "OKC", ["Orlando Magic"] = "ORL", ["Philadelphia 76ers"] = "PHI",
            ["Phoenix Suns"] = "PHX", ["Portland Trail Blazers"] = "POR", ["Sacramento Kings"] = "SAC",
            ["San Antonio Spurs"] = "SAS", ["Toronto Raptors"] = "TOR", ["Utah Jazz"] = "UTA",
            ["Washington Wizards"] = "WAS",
        };



        // ESPN college logos are keyed by numeric team id; the same id serves football and basketball.
        private const string College = "https://a.espncdn.com/i/teamlogos/ncaa/500/{0}.png";

        private static string ResolveCollege(string teamName)
        {
            if (CollegeIds.TryGetValue(teamName, out var id))
            {
                return string.Format(College, id);
            }

            // The Odds API sometimes returns the school without the mascot (e.g. "Alabama").
            // Try a prefix match against known full names.
            foreach (var kvp in CollegeIds)
            {
                if (kvp.Key.StartsWith(teamName + " ", StringComparison.OrdinalIgnoreCase) ||
                    kvp.Key.Equals(teamName, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Format(College, kvp.Value);
                }
            }
            return string.Empty;
        }

        // Full team name -> ESPN numeric team id. The same id serves football and basketball.
        // Comprehensive Division I coverage (FBS + D1 basketball programs).
        private static readonly Dictionary<string, string> CollegeIds = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Abilene Christian Wildcats"] = "2000", ["Air Force Falcons"] = "2005", ["Akron Zips"] = "2006",
            ["Alabama Crimson Tide"] = "333", ["Alabama A&M Bulldogs"] = "2010", ["Alabama State Hornets"] = "2011",
            ["Albany Great Danes"] = "399", ["Alcorn State Braves"] = "2016", ["American University Eagles"] = "44",
            ["Appalachian State Mountaineers"] = "2026", ["Arizona Wildcats"] = "12", ["Arizona State Sun Devils"] = "9",
            ["Arkansas Razorbacks"] = "8", ["Arkansas State Red Wolves"] = "2032", ["Arkansas-Pine Bluff Golden Lions"] = "2029",
            ["Army Black Knights"] = "349", ["Auburn Tigers"] = "2", ["Austin Peay Governors"] = "2046",
            ["Ball State Cardinals"] = "2050", ["Baylor Bears"] = "239", ["Bellarmine Knights"] = "91",
            ["Belmont Bruins"] = "2057", ["Bethune-Cookman Wildcats"] = "2065", ["Binghamton Bearcats"] = "2066",
            ["Boise State Broncos"] = "68", ["Boston College Eagles"] = "103", ["Boston University Terriers"] = "104",
            ["Bowling Green Falcons"] = "189", ["Bradley Braves"] = "71", ["Brown Bears"] = "225",
            ["Bryant Bulldogs"] = "2803", ["Bucknell Bison"] = "2083", ["Buffalo Bulls"] = "2084",
            ["Butler Bulldogs"] = "2086", ["BYU Cougars"] = "252", ["Cal Poly Mustangs"] = "13",
            ["Cal State Bakersfield Roadrunners"] = "2934", ["Cal State Fullerton Titans"] = "2239",
            ["Cal State Northridge Matadors"] = "2463", ["California Golden Bears"] = "25", ["Campbell Fighting Camels"] = "2097",
            ["Canisius Golden Griffins"] = "2099", ["Central Arkansas Bears"] = "2110", ["Central Connecticut Blue Devils"] = "2115",
            ["Central Michigan Chippewas"] = "2117", ["Charleston Cougars"] = "232", ["Charleston Southern Buccaneers"] = "2127",
            ["Charlotte 49ers"] = "2429", ["Chattanooga Mocs"] = "236", ["Chicago State Cougars"] = "2130",
            ["Cincinnati Bearcats"] = "2132", ["Clemson Tigers"] = "228", ["Cleveland State Vikings"] = "325",
            ["Coastal Carolina Chanticleers"] = "324", ["Colgate Raiders"] = "2142", ["College of Charleston Cougars"] = "232",
            ["Colorado Buffaloes"] = "38", ["Colorado State Rams"] = "36", ["Columbia Lions"] = "171",
            ["Connecticut Huskies"] = "41", ["Coppin State Eagles"] = "2154", ["Cornell Big Red"] = "172",
            ["Creighton Bluejays"] = "156", ["Dartmouth Big Green"] = "159", ["Davidson Wildcats"] = "2166",
            ["Dayton Flyers"] = "2168", ["Delaware Blue Hens"] = "48", ["Delaware State Hornets"] = "2169",
            ["Denver Pioneers"] = "2172", ["DePaul Blue Demons"] = "305", ["Detroit Mercy Titans"] = "2174",
            ["Drake Bulldogs"] = "2181", ["Drexel Dragons"] = "2182", ["Duke Blue Devils"] = "150",
            ["Duquesne Dukes"] = "2184", ["East Carolina Pirates"] = "151", ["East Tennessee State Buccaneers"] = "2193",
            ["Eastern Illinois Panthers"] = "2197", ["Eastern Kentucky Colonels"] = "2198", ["Eastern Michigan Eagles"] = "2199",
            ["Eastern Washington Eagles"] = "331", ["Elon Phoenix"] = "2210", ["Evansville Purple Aces"] = "339",
            ["Fairfield Stags"] = "2217", ["Fairleigh Dickinson Knights"] = "161", ["Florida Gators"] = "57",
            ["Florida A&M Rattlers"] = "50", ["Florida Atlantic Owls"] = "2226", ["Florida Gulf Coast Eagles"] = "526",
            ["Florida International Panthers"] = "2229", ["Florida State Seminoles"] = "52", ["Fordham Rams"] = "2230",
            ["Fresno State Bulldogs"] = "278", ["Furman Paladins"] = "231", ["Gardner-Webb Runnin' Bulldogs"] = "2241",
            ["George Mason Patriots"] = "2244", ["George Washington Colonials"] = "45", ["Georgetown Hoyas"] = "46",
            ["Georgia Bulldogs"] = "61", ["Georgia Southern Eagles"] = "290", ["Georgia State Panthers"] = "2247",
            ["Georgia Tech Yellow Jackets"] = "59", ["Gonzaga Bulldogs"] = "2250", ["Grambling Tigers"] = "2755",
            ["Grand Canyon Antelopes"] = "2253", ["Green Bay Phoenix"] = "2739", ["Hampton Pirates"] = "2261",
            ["Harvard Crimson"] = "108", ["Hawaii Rainbow Warriors"] = "62", ["High Point Panthers"] = "2272",
            ["Hofstra Pride"] = "2275", ["Holy Cross Crusaders"] = "107", ["Houston Cougars"] = "248",
            ["Houston Christian Huskies"] = "2277", ["Howard Bison"] = "47", ["Idaho Vandals"] = "70",
            ["Idaho State Bengals"] = "304", ["Illinois Fighting Illini"] = "356", ["Illinois State Redbirds"] = "2287",
            ["Illinois-Chicago Flames"] = "82", ["Incarnate Word Cardinals"] = "2916", ["Indiana Hoosiers"] = "84",
            ["Indiana State Sycamores"] = "282", ["Iona Gaels"] = "314", ["Iowa Hawkeyes"] = "2294",
            ["Iowa State Cyclones"] = "66", ["IUPUI Jaguars"] = "85", ["Jackson State Tigers"] = "2296",
            ["Jacksonville Dolphins"] = "294", ["Jacksonville State Gamecocks"] = "55", ["James Madison Dukes"] = "256",
            ["Kansas Jayhawks"] = "2305", ["Kansas City Roos"] = "140", ["Kansas State Wildcats"] = "2306",
            ["Kennesaw State Owls"] = "338", ["Kent State Golden Flashes"] = "2309", ["Kentucky Wildcats"] = "96",
            ["La Salle Explorers"] = "2325", ["Lafayette Leopards"] = "322", ["Lamar Cardinals"] = "2320",
            ["Lehigh Mountain Hawks"] = "2329", ["Liberty Flames"] = "2335", ["Lipscomb Bisons"] = "288",
            ["Long Beach State Beach"] = "299", ["Longwood Lancers"] = "2344", ["Louisiana Ragin' Cajuns"] = "309",
            ["Louisiana Tech Bulldogs"] = "2348", ["Louisiana-Monroe Warhawks"] = "2433", ["Louisville Cardinals"] = "97",
            ["Loyola Chicago Ramblers"] = "2350", ["Loyola Marymount Lions"] = "2351", ["Loyola Maryland Greyhounds"] = "2352",
            ["LSU Tigers"] = "99", ["Maine Black Bears"] = "311", ["Manhattan Jaspers"] = "2363",
            ["Marist Red Foxes"] = "2368", ["Marquette Golden Eagles"] = "269", ["Marshall Thundering Herd"] = "276",
            ["Maryland Terrapins"] = "120", ["Maryland-Eastern Shore Hawks"] = "2379", ["UMass Minutemen"] = "113",
            ["UMass Lowell River Hawks"] = "2349", ["McNeese Cowboys"] = "2377", ["Memphis Tigers"] = "235",
            ["Mercer Bears"] = "2382", ["Miami Hurricanes"] = "2390", ["Miami (OH) RedHawks"] = "193",
            ["Michigan Wolverines"] = "130", ["Michigan State Spartans"] = "127", ["Middle Tennessee Blue Raiders"] = "2393",
            ["Milwaukee Panthers"] = "270", ["Minnesota Golden Gophers"] = "135", ["Mississippi State Bulldogs"] = "344",
            ["Mississippi Valley State Delta Devils"] = "2400", ["Missouri Tigers"] = "142", ["Missouri State Bears"] = "2623",
            ["Monmouth Hawks"] = "2405", ["Montana Grizzlies"] = "149", ["Montana State Bobcats"] = "147",
            ["Morehead State Eagles"] = "2413", ["Morgan State Bears"] = "2415", ["Mount St. Mary's Mountaineers"] = "116",
            ["Murray State Racers"] = "93", ["Navy Midshipmen"] = "2426", ["NC State Wolfpack"] = "152",
            ["Nebraska Cornhuskers"] = "158", ["Nevada Wolf Pack"] = "2440", ["New Hampshire Wildcats"] = "160",
            ["New Mexico Lobos"] = "167", ["New Mexico State Aggies"] = "166", ["New Orleans Privateers"] = "2443",
            ["Niagara Purple Eagles"] = "315", ["Nicholls Colonels"] = "2447", ["NJIT Highlanders"] = "2885",
            ["Norfolk State Spartans"] = "2450", ["North Alabama Lions"] = "2453", ["North Carolina Tar Heels"] = "153",
            ["North Carolina A&T Aggies"] = "2448", ["North Carolina Central Eagles"] = "2428", ["North Dakota Fighting Hawks"] = "155",
            ["North Dakota State Bison"] = "2449", ["North Florida Ospreys"] = "2454", ["North Texas Mean Green"] = "249",
            ["Northeastern Huskies"] = "111", ["Northern Arizona Lumberjacks"] = "2464", ["Northern Colorado Bears"] = "2458",
            ["Northern Illinois Huskies"] = "2459", ["Northern Iowa Panthers"] = "2460", ["Northern Kentucky Norse"] = "94",
            ["Northwestern Wildcats"] = "77", ["Northwestern State Demons"] = "2466", ["Notre Dame Fighting Irish"] = "87",
            ["Oakland Golden Grizzlies"] = "2473", ["Ohio Bobcats"] = "195", ["Ohio State Buckeyes"] = "194",
            ["Oklahoma Sooners"] = "201", ["Oklahoma State Cowboys"] = "197", ["Old Dominion Monarchs"] = "295",
            ["Ole Miss Rebels"] = "145", ["Omaha Mavericks"] = "2437", ["Oral Roberts Golden Eagles"] = "198",
            ["Oregon Ducks"] = "2483", ["Oregon State Beavers"] = "204", ["Pacific Tigers"] = "279",
            ["Penn State Nittany Lions"] = "213", ["Pennsylvania Quakers"] = "219", ["Pepperdine Waves"] = "2492",
            ["Pittsburgh Panthers"] = "221", ["Portland Pilots"] = "2501", ["Portland State Vikings"] = "2502",
            ["Prairie View A&M Panthers"] = "2504", ["Presbyterian Blue Hose"] = "2506", ["Princeton Tigers"] = "163",
            ["Providence Friars"] = "2507", ["Purdue Boilermakers"] = "2509", ["Purdue Fort Wayne Mastodons"] = "2870",
            ["Quinnipiac Bobcats"] = "2514", ["Radford Highlanders"] = "2515", ["Rhode Island Rams"] = "227",
            ["Rice Owls"] = "242", ["Richmond Spiders"] = "257", ["Rider Broncs"] = "2520",
            ["Robert Morris Colonials"] = "2523", ["Rutgers Scarlet Knights"] = "164", ["Sacramento State Hornets"] = "16",
            ["Sacred Heart Pioneers"] = "2529", ["Saint Joseph's Hawks"] = "2603", ["Saint Louis Billikens"] = "139",
            ["Saint Mary's Gaels"] = "2608", ["Saint Peter's Peacocks"] = "2612", ["Sam Houston Bearkats"] = "2534",
            ["Samford Bulldogs"] = "2535", ["San Diego Toreros"] = "301", ["San Diego State Aztecs"] = "21",
            ["San Francisco Dons"] = "2539", ["San Jose State Spartans"] = "23", ["Santa Clara Broncos"] = "2541",
            ["Seattle Redhawks"] = "2547", ["Seton Hall Pirates"] = "2550", ["Siena Saints"] = "2561",
            ["SMU Mustangs"] = "2567", ["South Alabama Jaguars"] = "6", ["South Carolina Gamecocks"] = "2579",
            ["South Carolina State Bulldogs"] = "2569", ["South Dakota Coyotes"] = "233", ["South Dakota State Jackrabbits"] = "2571",
            ["South Florida Bulls"] = "58", ["Southeast Missouri State Redhawks"] = "2546", ["Southeastern Louisiana Lions"] = "2545",
            ["Southern Jaguars"] = "2582", ["Southern Illinois Salukis"] = "79", ["Southern Miss Golden Eagles"] = "2572",
            ["Southern Utah Thunderbirds"] = "253", ["St. Bonaventure Bonnies"] = "179", ["St. Francis (PA) Red Flash"] = "2598",
            ["St. John's Red Storm"] = "2599", ["St. Thomas Tommies"] = "2900", ["Stanford Cardinal"] = "24",
            ["Stephen F. Austin Lumberjacks"] = "2617", ["Stetson Hatters"] = "56", ["Stony Brook Seawolves"] = "2619",
            ["Syracuse Orange"] = "183", ["Tarleton State Texans"] = "2627", ["TCU Horned Frogs"] = "2628",
            ["Temple Owls"] = "218", ["Tennessee Volunteers"] = "2633", ["Tennessee State Tigers"] = "2634",
            ["Tennessee Tech Golden Eagles"] = "2635", ["Texas Longhorns"] = "251", ["Texas A&M Aggies"] = "245",
            ["Texas A&M-Corpus Christi Islanders"] = "357", ["Texas Southern Tigers"] = "2640", ["Texas State Bobcats"] = "326",
            ["Texas Tech Red Raiders"] = "2641", ["Toledo Rockets"] = "2649", ["Towson Tigers"] = "119",
            ["Troy Trojans"] = "2653", ["Tulane Green Wave"] = "2655", ["Tulsa Golden Hurricane"] = "202",
            ["UAB Blazers"] = "5", ["UC Davis Aggies"] = "302", ["UC Irvine Anteaters"] = "300",
            ["UC Riverside Highlanders"] = "27", ["UC San Diego Tritons"] = "28", ["UC Santa Barbara Gauchos"] = "2540",
            ["UCF Knights"] = "2116", ["UCLA Bruins"] = "26", ["UConn Huskies"] = "41",
            ["UMBC Retrievers"] = "2378", ["UNC Asheville Bulldogs"] = "2427", ["UNC Greensboro Spartans"] = "2430",
            ["UNC Wilmington Seahawks"] = "350", ["UNLV Rebels"] = "2439", ["USC Trojans"] = "30",
            ["USC Upstate Spartans"] = "2908", ["UT Arlington Mavericks"] = "250", ["UT Martin Skyhawks"] = "2630",
            ["UT Rio Grande Valley Vaqueros"] = "292", ["Utah Utes"] = "254", ["Utah State Aggies"] = "328",
            ["Utah Tech Trailblazers"] = "3101", ["Utah Valley Wolverines"] = "3084", ["UTEP Miners"] = "2638",
            ["UTSA Roadrunners"] = "2636", ["Valparaiso Beacons"] = "2674", ["Vanderbilt Commodores"] = "238",
            ["VCU Rams"] = "2670", ["Vermont Catamounts"] = "261", ["Villanova Wildcats"] = "222",
            ["Virginia Cavaliers"] = "258", ["Virginia Tech Hokies"] = "259", ["VMI Keydets"] = "2678",
            ["Wagner Seahawks"] = "2681", ["Wake Forest Demon Deacons"] = "154", ["Washington Huskies"] = "264",
            ["Washington State Cougars"] = "265", ["Weber State Wildcats"] = "2692", ["West Virginia Mountaineers"] = "277",
            ["Western Carolina Catamounts"] = "2717", ["Western Illinois Leathernecks"] = "2710", ["Western Kentucky Hilltoppers"] = "98",
            ["Western Michigan Broncos"] = "2711", ["Wichita State Shockers"] = "2724", ["William & Mary Tribe"] = "2729",
            ["Winthrop Eagles"] = "2737", ["Wisconsin Badgers"] = "275", ["Wofford Terriers"] = "2747",
            ["Wright State Raiders"] = "2750", ["Wyoming Cowboys"] = "2751", ["Xavier Musketeers"] = "2752",
            ["Yale Bulldogs"] = "43", ["Youngstown State Penguins"] = "2754",
        };


    }
}

using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class CFBGamesService : ICFBGamesService
    {
        private readonly ILogger<CFBGamesService> _logger;
        private readonly TheOddsApiClient _oddsApiClient;
        private readonly Dictionary<string, (string Name, string Logo, string Code)> _teamInfo;

        public CFBGamesService(
            ILogger<CFBGamesService> logger,
            TheOddsApiClient oddsApiClient)
        {
            _logger = logger;
            _oddsApiClient = oddsApiClient;
            _teamInfo = InitializeTeamInfo();
        }

        public async Task<List<CFBGameMatchup>> GetUpcomingCFBGamesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching NCAA Football games from The Odds API");

            // /events returns the full upcoming schedule (all games), while /odds only
            // returns games with posted betting lines. Fetch both and merge so every
            // scheduled game shows, enriched with lines where available.
            var eventsData = await _oddsApiClient.GetEventsAsync("americanfootball_ncaaf", cancellationToken);
            var oddsData = await _oddsApiClient.GetAsync("/v4/sports/americanfootball_ncaaf/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american", cancellationToken);

            var games = ParseCFBOddsApiResponse(eventsData);
            MergeCFBOdds(games, oddsData);

            if (games.Any())
            {
                _logger.LogInformation("Successfully fetched {Count} CFB games from The Odds API", games.Count);
                return games;
            }

            _logger.LogWarning("No CFB games available from The Odds API");
            return new List<CFBGameMatchup>();
        }

        // Enriches parsed games with spread/total/moneyline from the /odds payload, matching
        // by team names. Games without posted lines simply keep null odds.
        private void MergeCFBOdds(List<CFBGameMatchup> games, JsonElement oddsData)
        {
            if (oddsData.ValueKind != JsonValueKind.Array) return;

            foreach (var game in oddsData.EnumerateArray())
            {
                try
                {
                    var homeTeam = game.TryGetProperty("home_team", out var h) ? h.GetString() ?? "" : "";
                    var awayTeam = game.TryGetProperty("away_team", out var a) ? a.GetString() ?? "" : "";
                    var homeCode = MapTeamNameToCode(homeTeam);
                    var awayCode = MapTeamNameToCode(awayTeam);
                    if (string.IsNullOrEmpty(homeCode) || string.IsNullOrEmpty(awayCode)) continue;

                    var match = games.FirstOrDefault(g =>
                        g.HomeTeamCode == homeCode && g.AwayTeamCode == awayCode);
                    if (match is null) continue;

                    if (!game.TryGetProperty("bookmakers", out var bookmakers) ||
                        bookmakers.ValueKind != JsonValueKind.Array) continue;

                    foreach (var bookmaker in bookmakers.EnumerateArray())
                    {
                        if (!bookmaker.TryGetProperty("markets", out var markets) ||
                            markets.ValueKind != JsonValueKind.Array) continue;

                        foreach (var market in markets.EnumerateArray())
                        {
                            var key = market.TryGetProperty("key", out var k) ? k.GetString() : null;
                            if (!market.TryGetProperty("outcomes", out var outcomes) ||
                                outcomes.ValueKind != JsonValueKind.Array) continue;
                            var list = outcomes.EnumerateArray().ToList();

                            if (key == "spreads" && !match.Spread.HasValue)
                            {
                                var ho = list.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                if (ho.ValueKind != JsonValueKind.Undefined && ho.TryGetProperty("point", out var p))
                                    match.Spread = p.GetDecimal();
                            }
                            else if (key == "totals" && !match.OverUnder.HasValue && list.Count > 0 &&
                                     list[0].TryGetProperty("point", out var tp))
                            {
                                match.OverUnder = tp.GetDecimal();
                            }
                            else if (key == "h2h")
                            {
                                var ho = list.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                var ao = list.FirstOrDefault(o => o.GetProperty("name").GetString() == awayTeam);
                                if (ho.ValueKind != JsonValueKind.Undefined && !match.HomeMoneyline.HasValue && ho.TryGetProperty("price", out var hp))
                                    match.HomeMoneyline = hp.GetInt32();
                                if (ao.ValueKind != JsonValueKind.Undefined && !match.AwayMoneyline.HasValue && ao.TryGetProperty("price", out var ap))
                                    match.AwayMoneyline = ap.GetInt32();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error merging CFB odds");
                }
            }
        }

        private List<CFBGameMatchup> ParseCFBOddsApiResponse(JsonElement oddsData)
        {
            var games = new List<CFBGameMatchup>();

            if (oddsData.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected response format from The Odds API");
                return games;
            }

            var totalGames = oddsData.GetArrayLength();
            _logger.LogInformation("Processing {TotalGames} CFB games from The Odds API", totalGames);

            var skippedGames = 0;
            var unmappedTeams = new HashSet<string>();

            foreach (var game in oddsData.EnumerateArray())
            {
                try
                {
                    var homeTeam = game.GetProperty("home_team").GetString() ?? "";
                    var awayTeam = game.GetProperty("away_team").GetString() ?? "";
                    var commenceTime = game.GetProperty("commence_time").GetDateTime();

                    if (commenceTime.Kind == DateTimeKind.Unspecified)
                    {
                        commenceTime = DateTime.SpecifyKind(commenceTime, DateTimeKind.Utc);
                    }

                    var homeTeamCode = MapTeamNameToCode(homeTeam);
                    var awayTeamCode = MapTeamNameToCode(awayTeam);

                    if (string.IsNullOrEmpty(homeTeamCode) || string.IsNullOrEmpty(awayTeamCode))
                    {
                        _logger.LogWarning("Could not map CFB teams: {Home} / {Away}", homeTeam, awayTeam);

                        if (string.IsNullOrEmpty(homeTeamCode)) unmappedTeams.Add(homeTeam);
                        if (string.IsNullOrEmpty(awayTeamCode)) unmappedTeams.Add(awayTeam);

                        skippedGames++;
                        continue;
                    }

                    var homeInfo = _teamInfo.GetValueOrDefault(homeTeamCode);
                    var awayInfo = _teamInfo.GetValueOrDefault(awayTeamCode);

                    if (homeInfo == default || awayInfo == default)
                    {
                        _logger.LogWarning("CFB team code lookup failed: {HomeCode} or {AwayCode}", homeTeamCode, awayTeamCode);
                        skippedGames++;
                        continue;
                    }

                    decimal? spread = null;
                    decimal? overUnder = null;
                    int? homeMoneyline = null;
                    int? awayMoneyline = null;

                    if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var bookmaker in bookmakers.EnumerateArray())
                        {
                            if (bookmaker.TryGetProperty("markets", out var markets) && markets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var market in markets.EnumerateArray())
                                {
                                    var marketKey = market.GetProperty("key").GetString();

                                    if (marketKey == "spreads" && !spread.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined)
                                        {
                                            spread = homeOutcome.GetProperty("point").GetDecimal();
                                        }
                                    }
                                    else if (marketKey == "totals" && !overUnder.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        if (outcomes.Any())
                                        {
                                            overUnder = outcomes.First().GetProperty("point").GetDecimal();
                                        }
                                    }
                                    else if (marketKey == "h2h")
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        var awayOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == awayTeam);

                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined && !homeMoneyline.HasValue)
                                        {
                                            homeMoneyline = homeOutcome.GetProperty("price").GetInt32();
                                        }
                                        if (awayOutcome.ValueKind != JsonValueKind.Undefined && !awayMoneyline.HasValue)
                                        {
                                            awayMoneyline = awayOutcome.GetProperty("price").GetInt32();
                                        }
                                    }
                                }
                            }

                            if (spread.HasValue && overUnder.HasValue && homeMoneyline.HasValue && awayMoneyline.HasValue)
                            {
                                break;
                            }
                        }
                    }

                    games.Add(new CFBGameMatchup
                    {
                        GameId = $"{awayTeamCode.ToLower()}-{homeTeamCode.ToLower()}-{commenceTime:yyyyMMdd}",
                        AwayTeamCode = awayInfo.Code,
                        AwayTeamName = awayInfo.Name,
                        AwayTeamLogo = awayInfo.Logo,
                        HomeTeamCode = homeInfo.Code,
                        HomeTeamName = homeInfo.Name,
                        HomeTeamLogo = homeInfo.Logo,
                        GameTime = commenceTime,
                        Spread = spread,
                        OverUnder = overUnder,
                        HomeMoneyline = homeMoneyline,
                        AwayMoneyline = awayMoneyline,
                        Status = "Scheduled"
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error parsing CFB game from Odds API");
                    skippedGames++;
                }
            }

            if (unmappedTeams.Any())
            {
                _logger.LogWarning("?? Unmapped CFB teams: {UnmappedTeams}", string.Join(", ", unmappedTeams));
            }

            _logger.LogInformation("CFB Parsing complete: {ParsedGames}/{TotalGames} games, {SkippedGames} skipped",
                games.Count, totalGames, skippedGames);

            return games.OrderBy(g => g.GameTime).ToList();
        }

        private string MapTeamNameToCode(string teamName)
        {
            return TeamNameToCode.GetValueOrDefault(teamName, "");
        }

        // Shared instance used by callers (e.g. simulation view models) to resolve a team
        // code to its display name and logo without needing to hit the Odds API.
        private static readonly Dictionary<string, (string Name, string Logo, string Code)> TeamInfoLookup =
            BuildTeamInfo();

        /// <summary>
        /// Resolves a CFB team code (case-insensitive) to its display name and logo URL.
        /// Returns the original code as the name and an empty logo when the code is unknown.
        /// </summary>
        public static (string Name, string Logo, string Code) GetTeamInfo(string teamCode)
        {
            if (!string.IsNullOrEmpty(teamCode) &&
                TeamInfoLookup.TryGetValue(teamCode.ToUpper(), out var info))
            {
                return info;
            }

            return (teamCode, string.Empty, teamCode);
        }

        /// <summary>True when the supplied code maps to a known Division I FBS team.</summary>
        public static bool IsKnownTeamCode(string teamCode)
            => !string.IsNullOrEmpty(teamCode) && TeamInfoLookup.ContainsKey(teamCode.ToUpper());

        // FBS conference groupings (team codes) used by the College Football teams landing page.
        private static readonly IReadOnlyList<(string Conference, string[] Codes)> ConferenceGroups = new List<(string, string[])>
        {
            ("ACC", new[] { "BC", "CAL", "CLEM", "DUKE", "FSU", "GT", "LOU", "MIA", "NCST", "UNC", "PITT", "SMU", "STAN", "SYR", "UVA", "VT", "WAKE" }),
            ("Big Ten", new[] { "ILL", "IND", "IOWA", "MD", "MICH", "MSU", "MINN", "NEB", "NW", "OSU", "ORE", "PSU", "PUR", "RUT", "UCLA", "USC", "WASH", "WISC" }),
            ("Big 12", new[] { "ARIZ", "ASU", "BAY", "BYU", "CIN", "COLO", "HOU", "ISU", "KU", "KSU", "OKST", "TCU", "TTU", "UCF", "UTAH", "WVU" }),
            ("SEC", new[] { "ALA", "ARK", "AUB", "FLA", "UGA", "UK", "LSU", "MSST", "MIZ", "OU", "MISS", "SC", "TENN", "TEX", "TAMU", "VAN" }),
            ("Pac-12", new[] { "ORST", "WSU" }),
            ("Independents", new[] { "ND", "CONN", "UMASS" }),
            ("American Athletic", new[] { "ARMY", "CHAR", "ECU", "FAU", "MEM", "NAVY", "UNT", "RICE", "USF", "TEM", "TUL", "TLSA", "UAB", "UTSA" }),
            ("Conference USA", new[] { "DEL", "FIU", "JVST", "KENN", "LIB", "LT", "MTSU", "MOST", "NMSU", "SHSU", "UTEP", "WKU" }),
            ("Mid-American", new[] { "AKR", "BALL", "BGSU", "BUFF", "CMU", "EMU", "KENT", "M-OH", "NIU", "OHIO", "TOL", "WMU" }),
            ("Mountain West", new[] { "AFA", "BSU", "CSU", "FRES", "HAW", "NEV", "UNM", "SDSU", "SJSU", "UNLV", "USU", "WYO" }),
            ("Sun Belt", new[] { "APP", "ARST", "CCU", "GASO", "GAST", "JMU", "UL", "ULM", "MRSH", "ODU", "USA", "USM", "TXST", "TROY" })
        };

        /// <summary>
        /// Returns all FBS teams grouped by conference, ordered as displayed on the landing page.
        /// Each team includes its display name, logo URL, and code.
        /// </summary>
        public static IReadOnlyList<(string Conference, IReadOnlyList<(string Name, string Logo, string Code)> Teams)> GetTeamsByConference()
        {
            var result = new List<(string, IReadOnlyList<(string, string, string)>)>();

            foreach (var (conference, codes) in ConferenceGroups)
            {
                var teams = codes
                    .Select(GetTeamInfo)
                    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                result.Add((conference, teams));
            }

            return result;
        }



        // Maps The Odds API team names (and common variants) to internal team codes.
        private static readonly Dictionary<string, string> TeamNameToCode = new(StringComparer.OrdinalIgnoreCase)
        {
            // ACC
            { "Boston College Eagles", "BC" }, { "Boston College", "BC" },
            { "California Golden Bears", "CAL" }, { "California", "CAL" },
            { "Clemson Tigers", "CLEM" }, { "Clemson", "CLEM" },
            { "Duke Blue Devils", "DUKE" }, { "Duke", "DUKE" },
            { "Florida State Seminoles", "FSU" }, { "Florida State", "FSU" },
            { "Georgia Tech Yellow Jackets", "GT" }, { "Georgia Tech", "GT" },
            { "Louisville Cardinals", "LOU" }, { "Louisville", "LOU" },
            { "Miami Hurricanes", "MIA" }, { "Miami (FL)", "MIA" }, { "Miami FL", "MIA" }, { "Miami", "MIA" },
            { "NC State Wolfpack", "NCST" }, { "North Carolina State", "NCST" }, { "NC State", "NCST" },
            { "North Carolina Tar Heels", "UNC" }, { "North Carolina", "UNC" },
            { "Pittsburgh Panthers", "PITT" }, { "Pittsburgh", "PITT" }, { "Pitt", "PITT" },
            { "SMU Mustangs", "SMU" }, { "SMU", "SMU" },
            { "Stanford Cardinal", "STAN" }, { "Stanford", "STAN" },
            { "Syracuse Orange", "SYR" }, { "Syracuse", "SYR" },
            { "Virginia Cavaliers", "UVA" }, { "Virginia", "UVA" },
            { "Virginia Tech Hokies", "VT" }, { "Virginia Tech", "VT" },
            { "Wake Forest Demon Deacons", "WAKE" }, { "Wake Forest", "WAKE" },

            // Big Ten
            { "Illinois Fighting Illini", "ILL" }, { "Illinois", "ILL" },
            { "Indiana Hoosiers", "IND" }, { "Indiana", "IND" },
            { "Iowa Hawkeyes", "IOWA" }, { "Iowa", "IOWA" },
            { "Maryland Terrapins", "MD" }, { "Maryland", "MD" },
            { "Michigan Wolverines", "MICH" }, { "Michigan", "MICH" },
            { "Michigan State Spartans", "MSU" }, { "Michigan State", "MSU" },
            { "Minnesota Golden Gophers", "MINN" }, { "Minnesota", "MINN" },
            { "Nebraska Cornhuskers", "NEB" }, { "Nebraska", "NEB" },
            { "Northwestern Wildcats", "NW" }, { "Northwestern", "NW" },
            { "Ohio State Buckeyes", "OSU" }, { "Ohio State", "OSU" },
            { "Oregon Ducks", "ORE" }, { "Oregon", "ORE" },
            { "Penn State Nittany Lions", "PSU" }, { "Penn State", "PSU" },
            { "Purdue Boilermakers", "PUR" }, { "Purdue", "PUR" },
            { "Rutgers Scarlet Knights", "RUT" }, { "Rutgers", "RUT" },
            { "UCLA Bruins", "UCLA" }, { "UCLA", "UCLA" },
            { "USC Trojans", "USC" }, { "USC", "USC" },
            { "Washington Huskies", "WASH" }, { "Washington", "WASH" },
            { "Wisconsin Badgers", "WISC" }, { "Wisconsin", "WISC" },

            // Big 12
            { "Arizona Wildcats", "ARIZ" }, { "Arizona", "ARIZ" },
            { "Arizona State Sun Devils", "ASU" }, { "Arizona State", "ASU" },
            { "Baylor Bears", "BAY" }, { "Baylor", "BAY" },
            { "BYU Cougars", "BYU" }, { "BYU", "BYU" },
            { "Cincinnati Bearcats", "CIN" }, { "Cincinnati", "CIN" },
            { "Colorado Buffaloes", "COLO" }, { "Colorado", "COLO" },
            { "Houston Cougars", "HOU" }, { "Houston", "HOU" },
            { "Iowa State Cyclones", "ISU" }, { "Iowa State", "ISU" },
            { "Kansas Jayhawks", "KU" }, { "Kansas", "KU" },
            { "Kansas State Wildcats", "KSU" }, { "Kansas State", "KSU" },
            { "Oklahoma State Cowboys", "OKST" }, { "Oklahoma State", "OKST" },
            { "TCU Horned Frogs", "TCU" }, { "TCU", "TCU" },
            { "Texas Tech Red Raiders", "TTU" }, { "Texas Tech", "TTU" },
            { "UCF Knights", "UCF" }, { "UCF", "UCF" }, { "Central Florida", "UCF" },
            { "Utah Utes", "UTAH" }, { "Utah", "UTAH" },
            { "West Virginia Mountaineers", "WVU" }, { "West Virginia", "WVU" },

            // SEC
            { "Alabama Crimson Tide", "ALA" }, { "Alabama", "ALA" },
            { "Arkansas Razorbacks", "ARK" }, { "Arkansas", "ARK" },
            { "Auburn Tigers", "AUB" }, { "Auburn", "AUB" },
            { "Florida Gators", "FLA" }, { "Florida", "FLA" },
            { "Georgia Bulldogs", "UGA" }, { "Georgia", "UGA" },
            { "Kentucky Wildcats", "UK" }, { "Kentucky", "UK" },
            { "LSU Tigers", "LSU" }, { "LSU", "LSU" },
            { "Mississippi State Bulldogs", "MSST" }, { "Mississippi State", "MSST" },
            { "Missouri Tigers", "MIZ" }, { "Missouri", "MIZ" },
            { "Oklahoma Sooners", "OU" }, { "Oklahoma", "OU" },
            { "Ole Miss Rebels", "MISS" }, { "Mississippi", "MISS" }, { "Ole Miss", "MISS" },
            { "South Carolina Gamecocks", "SC" }, { "South Carolina", "SC" },
            { "Tennessee Volunteers", "TENN" }, { "Tennessee", "TENN" },
            { "Texas Longhorns", "TEX" }, { "Texas", "TEX" },
            { "Texas A&M Aggies", "TAMU" }, { "Texas A&M", "TAMU" },
            { "Vanderbilt Commodores", "VAN" }, { "Vanderbilt", "VAN" },

            // Pac-12 (remaining)
            { "Oregon State Beavers", "ORST" }, { "Oregon State", "ORST" },
            { "Washington State Cougars", "WSU" }, { "Washington State", "WSU" },

            // Independents / Notre Dame / UConn / UMass
            { "Notre Dame Fighting Irish", "ND" }, { "Notre Dame", "ND" },
            { "UConn Huskies", "CONN" }, { "Connecticut", "CONN" }, { "UConn", "CONN" },
            { "UMass Minutemen", "UMASS" }, { "UMass", "UMASS" }, { "Massachusetts", "UMASS" },

            // American Athletic (AAC)
            { "Army Black Knights", "ARMY" }, { "Army", "ARMY" },
            { "Charlotte 49ers", "CHAR" }, { "Charlotte", "CHAR" },
            { "East Carolina Pirates", "ECU" }, { "East Carolina", "ECU" },
            { "Florida Atlantic Owls", "FAU" }, { "Florida Atlantic", "FAU" },
            { "Memphis Tigers", "MEM" }, { "Memphis", "MEM" },
            { "Navy Midshipmen", "NAVY" }, { "Navy", "NAVY" },
            { "North Texas Mean Green", "UNT" }, { "North Texas", "UNT" },
            { "Rice Owls", "RICE" }, { "Rice", "RICE" },
            { "South Florida Bulls", "USF" }, { "South Florida", "USF" },
            { "Temple Owls", "TEM" }, { "Temple", "TEM" },
            { "Tulane Green Wave", "TUL" }, { "Tulane", "TUL" },
            { "Tulsa Golden Hurricane", "TLSA" }, { "Tulsa", "TLSA" },
            { "UAB Blazers", "UAB" }, { "UAB", "UAB" },
            { "UTSA Roadrunners", "UTSA" }, { "UTSA", "UTSA" },

            // Conference USA (C-USA)
            { "Delaware Fightin' Blue Hens", "DEL" }, { "Delaware", "DEL" },
            { "Florida International Panthers", "FIU" }, { "Florida International", "FIU" }, { "FIU", "FIU" },
            { "Jacksonville State Gamecocks", "JVST" }, { "Jacksonville State", "JVST" },
            { "Kennesaw State Owls", "KENN" }, { "Kennesaw State", "KENN" },
            { "Liberty Flames", "LIB" }, { "Liberty", "LIB" },
            { "Louisiana Tech Bulldogs", "LT" }, { "Louisiana Tech", "LT" },
            { "Middle Tennessee Blue Raiders", "MTSU" }, { "Middle Tennessee", "MTSU" }, { "Middle Tennessee State", "MTSU" },
            { "Missouri State Bears", "MOST" }, { "Missouri State", "MOST" },
            { "New Mexico State Aggies", "NMSU" }, { "New Mexico State", "NMSU" },
            { "Sam Houston Bearkats", "SHSU" }, { "Sam Houston", "SHSU" }, { "Sam Houston State", "SHSU" },
            { "UTEP Miners", "UTEP" }, { "UTEP", "UTEP" },
            { "Western Kentucky Hilltoppers", "WKU" }, { "Western Kentucky", "WKU" },

            // Mid-American (MAC)
            { "Akron Zips", "AKR" }, { "Akron", "AKR" },
            { "Ball State Cardinals", "BALL" }, { "Ball State", "BALL" },
            { "Bowling Green Falcons", "BGSU" }, { "Bowling Green", "BGSU" },
            { "Buffalo Bulls", "BUFF" }, { "Buffalo", "BUFF" },
            { "Central Michigan Chippewas", "CMU" }, { "Central Michigan", "CMU" },
            { "Eastern Michigan Eagles", "EMU" }, { "Eastern Michigan", "EMU" },
            { "Kent State Golden Flashes", "KENT" }, { "Kent State", "KENT" },
            { "Miami (OH) RedHawks", "M-OH" }, { "Miami (OH)", "M-OH" }, { "Miami Ohio", "M-OH" }, { "Miami OH", "M-OH" },
            { "Northern Illinois Huskies", "NIU" }, { "Northern Illinois", "NIU" },
            { "Ohio Bobcats", "OHIO" }, { "Ohio", "OHIO" },
            { "Toledo Rockets", "TOL" }, { "Toledo", "TOL" },
            { "Western Michigan Broncos", "WMU" }, { "Western Michigan", "WMU" },

            // Mountain West
            { "Air Force Falcons", "AFA" }, { "Air Force", "AFA" },
            { "Boise State Broncos", "BSU" }, { "Boise State", "BSU" },
            { "Colorado State Rams", "CSU" }, { "Colorado State", "CSU" },
            { "Fresno State Bulldogs", "FRES" }, { "Fresno State", "FRES" },
            { "Hawai'i Rainbow Warriors", "HAW" }, { "Hawaii Rainbow Warriors", "HAW" }, { "Hawai'i", "HAW" }, { "Hawaii", "HAW" },
            { "Nevada Wolf Pack", "NEV" }, { "Nevada", "NEV" },
            { "New Mexico Lobos", "UNM" }, { "New Mexico", "UNM" },
            { "San Diego State Aztecs", "SDSU" }, { "San Diego State", "SDSU" },
            { "San Jose State Spartans", "SJSU" }, { "San Jose State", "SJSU" },
            { "UNLV Rebels", "UNLV" }, { "UNLV", "UNLV" },
            { "Utah State Aggies", "USU" }, { "Utah State", "USU" },
            { "Wyoming Cowboys", "WYO" }, { "Wyoming", "WYO" },

            // Sun Belt
            { "Appalachian State Mountaineers", "APP" }, { "Appalachian State", "APP" }, { "App State", "APP" },
            { "Arkansas State Red Wolves", "ARST" }, { "Arkansas State", "ARST" },
            { "Coastal Carolina Chanticleers", "CCU" }, { "Coastal Carolina", "CCU" },
            { "Georgia Southern Eagles", "GASO" }, { "Georgia Southern", "GASO" },
            { "Georgia State Panthers", "GAST" }, { "Georgia State", "GAST" },
            { "James Madison Dukes", "JMU" }, { "James Madison", "JMU" },
            { "Louisiana Ragin' Cajuns", "UL" }, { "Louisiana", "UL" }, { "Louisiana-Lafayette", "UL" }, { "Louisiana Lafayette", "UL" },
            { "Louisiana-Monroe Warhawks", "ULM" }, { "Louisiana Monroe", "ULM" }, { "UL Monroe", "ULM" },
            { "Marshall Thundering Herd", "MRSH" }, { "Marshall", "MRSH" },
            { "Old Dominion Monarchs", "ODU" }, { "Old Dominion", "ODU" },
            { "South Alabama Jaguars", "USA" }, { "South Alabama", "USA" },
            { "Southern Miss Golden Eagles", "USM" }, { "Southern Miss", "USM" }, { "Southern Mississippi", "USM" },
            { "Texas State Bobcats", "TXST" }, { "Texas State", "TXST" },
            { "Troy Trojans", "TROY" }, { "Troy", "TROY" }
        };

        private Dictionary<string, (string Name, string Logo, string Code)> InitializeTeamInfo()
        {
            return BuildTeamInfo();
        }

        private static Dictionary<string, (string Name, string Logo, string Code)> BuildTeamInfo()
        {
            static (string, string, string) T(string name, string id, string code)
                => (name, $"https://a.espncdn.com/i/teamlogos/ncaa/500/{id}.png", code);

            return new Dictionary<string, (string Name, string Logo, string Code)>
            {
                // ACC
                { "BC", T("Boston College", "103", "BC") },
                { "CAL", T("California", "25", "CAL") },
                { "CLEM", T("Clemson", "228", "CLEM") },
                { "DUKE", T("Duke", "150", "DUKE") },
                { "FSU", T("Florida State", "52", "FSU") },
                { "GT", T("Georgia Tech", "59", "GT") },
                { "LOU", T("Louisville", "97", "LOU") },
                { "MIA", T("Miami", "2390", "MIA") },
                { "NCST", T("NC State", "152", "NCST") },
                { "UNC", T("North Carolina", "153", "UNC") },
                { "PITT", T("Pittsburgh", "221", "PITT") },
                { "SMU", T("SMU", "2567", "SMU") },
                { "STAN", T("Stanford", "24", "STAN") },
                { "SYR", T("Syracuse", "183", "SYR") },
                { "UVA", T("Virginia", "258", "UVA") },
                { "VT", T("Virginia Tech", "259", "VT") },
                { "WAKE", T("Wake Forest", "154", "WAKE") },

                // Big Ten
                { "ILL", T("Illinois", "356", "ILL") },
                { "IND", T("Indiana", "84", "IND") },
                { "IOWA", T("Iowa", "2294", "IOWA") },
                { "MD", T("Maryland", "120", "MD") },
                { "MICH", T("Michigan", "130", "MICH") },
                { "MSU", T("Michigan State", "127", "MSU") },
                { "MINN", T("Minnesota", "135", "MINN") },
                { "NEB", T("Nebraska", "158", "NEB") },
                { "NW", T("Northwestern", "77", "NW") },
                { "OSU", T("Ohio State", "194", "OSU") },
                { "ORE", T("Oregon", "2483", "ORE") },
                { "PSU", T("Penn State", "213", "PSU") },
                { "PUR", T("Purdue", "2509", "PUR") },
                { "RUT", T("Rutgers", "164", "RUT") },
                { "UCLA", T("UCLA", "26", "UCLA") },
                { "USC", T("USC", "30", "USC") },
                { "WASH", T("Washington", "264", "WASH") },
                { "WISC", T("Wisconsin", "275", "WISC") },

                // Big 12
                { "ARIZ", T("Arizona", "12", "ARIZ") },
                { "ASU", T("Arizona State", "9", "ASU") },
                { "BAY", T("Baylor", "239", "BAY") },
                { "BYU", T("BYU", "252", "BYU") },
                { "CIN", T("Cincinnati", "2132", "CIN") },
                { "COLO", T("Colorado", "38", "COLO") },
                { "HOU", T("Houston", "248", "HOU") },
                { "ISU", T("Iowa State", "66", "ISU") },
                { "KU", T("Kansas", "2305", "KU") },
                { "KSU", T("Kansas State", "2306", "KSU") },
                { "OKST", T("Oklahoma State", "197", "OKST") },
                { "TCU", T("TCU", "2628", "TCU") },
                { "TTU", T("Texas Tech", "2641", "TTU") },
                { "UCF", T("UCF", "2116", "UCF") },
                { "UTAH", T("Utah", "254", "UTAH") },
                { "WVU", T("West Virginia", "277", "WVU") },

                // SEC
                { "ALA", T("Alabama", "333", "ALA") },
                { "ARK", T("Arkansas", "8", "ARK") },
                { "AUB", T("Auburn", "2", "AUB") },
                { "FLA", T("Florida", "57", "FLA") },
                { "UGA", T("Georgia", "61", "UGA") },
                { "UK", T("Kentucky", "96", "UK") },
                { "LSU", T("LSU", "99", "LSU") },
                { "MSST", T("Mississippi State", "344", "MSST") },
                { "MIZ", T("Missouri", "142", "MIZ") },
                { "OU", T("Oklahoma", "201", "OU") },
                { "MISS", T("Ole Miss", "145", "MISS") },
                { "SC", T("South Carolina", "2579", "SC") },
                { "TENN", T("Tennessee", "2633", "TENN") },
                { "TEX", T("Texas", "251", "TEX") },
                { "TAMU", T("Texas A&M", "245", "TAMU") },
                { "VAN", T("Vanderbilt", "238", "VAN") },

                // Pac-12 remaining
                { "ORST", T("Oregon State", "204", "ORST") },
                { "WSU", T("Washington State", "265", "WSU") },

                // Independents
                { "ND", T("Notre Dame", "87", "ND") },
                { "CONN", T("UConn", "41", "CONN") },
                { "UMASS", T("UMass", "113", "UMASS") },

                // American Athletic
                { "ARMY", T("Army", "349", "ARMY") },
                { "CHAR", T("Charlotte", "2429", "CHAR") },
                { "ECU", T("East Carolina", "151", "ECU") },
                { "FAU", T("Florida Atlantic", "2226", "FAU") },
                { "MEM", T("Memphis", "235", "MEM") },
                { "NAVY", T("Navy", "2426", "NAVY") },
                { "UNT", T("North Texas", "249", "UNT") },
                { "RICE", T("Rice", "242", "RICE") },
                { "USF", T("South Florida", "58", "USF") },
                { "TEM", T("Temple", "218", "TEM") },
                { "TUL", T("Tulane", "2655", "TUL") },
                { "TLSA", T("Tulsa", "202", "TLSA") },
                { "UAB", T("UAB", "5", "UAB") },
                { "UTSA", T("UTSA", "2636", "UTSA") },

                // Conference USA
                { "DEL", T("Delaware", "48", "DEL") },
                { "FIU", T("Florida International", "2229", "FIU") },
                { "JVST", T("Jacksonville State", "55", "JVST") },
                { "KENN", T("Kennesaw State", "338", "KENN") },
                { "LIB", T("Liberty", "2335", "LIB") },
                { "LT", T("Louisiana Tech", "2348", "LT") },
                { "MTSU", T("Middle Tennessee", "2393", "MTSU") },
                { "MOST", T("Missouri State", "2623", "MOST") },
                { "NMSU", T("New Mexico State", "166", "NMSU") },
                { "SHSU", T("Sam Houston", "2534", "SHSU") },
                { "UTEP", T("UTEP", "2638", "UTEP") },
                { "WKU", T("Western Kentucky", "98", "WKU") },

                // Mid-American
                { "AKR", T("Akron", "2006", "AKR") },
                { "BALL", T("Ball State", "2050", "BALL") },
                { "BGSU", T("Bowling Green", "189", "BGSU") },
                { "BUFF", T("Buffalo", "2084", "BUFF") },
                { "CMU", T("Central Michigan", "2117", "CMU") },
                { "EMU", T("Eastern Michigan", "2199", "EMU") },
                { "KENT", T("Kent State", "2309", "KENT") },
                { "M-OH", T("Miami (OH)", "193", "M-OH") },
                { "NIU", T("Northern Illinois", "2459", "NIU") },
                { "OHIO", T("Ohio", "195", "OHIO") },
                { "TOL", T("Toledo", "2649", "TOL") },
                { "WMU", T("Western Michigan", "2711", "WMU") },

                // Mountain West
                { "AFA", T("Air Force", "2005", "AFA") },
                { "BSU", T("Boise State", "68", "BSU") },
                { "CSU", T("Colorado State", "36", "CSU") },
                { "FRES", T("Fresno State", "278", "FRES") },
                { "HAW", T("Hawai'i", "62", "HAW") },
                { "NEV", T("Nevada", "2440", "NEV") },
                { "UNM", T("New Mexico", "167", "UNM") },
                { "SDSU", T("San Diego State", "21", "SDSU") },
                { "SJSU", T("San Jose State", "23", "SJSU") },
                { "UNLV", T("UNLV", "2439", "UNLV") },
                { "USU", T("Utah State", "328", "USU") },
                { "WYO", T("Wyoming", "2751", "WYO") },

                // Sun Belt
                { "APP", T("Appalachian State", "2026", "APP") },
                { "ARST", T("Arkansas State", "2032", "ARST") },
                { "CCU", T("Coastal Carolina", "324", "CCU") },
                { "GASO", T("Georgia Southern", "290", "GASO") },
                { "GAST", T("Georgia State", "2247", "GAST") },
                { "JMU", T("James Madison", "256", "JMU") },
                { "UL", T("Louisiana", "309", "UL") },
                { "ULM", T("Louisiana-Monroe", "2433", "ULM") },
                { "MRSH", T("Marshall", "276", "MRSH") },
                { "ODU", T("Old Dominion", "295", "ODU") },
                { "USA", T("South Alabama", "6", "USA") },
                { "USM", T("Southern Miss", "2572", "USM") },
                { "TXST", T("Texas State", "326", "TXST") },
                { "TROY", T("Troy", "2653", "TROY") }
            };
        }
    }
}

using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class CBBGamesService : ICBBGamesService
    {
        private readonly ILogger<CBBGamesService> _logger;
        private readonly TheOddsApiClient _oddsApiClient;
        private readonly Dictionary<string, (string Name, string Logo, string Code)> _teamInfo;

        public CBBGamesService(
            ILogger<CBBGamesService> logger,
            TheOddsApiClient oddsApiClient)
        {
            _logger = logger;
            _oddsApiClient = oddsApiClient;
            _teamInfo = InitializeTeamInfo();
        }

        public async Task<List<CBBGameMatchup>> GetUpcomingCBBGamesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching NCAA Men's Basketball games from The Odds API");

            var oddsData = await _oddsApiClient.GetAsync("/v4/sports/basketball_ncaab/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american", cancellationToken);

            var games = ParseCBBOddsApiResponse(oddsData);

            if (games.Any())
            {
                _logger.LogInformation("Successfully fetched {Count} CBB games from The Odds API", games.Count);
                return games;
            }

            _logger.LogWarning("No CBB games available from The Odds API");
            return new List<CBBGameMatchup>();
        }

        private List<CBBGameMatchup> ParseCBBOddsApiResponse(JsonElement oddsData)
        {
            var games = new List<CBBGameMatchup>();

            if (oddsData.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected response format from The Odds API");
                return games;
            }

            var totalGames = oddsData.GetArrayLength();
            _logger.LogInformation("Processing {TotalGames} CBB games from The Odds API", totalGames);

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
                        _logger.LogWarning("Could not map CBB teams: {Home} / {Away}", homeTeam, awayTeam);

                        if (string.IsNullOrEmpty(homeTeamCode)) unmappedTeams.Add(homeTeam);
                        if (string.IsNullOrEmpty(awayTeamCode)) unmappedTeams.Add(awayTeam);

                        skippedGames++;
                        continue;
                    }

                    var homeInfo = _teamInfo.GetValueOrDefault(homeTeamCode);
                    var awayInfo = _teamInfo.GetValueOrDefault(awayTeamCode);

                    if (homeInfo == default || awayInfo == default)
                    {
                        _logger.LogWarning("CBB team code lookup failed: {HomeCode} or {AwayCode}", homeTeamCode, awayTeamCode);
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

                    games.Add(new CBBGameMatchup
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
                    _logger.LogWarning(ex, "Error parsing CBB game from Odds API");
                    skippedGames++;
                }
            }

            if (unmappedTeams.Any())
            {
                _logger.LogWarning("⚠️ Unmapped CBB teams: {UnmappedTeams}", string.Join(", ", unmappedTeams));
            }

            _logger.LogInformation("CBB Parsing complete: {ParsedGames}/{TotalGames} games, {SkippedGames} skipped",
                games.Count, totalGames, skippedGames);

            return games.OrderBy(g => g.GameTime).ToList();
        }

        private string MapTeamNameToCode(string teamName)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // ACC
                { "Duke Blue Devils", "DUKE" }, { "Duke", "DUKE" },
                { "North Carolina Tar Heels", "UNC" }, { "North Carolina", "UNC" },
                { "Virginia Cavaliers", "UVA" }, { "Virginia", "UVA" },
                { "Miami Hurricanes", "MIA" }, { "Miami (FL)", "MIA" }, { "Miami FL", "MIA" }, { "Miami", "MIA" },
                { "Clemson Tigers", "CLEM" }, { "Clemson", "CLEM" },
                { "NC State Wolfpack", "NCST" }, { "North Carolina State", "NCST" }, { "NC State", "NCST" },
                { "Wake Forest Demon Deacons", "WAKE" }, { "Wake Forest", "WAKE" },
                { "Syracuse Orange", "SYR" }, { "Syracuse", "SYR" },
                { "Pittsburgh Panthers", "PITT" }, { "Pittsburgh", "PITT" },
                { "Louisville Cardinals", "LOU" }, { "Louisville", "LOU" },
                { "Florida State Seminoles", "FSU" }, { "Florida State", "FSU" },
                { "Georgia Tech Yellow Jackets", "GT" }, { "Georgia Tech", "GT" },
                { "Virginia Tech Hokies", "VT" }, { "Virginia Tech", "VT" },
                { "Boston College Eagles", "BC" }, { "Boston College", "BC" },
                { "Notre Dame Fighting Irish", "ND" }, { "Notre Dame", "ND" },
                
                // Big Ten
                { "Michigan Wolverines", "MICH" }, { "Michigan", "MICH" },
                { "Ohio State Buckeyes", "OSU" }, { "Ohio State", "OSU" },
                { "Michigan State Spartans", "MSU" }, { "Michigan State", "MSU" },
                { "Illinois Fighting Illini", "ILL" }, { "Illinois", "ILL" },
                { "Indiana Hoosiers", "IND" }, { "Indiana", "IND" },
                { "Purdue Boilermakers", "PUR" }, { "Purdue", "PUR" },
                { "Wisconsin Badgers", "WISC" }, { "Wisconsin", "WISC" },
                { "Iowa Hawkeyes", "IOWA" }, { "Iowa", "IOWA" },
                { "Maryland Terrapins", "MD" }, { "Maryland", "MD" },
                { "Penn State Nittany Lions", "PSU" }, { "Penn State", "PSU" },
                { "Rutgers Scarlet Knights", "RUT" }, { "Rutgers", "RUT" },
                { "Northwestern Wildcats", "NW" }, { "Northwestern", "NW" },
                { "Minnesota Golden Gophers", "MINN" }, { "Minnesota", "MINN" },
                { "Nebraska Cornhuskers", "NEB" }, { "Nebraska", "NEB" },
                
                // Big 12
                { "Kansas Jayhawks", "KU" }, { "Kansas", "KU" },
                { "Baylor Bears", "BAY" }, { "Baylor", "BAY" },
                { "Texas Longhorns", "TEX" }, { "Texas", "TEX" },
                { "Texas Tech Red Raiders", "TTU" }, { "Texas Tech", "TTU" },
                { "Oklahoma Sooners", "OU" }, { "Oklahoma", "OU" },
                { "Oklahoma State Cowboys", "OKST" }, { "Oklahoma State", "OKST" },
                { "Iowa State Cyclones", "ISU" }, { "Iowa State", "ISU" },
                { "Kansas State Wildcats", "KSU" }, { "Kansas State", "KSU" },
                { "West Virginia Mountaineers", "WVU" }, { "West Virginia", "WVU" },
                { "TCU Horned Frogs", "TCU" }, { "TCU", "TCU" },
                { "BYU Cougars", "BYU" }, { "BYU", "BYU" },
                { "Cincinnati Bearcats", "CIN" }, { "Cincinnati", "CIN" },
                { "UCF Knights", "UCF" }, { "UCF", "UCF" },
                { "Houston Cougars", "HOU" }, { "Houston", "HOU" },
                
                // SEC
                { "Kentucky Wildcats", "UK" }, { "Kentucky", "UK" },
                { "Tennessee Volunteers", "TENN" }, { "Tennessee", "TENN" },
                { "Alabama Crimson Tide", "ALA" }, { "Alabama", "ALA" },
                { "Auburn Tigers", "AUB" }, { "Auburn", "AUB" },
                { "Florida Gators", "FLA" }, { "Florida", "FLA" },
                { "Arkansas Razorbacks", "ARK" }, { "Arkansas", "ARK" },
                { "LSU Tigers", "LSU" }, { "LSU", "LSU" },
                { "Mississippi State Bulldogs", "MSST" }, { "Mississippi State", "MSST" },
                { "Ole Miss Rebels", "MISS" }, { "Mississippi", "MISS" }, { "Ole Miss", "MISS" },
                { "Missouri Tigers", "MIZ" }, { "Missouri", "MIZ" },
                { "South Carolina Gamecocks", "SC" }, { "South Carolina", "SC" },
                { "Texas A&M Aggies", "TAMU" }, { "Texas A&M", "TAMU" },
                { "Vanderbilt Commodores", "VAN" }, { "Vanderbilt", "VAN" },
                { "Georgia Bulldogs", "UGA" }, { "Georgia", "UGA" },
                
                // Pac-12
                { "UCLA Bruins", "UCLA" }, { "UCLA", "UCLA" },
                { "Arizona Wildcats", "ARIZ" }, { "Arizona", "ARIZ" },
                { "USC Trojans", "USC" }, { "USC", "USC" },
                { "Oregon Ducks", "ORE" }, { "Oregon", "ORE" },
                { "Colorado Buffaloes", "COLO" }, { "Colorado", "COLO" },
                { "Washington Huskies", "WASH" }, { "Washington", "WASH" },
                { "Arizona State Sun Devils", "ASU" }, { "Arizona State", "ASU" },
                { "Utah Utes", "UTAH" }, { "Utah", "UTAH" },
                { "Oregon State Beavers", "ORST" }, { "Oregon State", "ORST" },
                { "Stanford Cardinal", "STAN" }, { "Stanford", "STAN" },
                { "California Golden Bears", "CAL" }, { "California", "CAL" },
                { "Washington State Cougars", "WSU" }, { "Washington State", "WSU" },
                
                // Big East
                { "Villanova Wildcats", "NOVA" }, { "Villanova", "NOVA" },
                { "UConn Huskies", "CONN" }, { "Connecticut", "CONN" }, { "UConn", "CONN" },
                { "Creighton Bluejays", "CREI" }, { "Creighton", "CREI" },
                { "Xavier Musketeers", "XAV" }, { "Xavier", "XAV" },
                { "Marquette Golden Eagles", "MARQ" }, { "Marquette", "MARQ" },
                { "Providence Friars", "PROV" }, { "Providence", "PROV" },
                { "Seton Hall Pirates", "SHU" }, { "Seton Hall", "SHU" },
                { "Butler Bulldogs", "BUT" }, { "Butler", "BUT" },
                { "Georgetown Hoyas", "GTWN" }, { "Georgetown", "GTWN" },
                { "St. John's Red Storm", "SJU" }, { "St. John's", "SJU" }, { "St John's", "SJU" },
                { "DePaul Blue Demons", "DEP" }, { "DePaul", "DEP" },
                
                // WCC
                { "Gonzaga Bulldogs", "GONZ" }, { "Gonzaga", "GONZ" },
                { "Saint Mary's Gaels", "SMC" }, { "Saint Mary's", "SMC" }, { "St Mary's", "SMC" },
                { "San Francisco Dons", "SF" }, { "San Francisco", "SF" },
                { "Santa Clara Broncos", "SCU" }, { "Santa Clara", "SCU" },
                { "Loyola Marymount Lions", "LMU" }, { "Loyola Marymount", "LMU" },
                { "Pepperdine Waves", "PEPP" }, { "Pepperdine", "PEPP" },
                { "Pacific Tigers", "PAC" }, { "Pacific", "PAC" },
                { "San Diego Toreros", "USD" }, { "San Diego", "USD" },
                { "Portland Pilots", "PORT" }, { "Portland", "PORT" },
                
                // Atlantic 10
                { "VCU Rams", "VCU" }, { "VCU", "VCU" },
                { "Dayton Flyers", "DAY" }, { "Dayton", "DAY" },
                { "Saint Louis Billikens", "SLU" }, { "Saint Louis", "SLU" }, { "St Louis", "SLU" },
                { "Richmond Spiders", "RICH" }, { "Richmond", "RICH" },
                { "Davidson Wildcats", "DAV" }, { "Davidson", "DAV" },
                { "Saint Joseph's Hawks", "SJO" }, { "Saint Joseph's", "SJO" }, { "St Joseph's", "SJO" },
                { "Rhode Island Rams", "URI" }, { "Rhode Island", "URI" },
                { "George Mason Patriots", "GMU" }, { "George Mason", "GMU" },
                { "La Salle Explorers", "LAS" }, { "La Salle", "LAS" },
                { "UMass Minutemen", "UMASS" }, { "UMass", "UMASS" }, { "Massachusetts", "UMASS" },
                { "Duquesne Dukes", "DUQ" }, { "Duquesne", "DUQ" },
                { "St. Bonaventure Bonnies", "SBU" }, { "St. Bonaventure", "SBU" }, { "St Bonaventure", "SBU" },
                { "Fordham Rams", "FOR" }, { "Fordham", "FOR" },
                { "George Washington Colonials", "GW" }, { "George Washington", "GW" },
                { "Loyola Chicago Ramblers", "LOY" }, { "Loyola Chicago", "LOY" },
                
                // American Athletic
                { "Memphis Tigers", "MEM" }, { "Memphis", "MEM" },
                { "Temple Owls", "TEM" }, { "Temple", "TEM" },
                { "SMU Mustangs", "SMU" }, { "SMU", "SMU" },
                { "Tulane Green Wave", "TUL" }, { "Tulane", "TUL" },
                { "Tulsa Golden Hurricane", "TLSA" }, { "Tulsa", "TLSA" },
                { "Wichita State Shockers", "WICH" }, { "Wichita State", "WICH" },
                { "East Carolina Pirates", "ECU" }, { "East Carolina", "ECU" },
                { "South Florida Bulls", "USF" }, { "South Florida", "USF" },
                { "UAB Blazers", "UAB" }, { "UAB", "UAB" },
                { "Charlotte 49ers", "CHAR" }, { "Charlotte", "CHAR" },
                { "Florida Atlantic Owls", "FAU" }, { "Florida Atlantic", "FAU" },
                { "North Texas Mean Green", "UNT" }, { "North Texas", "UNT" },
                { "Rice Owls", "RICE" }, { "Rice", "RICE" },
                { "UTSA Roadrunners", "UTSA" }, { "UTSA", "UTSA" },
                
                // Mountain West
                { "San Diego State Aztecs", "SDSU" }, { "San Diego State", "SDSU" },
                { "Boise State Broncos", "BSU" }, { "Boise State", "BSU" },
                { "Nevada Wolf Pack", "NEV" }, { "Nevada", "NEV" },
                { "New Mexico Lobos", "UNM" }, { "New Mexico", "UNM" },
                { "Colorado State Rams", "CSU" }, { "Colorado State", "CSU" },
                { "Wyoming Cowboys", "WYO" }, { "Wyoming", "WYO" },
                { "UNLV Rebels", "UNLV" }, { "UNLV", "UNLV" },
                { "Fresno State Bulldogs", "FRES" }, { "Fresno State", "FRES" },
                { "Air Force Falcons", "AFA" }, { "Air Force", "AFA" },
                { "Utah State Aggies", "USU" }, { "Utah State", "USU" },
                { "San Jose State Spartans", "SJSU" }, { "San Jose State", "SJSU" }
            };

            return mapping.GetValueOrDefault(teamName, "");
        }

        private Dictionary<string, (string Name, string Logo, string Code)> InitializeTeamInfo()
        {
            return new Dictionary<string, (string Name, string Logo, string Code)>
            {
                // ACC
                { "DUKE", ("Duke", "https://a.espncdn.com/i/teamlogos/ncaa/500/150.png", "DUKE") },
                { "UNC", ("North Carolina", "https://a.espncdn.com/i/teamlogos/ncaa/500/153.png", "UNC") },
                { "UVA", ("Virginia", "https://a.espncdn.com/i/teamlogos/ncaa/500/258.png", "UVA") },
                { "MIA", ("Miami", "https://a.espncdn.com/i/teamlogos/ncaa/500/2390.png", "MIA") },
                { "CLEM", ("Clemson", "https://a.espncdn.com/i/teamlogos/ncaa/500/228.png", "CLEM") },
                { "NCST", ("NC State", "https://a.espncdn.com/i/teamlogos/ncaa/500/152.png", "NCST") },
                { "WAKE", ("Wake Forest", "https://a.espncdn.com/i/teamlogos/ncaa/500/154.png", "WAKE") },
                { "SYR", ("Syracuse", "https://a.espncdn.com/i/teamlogos/ncaa/500/183.png", "SYR") },
                { "PITT", ("Pittsburgh", "https://a.espncdn.com/i/teamlogos/ncaa/500/221.png", "PITT") },
                { "LOU", ("Louisville", "https://a.espncdn.com/i/teamlogos/ncaa/500/97.png", "LOU") },
                { "FSU", ("Florida State", "https://a.espncdn.com/i/teamlogos/ncaa/500/52.png", "FSU") },
                { "GT", ("Georgia Tech", "https://a.espncdn.com/i/teamlogos/ncaa/500/59.png", "GT") },
                { "VT", ("Virginia Tech", "https://a.espncdn.com/i/teamlogos/ncaa/500/259.png", "VT") },
                { "BC", ("Boston College", "https://a.espncdn.com/i/teamlogos/ncaa/500/103.png", "BC") },
                { "ND", ("Notre Dame", "https://a.espncdn.com/i/teamlogos/ncaa/500/87.png", "ND") },
                
                // Big Ten
                { "MICH", ("Michigan", "https://a.espncdn.com/i/teamlogos/ncaa/500/130.png", "MICH") },
                { "OSU", ("Ohio State", "https://a.espncdn.com/i/teamlogos/ncaa/500/194.png", "OSU") },
                { "MSU", ("Michigan State", "https://a.espncdn.com/i/teamlogos/ncaa/500/127.png", "MSU") },
                { "ILL", ("Illinois", "https://a.espncdn.com/i/teamlogos/ncaa/500/356.png", "ILL") },
                { "IND", ("Indiana", "https://a.espncdn.com/i/teamlogos/ncaa/500/84.png", "IND") },
                { "PUR", ("Purdue", "https://a.espncdn.com/i/teamlogos/ncaa/500/2509.png", "PUR") },
                { "WISC", ("Wisconsin", "https://a.espncdn.com/i/teamlogos/ncaa/500/275.png", "WISC") },
                { "IOWA", ("Iowa", "https://a.espncdn.com/i/teamlogos/ncaa/500/2294.png", "IOWA") },
                { "MD", ("Maryland", "https://a.espncdn.com/i/teamlogos/ncaa/500/120.png", "MD") },
                { "PSU", ("Penn State", "https://a.espncdn.com/i/teamlogos/ncaa/500/213.png", "PSU") },
                { "RUT", ("Rutgers", "https://a.espncdn.com/i/teamlogos/ncaa/500/164.png", "RUT") },
                { "NW", ("Northwestern", "https://a.espncdn.com/i/teamlogos/ncaa/500/77.png", "NW") },
                { "MINN", ("Minnesota", "https://a.espncdn.com/i/teamlogos/ncaa/500/135.png", "MINN") },
                { "NEB", ("Nebraska", "https://a.espncdn.com/i/teamlogos/ncaa/500/158.png", "NEB") },
                
                // Big 12
                { "KU", ("Kansas", "https://a.espncdn.com/i/teamlogos/ncaa/500/2305.png", "KU") },
                { "BAY", ("Baylor", "https://a.espncdn.com/i/teamlogos/ncaa/500/239.png", "BAY") },
                { "TEX", ("Texas", "https://a.espncdn.com/i/teamlogos/ncaa/500/251.png", "TEX") },
                { "TTU", ("Texas Tech", "https://a.espncdn.com/i/teamlogos/ncaa/500/2641.png", "TTU") },
                { "OU", ("Oklahoma", "https://a.espncdn.com/i/teamlogos/ncaa/500/201.png", "OU") },
                { "OKST", ("Oklahoma State", "https://a.espncdn.com/i/teamlogos/ncaa/500/197.png", "OKST") },
                { "ISU", ("Iowa State", "https://a.espncdn.com/i/teamlogos/ncaa/500/66.png", "ISU") },
                { "KSU", ("Kansas State", "https://a.espncdn.com/i/teamlogos/ncaa/500/2306.png", "KSU") },
                { "WVU", ("West Virginia", "https://a.espncdn.com/i/teamlogos/ncaa/500/277.png", "WVU") },
                { "TCU", ("TCU", "https://a.espncdn.com/i/teamlogos/ncaa/500/2628.png", "TCU") },
                { "BYU", ("BYU", "https://a.espncdn.com/i/teamlogos/ncaa/500/252.png", "BYU") },
                { "CIN", ("Cincinnati", "https://a.espncdn.com/i/teamlogos/ncaa/500/2132.png", "CIN") },
                { "UCF", ("UCF", "https://a.espncdn.com/i/teamlogos/ncaa/500/2116.png", "UCF") },
                { "HOU", ("Houston", "https://a.espncdn.com/i/teamlogos/ncaa/500/248.png", "HOU") },
                
                // SEC
                { "UK", ("Kentucky", "https://a.espncdn.com/i/teamlogos/ncaa/500/96.png", "UK") },
                { "TENN", ("Tennessee", "https://a.espncdn.com/i/teamlogos/ncaa/500/2633.png", "TENN") },
                { "ALA", ("Alabama", "https://a.espncdn.com/i/teamlogos/ncaa/500/333.png", "ALA") },
                { "AUB", ("Auburn", "https://a.espncdn.com/i/teamlogos/ncaa/500/2.png", "AUB") },
                { "FLA", ("Florida", "https://a.espncdn.com/i/teamlogos/ncaa/500/57.png", "FLA") },
                { "ARK", ("Arkansas", "https://a.espncdn.com/i/teamlogos/ncaa/500/8.png", "ARK") },
                { "LSU", ("LSU", "https://a.espncdn.com/i/teamlogos/ncaa/500/99.png", "LSU") },
                { "MSST", ("Mississippi State", "https://a.espncdn.com/i/teamlogos/ncaa/500/344.png", "MSST") },
                { "MISS", ("Ole Miss", "https://a.espncdn.com/i/teamlogos/ncaa/500/145.png", "MISS") },
                { "MIZ", ("Missouri", "https://a.espncdn.com/i/teamlogos/ncaa/500/142.png", "MIZ") },
                { "SC", ("South Carolina", "https://a.espncdn.com/i/teamlogos/ncaa/500/2579.png", "SC") },
                { "TAMU", ("Texas A&M", "https://a.espncdn.com/i/teamlogos/ncaa/500/245.png", "TAMU") },
                { "VAN", ("Vanderbilt", "https://a.espncdn.com/i/teamlogos/ncaa/500/238.png", "VAN") },
                { "UGA", ("Georgia", "https://a.espncdn.com/i/teamlogos/ncaa/500/61.png", "UGA") },
                
                // Pac-12
                { "UCLA", ("UCLA", "https://a.espncdn.com/i/teamlogos/ncaa/500/26.png", "UCLA") },
                { "ARIZ", ("Arizona", "https://a.espncdn.com/i/teamlogos/ncaa/500/12.png", "ARIZ") },
                { "USC", ("USC", "https://a.espncdn.com/i/teamlogos/ncaa/500/30.png", "USC") },
                { "ORE", ("Oregon", "https://a.espncdn.com/i/teamlogos/ncaa/500/2483.png", "ORE") },
                { "COLO", ("Colorado", "https://a.espncdn.com/i/teamlogos/ncaa/500/38.png", "COLO") },
                { "WASH", ("Washington", "https://a.espncdn.com/i/teamlogos/ncaa/500/264.png", "WASH") },
                { "ASU", ("Arizona State", "https://a.espncdn.com/i/teamlogos/ncaa/500/9.png", "ASU") },
                { "UTAH", ("Utah", "https://a.espncdn.com/i/teamlogos/ncaa/500/254.png", "UTAH") },
                { "ORST", ("Oregon State", "https://a.espncdn.com/i/teamlogos/ncaa/500/204.png", "ORST") },
                { "STAN", ("Stanford", "https://a.espncdn.com/i/teamlogos/ncaa/500/24.png", "STAN") },
                { "CAL", ("California", "https://a.espncdn.com/i/teamlogos/ncaa/500/25.png", "CAL") },
                { "WSU", ("Washington State", "https://a.espncdn.com/i/teamlogos/ncaa/500/265.png", "WSU") },
                
                // Big East
                { "NOVA", ("Villanova", "https://a.espncdn.com/i/teamlogos/ncaa/500/222.png", "NOVA") },
                { "CONN", ("UConn", "https://a.espncdn.com/i/teamlogos/ncaa/500/41.png", "CONN") },
                { "CREI", ("Creighton", "https://a.espncdn.com/i/teamlogos/ncaa/500/156.png", "CREI") },
                { "XAV", ("Xavier", "https://a.espncdn.com/i/teamlogos/ncaa/500/2752.png", "XAV") },
                { "MARQ", ("Marquette", "https://a.espncdn.com/i/teamlogos/ncaa/500/269.png", "MARQ") },
                { "PROV", ("Providence", "https://a.espncdn.com/i/teamlogos/ncaa/500/2507.png", "PROV") },
                { "SHU", ("Seton Hall", "https://a.espncdn.com/i/teamlogos/ncaa/500/2550.png", "SHU") },
                { "BUT", ("Butler", "https://a.espncdn.com/i/teamlogos/ncaa/500/2086.png", "BUT") },
                { "GTWN", ("Georgetown", "https://a.espncdn.com/i/teamlogos/ncaa/500/46.png", "GTWN") },
                { "SJU", ("St. John's", "https://a.espncdn.com/i/teamlogos/ncaa/500/2599.png", "SJU") },
                { "DEP", ("DePaul", "https://a.espncdn.com/i/teamlogos/ncaa/500/305.png", "DEP") },
                
                // WCC
                { "GONZ", ("Gonzaga", "https://a.espncdn.com/i/teamlogos/ncaa/500/2250.png", "GONZ") },
                { "SMC", ("Saint Mary's", "https://a.espncdn.com/i/teamlogos/ncaa/500/2608.png", "SMC") },
                { "SF", ("San Francisco", "https://a.espncdn.com/i/teamlogos/ncaa/500/2539.png", "SF") },
                { "SCU", ("Santa Clara", "https://a.espncdn.com/i/teamlogos/ncaa/500/2541.png", "SCU") },
                { "LMU", ("Loyola Marymount", "https://a.espncdn.com/i/teamlogos/ncaa/500/2351.png", "LMU") },
                { "PEPP", ("Pepperdine", "https://a.espncdn.com/i/teamlogos/ncaa/500/2492.png", "PEPP") },
                { "PAC", ("Pacific", "https://a.espncdn.com/i/teamlogos/ncaa/500/279.png", "PAC") },
                { "USD", ("San Diego", "https://a.espncdn.com/i/teamlogos/ncaa/500/301.png", "USD") },
                { "PORT", ("Portland", "https://a.espncdn.com/i/teamlogos/ncaa/500/2501.png", "PORT") },
                
                // Atlantic 10
                { "VCU", ("VCU", "https://a.espncdn.com/i/teamlogos/ncaa/500/2670.png", "VCU") },
                { "DAY", ("Dayton", "https://a.espncdn.com/i/teamlogos/ncaa/500/2168.png", "DAY") },
                { "SLU", ("Saint Louis", "https://a.espncdn.com/i/teamlogos/ncaa/500/139.png", "SLU") },
                { "RICH", ("Richmond", "https://a.espncdn.com/i/teamlogos/ncaa/500/257.png", "RICH") },
                { "DAV", ("Davidson", "https://a.espncdn.com/i/teamlogos/ncaa/500/2166.png", "DAV") },
                { "SJO", ("Saint Joseph's", "https://a.espncdn.com/i/teamlogos/ncaa/500/2603.png", "SJO") },
                { "URI", ("Rhode Island", "https://a.espncdn.com/i/teamlogos/ncaa/500/227.png", "URI") },
                { "GMU", ("George Mason", "https://a.espncdn.com/i/teamlogos/ncaa/500/2244.png", "GMU") },
                { "LAS", ("La Salle", "https://a.espncdn.com/i/teamlogos/ncaa/500/2325.png", "LAS") },
                { "UMASS", ("UMass", "https://a.espncdn.com/i/teamlogos/ncaa/500/113.png", "UMASS") },
                { "DUQ", ("Duquesne", "https://a.espncdn.com/i/teamlogos/ncaa/500/2181.png", "DUQ") },
                { "SBU", ("St. Bonaventure", "https://a.espncdn.com/i/teamlogos/ncaa/500/179.png", "SBU") },
                { "FOR", ("Fordham", "https://a.espncdn.com/i/teamlogos/ncaa/500/2230.png", "FOR") },
                { "GW", ("George Washington", "https://a.espncdn.com/i/teamlogos/ncaa/500/45.png", "GW") },
                { "LOY", ("Loyola Chicago", "https://a.espncdn.com/i/teamlogos/ncaa/500/2350.png", "LOY") },
                
                // American Athletic
                { "MEM", ("Memphis", "https://a.espncdn.com/i/teamlogos/ncaa/500/235.png", "MEM") },
                { "TEM", ("Temple", "https://a.espncdn.com/i/teamlogos/ncaa/500/218.png", "TEM") },
                { "SMU", ("SMU", "https://a.espncdn.com/i/teamlogos/ncaa/500/2567.png", "SMU") },
                { "TUL", ("Tulane", "https://a.espncdn.com/i/teamlogos/ncaa/500/2655.png", "TUL") },
                { "TLSA", ("Tulsa", "https://a.espncdn.com/i/teamlogos/ncaa/500/202.png", "TLSA") },
                { "WICH", ("Wichita State", "https://a.espncdn.com/i/teamlogos/ncaa/500/2724.png", "WICH") },
                { "ECU", ("East Carolina", "https://a.espncdn.com/i/teamlogos/ncaa/500/151.png", "ECU") },
                { "USF", ("South Florida", "https://a.espncdn.com/i/teamlogos/ncaa/500/58.png", "USF") },
                { "UAB", ("UAB", "https://a.espncdn.com/i/teamlogos/ncaa/500/5.png", "UAB") },
                { "CHAR", ("Charlotte", "https://a.espncdn.com/i/teamlogos/ncaa/500/2429.png", "CHAR") },
                { "FAU", ("Florida Atlantic", "https://a.espncdn.com/i/teamlogos/ncaa/500/2226.png", "FAU") },
                { "UNT", ("North Texas", "https://a.espncdn.com/i/teamlogos/ncaa/500/249.png", "UNT") },
                { "RICE", ("Rice", "https://a.espncdn.com/i/teamlogos/ncaa/500/242.png", "RICE") },
                { "UTSA", ("UTSA", "https://a.espncdn.com/i/teamlogos/ncaa/500/2636.png", "UTSA") },
                
                // Mountain West
                { "SDSU", ("San Diego State", "https://a.espncdn.com/i/teamlogos/ncaa/500/21.png", "SDSU") },
                { "BSU", ("Boise State", "https://a.espncdn.com/i/teamlogos/ncaa/500/68.png", "BSU") },
                { "NEV", ("Nevada", "https://a.espncdn.com/i/teamlogos/ncaa/500/2440.png", "NEV") },
                { "UNM", ("New Mexico", "https://a.espncdn.com/i/teamlogos/ncaa/500/167.png", "UNM") },
                { "CSU", ("Colorado State", "https://a.espncdn.com/i/teamlogos/ncaa/500/36.png", "CSU") },
                { "WYO", ("Wyoming", "https://a.espncdn.com/i/teamlogos/ncaa/500/2751.png", "WYO") },
                { "UNLV", ("UNLV", "https://a.espncdn.com/i/teamlogos/ncaa/500/2439.png", "UNLV") },
                { "FRES", ("Fresno State", "https://a.espncdn.com/i/teamlogos/ncaa/500/278.png", "FRES") },
                { "AFA", ("Air Force", "https://a.espncdn.com/i/teamlogos/ncaa/500/2005.png", "AFA") },
                { "USU", ("Utah State", "https://a.espncdn.com/i/teamlogos/ncaa/500/328.png", "USU") },
                { "SJSU", ("San Jose State", "https://a.espncdn.com/i/teamlogos/ncaa/500/23.png", "SJSU") }
            };
        }
    }
}
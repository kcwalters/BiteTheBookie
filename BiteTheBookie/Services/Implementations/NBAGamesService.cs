using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations
{
    public class NBAGamesService : INBAGamesService
    {
        private readonly ILogger<NBAGamesService> _logger;
        private readonly TheOddsApiClient _oddsApiClient;
        private readonly INBAScoresService _scoresService;
        private readonly Dictionary<string, (string Name, string Logo, string Code)> _teamInfo;

        public NBAGamesService(
            ILogger<NBAGamesService> logger,
            TheOddsApiClient oddsApiClient,
            INBAScoresService scoresService)
        {
            _logger = logger;
            _oddsApiClient = oddsApiClient;
            _scoresService = scoresService;
            _teamInfo = InitializeTeamInfo();
        }

        public async Task<List<NBAGameMatchup>> GetUpcomingNBAGamesAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Fetching NBA games from The Odds API and ESPN Scores");
            
            var oddsData = await _oddsApiClient.GetAsync("/v4/sports/basketball_nba/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american", cancellationToken);
            var games = ParseNBAOddsApiResponse(oddsData);
            
            if (!games.Any())
            {
                _logger.LogWarning("No games available from The Odds API");
                return new List<NBAGameMatchup>();
            }
            
            try
            {
                var liveScores = await _scoresService.GetGamesAsync(cancellationToken);
                _logger.LogInformation("Fetched {Count} live scores from ESPN", liveScores.Count);
                MergeScoresWithGames(games, liveScores);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch live scores from ESPN, continuing with odds data only");
            }
            
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);
            var todayAndTomorrowGames = games
                .Where(g => g.GameTime.Date == today || g.GameTime.Date == tomorrow)
                .OrderBy(g => g.GameTime)
                .ToList();
            
            _logger.LogInformation("Returning {Count} games for today and tomorrow ({Today} - {Tomorrow})", 
                todayAndTomorrowGames.Count, today.ToString("yyyy-MM-dd"), tomorrow.ToString("yyyy-MM-dd"));
            
            return todayAndTomorrowGames;
        }

        private List<NBAGameMatchup> ParseNBAOddsApiResponse(JsonElement oddsData)
        {
            var games = new List<NBAGameMatchup>();

            if (oddsData.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Unexpected response format from The Odds API");
                return games;
            }

            var totalGames = oddsData.GetArrayLength();
            _logger.LogInformation("Processing {TotalGames} games from The Odds API", totalGames);
            
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
                        commenceTime = DateTime.SpecifyKind(commenceTime, DateTimeKind.Utc);

                    var homeTeamCode = MapTeamNameToCode(homeTeam);
                    var awayTeamCode = MapTeamNameToCode(awayTeam);

                    if (string.IsNullOrEmpty(homeTeamCode) || string.IsNullOrEmpty(awayTeamCode))
                    {
                        _logger.LogWarning("Could not map teams: {Home} ({HomeCode}) / {Away} ({AwayCode})", 
                            homeTeam, homeTeamCode, awayTeam, awayTeamCode);
                        
                        if (string.IsNullOrEmpty(homeTeamCode)) unmappedTeams.Add(homeTeam);
                        if (string.IsNullOrEmpty(awayTeamCode)) unmappedTeams.Add(awayTeam);
                        
                        skippedGames++;
                        continue;
                    }

                    var homeInfo = _teamInfo.GetValueOrDefault(homeTeamCode);
                    var awayInfo = _teamInfo.GetValueOrDefault(awayTeamCode);

                    if (homeInfo == default || awayInfo == default)
                    {
                        _logger.LogWarning("Team code lookup failed: {HomeCode} or {AwayCode} not found in team info dictionary", 
                            homeTeamCode, awayTeamCode);
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
                                            spread = homeOutcome.GetProperty("point").GetDecimal();
                                    }
                                    else if (marketKey == "totals" && !overUnder.HasValue)
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        if (outcomes.Any())
                                            overUnder = outcomes.First().GetProperty("point").GetDecimal();
                                    }
                                    else if (marketKey == "h2h")
                                    {
                                        var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();
                                        var homeOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == homeTeam);
                                        var awayOutcome = outcomes.FirstOrDefault(o => o.GetProperty("name").GetString() == awayTeam);
                                        
                                        if (homeOutcome.ValueKind != JsonValueKind.Undefined && !homeMoneyline.HasValue)
                                            homeMoneyline = homeOutcome.GetProperty("price").GetInt32();
                                        if (awayOutcome.ValueKind != JsonValueKind.Undefined && !awayMoneyline.HasValue)
                                            awayMoneyline = awayOutcome.GetProperty("price").GetInt32();
                                    }
                                }
                            }

                            if (spread.HasValue && overUnder.HasValue)
                                break;
                        }
                    }

                    games.Add(new NBAGameMatchup
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
                    _logger.LogWarning(ex, "Error parsing game from Odds API");
                    skippedGames++;
                }
            }

            if (unmappedTeams.Any())
                _logger.LogWarning("⚠️ Unmapped teams detected: {UnmappedTeams}. Add these to MapTeamNameToCode().", 
                    string.Join(", ", unmappedTeams));

            _logger.LogInformation("Parsing complete: {ParsedGames}/{TotalGames} games parsed successfully, {SkippedGames} skipped", 
                games.Count, totalGames, skippedGames);

            return games.OrderBy(g => g.GameTime).ToList();
        }

        private string MapTeamNameToCode(string teamName)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Atlanta Hawks",            "ATL" },
                { "Boston Celtics",           "BOS" },
                { "Brooklyn Nets",            "BKN" },
                { "Charlotte Hornets",        "CHA" },
                { "Chicago Bulls",            "CHI" },
                { "Cleveland Cavaliers",      "CLE" },
                { "Dallas Mavericks",         "DAL" },
                { "Denver Nuggets",           "DEN" },
                { "Detroit Pistons",          "DET" },
                { "Golden State Warriors",    "GSW" },
                { "Houston Rockets",          "HOU" },
                { "Indiana Pacers",           "IND" },
                { "Los Angeles Clippers",     "LAC" },
                { "LA Clippers",              "LAC" },
                { "Los Angeles Lakers",       "LAL" },
                { "Memphis Grizzlies",        "MEM" },
                { "Miami Heat",               "MIA" },
                { "Milwaukee Bucks",          "MIL" },
                { "Minnesota Timberwolves",   "MIN" },
                { "New Orleans Pelicans",     "NOP" },
                { "New York Knicks",          "NYK" },
                { "Oklahoma City Thunder",    "OKC" },
                { "Orlando Magic",            "ORL" },
                { "Philadelphia 76ers",       "PHI" },
                { "Phoenix Suns",             "PHX" },
                { "Portland Trail Blazers",   "POR" },
                { "Sacramento Kings",         "SAC" },
                { "San Antonio Spurs",        "SAS" },
                { "Toronto Raptors",          "TOR" },
                { "Utah Jazz",                "UTA" },
                { "Washington Wizards",       "WAS" }
            };

            return mapping.GetValueOrDefault(teamName, "");
        }

        private void MergeScoresWithGames(List<NBAGameMatchup> games, IReadOnlyList<NBATickerView> liveScores)
        {
            _logger.LogInformation("Merging {ScoreCount} live scores with {GameCount} games", liveScores.Count, games.Count);
            
            foreach (var game in games)
            {
                var found = false;
                NBATickerView liveGame = default;
                foreach (var s in liveScores)
                {
                    if (s.AwayTeam?.Equals(game.AwayTeamCode, StringComparison.OrdinalIgnoreCase) == true &&
                        s.HomeTeam?.Equals(game.HomeTeamCode, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        liveGame = s;
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    game.AwayScore = liveGame.AwayScore;
                    game.HomeScore = liveGame.HomeScore;
                    game.Status = DetermineGameStatus(liveGame.IsLive, liveGame.IsFinal);

                    _logger.LogInformation("✅ Merged score: {Away} {AwayScore} @ {Home} {HomeScore} - {Status}",
                        game.AwayTeamCode, game.AwayScore ?? 0, game.HomeTeamCode, game.HomeScore ?? 0, game.Status);
                }
                else
                {
                    _logger.LogDebug("No live score found for {Away} @ {Home}", game.AwayTeamCode, game.HomeTeamCode);
                }
            }
        }

        private static string DetermineGameStatus(bool isLive, bool isFinal)
        {
            if (isFinal) return "Final";
            if (isLive)  return "Live";
            return "Scheduled";
        }

        private Dictionary<string, (string Name, string Logo, string Code)> InitializeTeamInfo()
        {
            return new Dictionary<string, (string Name, string Logo, string Code)>
            {
                { "ATL", ("Atlanta Hawks",           "https://a.espncdn.com/i/teamlogos/nba/500/atl.png",  "ATL") },
                { "BOS", ("Boston Celtics",           "https://a.espncdn.com/i/teamlogos/nba/500/bos.png",  "BOS") },
                { "BKN", ("Brooklyn Nets",            "https://a.espncdn.com/i/teamlogos/nba/500/bkn.png",  "BKN") },
                { "CHA", ("Charlotte Hornets",        "https://a.espncdn.com/i/teamlogos/nba/500/cha.png",  "CHA") },
                { "CHI", ("Chicago Bulls",            "https://a.espncdn.com/i/teamlogos/nba/500/chi.png",  "CHI") },
                { "CLE", ("Cleveland Cavaliers",      "https://a.espncdn.com/i/teamlogos/nba/500/cle.png",  "CLE") },
                { "DAL", ("Dallas Mavericks",         "https://a.espncdn.com/i/teamlogos/nba/500/dal.png",  "DAL") },
                { "DEN", ("Denver Nuggets",           "https://a.espncdn.com/i/teamlogos/nba/500/den.png",  "DEN") },
                { "DET", ("Detroit Pistons",          "https://a.espncdn.com/i/teamlogos/nba/500/det.png",  "DET") },
                { "GSW", ("Golden State Warriors",    "https://a.espncdn.com/i/teamlogos/nba/500/gs.png",   "GSW") },
                { "HOU", ("Houston Rockets",          "https://a.espncdn.com/i/teamlogos/nba/500/hou.png",  "HOU") },
                { "IND", ("Indiana Pacers",           "https://a.espncdn.com/i/teamlogos/nba/500/ind.png",  "IND") },
                { "LAC", ("LA Clippers",              "https://a.espncdn.com/i/teamlogos/nba/500/lac.png",  "LAC") },
                { "LAL", ("Los Angeles Lakers",       "https://a.espncdn.com/i/teamlogos/nba/500/lal.png",  "LAL") },
                { "MEM", ("Memphis Grizzlies",        "https://a.espncdn.com/i/teamlogos/nba/500/mem.png",  "MEM") },
                { "MIA", ("Miami Heat",               "https://a.espncdn.com/i/teamlogos/nba/500/mia.png",  "MIA") },
                { "MIL", ("Milwaukee Bucks",          "https://a.espncdn.com/i/teamlogos/nba/500/mil.png",  "MIL") }
,                { "MIN", ("Minnesota Timberwolves",   "https://a.espncdn.com/i/teamlogos/nba/500/min.png",  "MIN") },
                { "NOP", ("New Orleans Pelicans",     "https://a.espncdn.com/i/teamlogos/nba/500/no.png",   "NOP") },
                { "NYK", ("New York Knicks",          "https://a.espncdn.com/i/teamlogos/nba/500/ny.png",   "NYK") },
                { "OKC", ("Oklahoma City Thunder",    "https://a.espncdn.com/i/teamlogos/nba/500/okc.png",  "OKC") },
                { "ORL", ("Orlando Magic",            "https://a.espncdn.com/i/teamlogos/nba/500/orl.png",  "ORL") },
                { "PHI", ("Philadelphia 76ers",       "https://a.espncdn.com/i/teamlogos/nba/500/phi.png",  "PHI") },
                { "PHX", ("Phoenix Suns",             "https://a.espncdn.com/i/teamlogos/nba/500/phx.png",  "PHX") },
                { "POR", ("Portland Trail Blazers",   "https://a.espncdn.com/i/teamlogos/nba/500/por.png",  "POR") },
                { "SAC", ("Sacramento Kings",         "https://a.espncdn.com/i/teamlogos/nba/500/sac.png",  "SAC") },
                { "SAS", ("San Antonio Spurs",        "https://a.espncdn.com/i/teamlogos/nba/500/sa.png",   "SAS") },
                { "TOR", ("Toronto Raptors",          "https://a.espncdn.com/i/teamlogos/nba/500/tor.png",  "TOR") },
                { "UTA", ("Utah Jazz",                "https://a.espncdn.com/i/teamlogos/nba/500/utah.png", "UTA") },
                { "WAS", ("Washington Wizards",       "https://a.espncdn.com/i/teamlogos/nba/500/wsh.png",  "WAS") }
            };
        }
    }
}



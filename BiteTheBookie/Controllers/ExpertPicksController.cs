using System.Text;
using System.Text.Json;
using BiteTheBookie.Data;
using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.Services.Implementations;
using BiteTheBookie.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Controllers
{
    [Authorize(Roles = "Admin")]
    public class ExpertPicksController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ExpertPicksController> _logger;
        private readonly INBAGamesService _nbaGamesService;
        private readonly IMLBGamesService _mlbGamesService;
        private readonly TheOddsApiClient _oddsApiClient;

        public ExpertPicksController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILogger<ExpertPicksController> logger,
            INBAGamesService nbaGamesService,
            IMLBGamesService mlbGamesService,
            TheOddsApiClient oddsApiClient)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
            _nbaGamesService = nbaGamesService;
            _mlbGamesService = mlbGamesService;
            _oddsApiClient = oddsApiClient;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index(string league = "NBA")
        {
            var picks = await _db.ExpertPicks
                .Where(p => p.League == league)
                .OrderByDescending(p => p.GameTime)
                .ThenByDescending(p => p.CreatedAt)
                .Select(p => new ExpertPickSummary
                {
                    Id = p.Id,
                    GameId = p.GameId,
                    League = p.League,
                    AwayTeamName = p.AwayTeamName,
                    HomeTeamName = p.HomeTeamName,
                    GameTime = p.GameTime,
                    PickType = p.PickType,
                    PickSelection = p.PickSelection,
                    Confidence = p.Confidence,
                    Analysis = p.Analysis,
                    EnteredBy = p.EnteredBy,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            var vm = new AdminPicksListViewModel
            {
                SelectedLeague = league,
                Picks = picks
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> Create(string league = "NBA", string? gameId = null,
            string? awayTeam = null, string? homeTeam = null, string? gameTime = null)
        {
            var vm = new AdminPickViewModel
            {
                League = league,
                GameId = gameId ?? string.Empty,
                AwayTeamName = awayTeam ?? string.Empty,
                HomeTeamName = homeTeam ?? string.Empty,
                GameTime = DateTime.TryParse(gameTime, out var gt) ? gt : DateTime.UtcNow
            };

            await PopulateAvailableGamesAsync(vm);

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AdminPickViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                await PopulateAvailableGamesAsync(vm);
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);

            var pick = new ExpertPick
            {
                GameId = vm.GameId,
                League = vm.League,
                AwayTeamName = vm.AwayTeamName,
                HomeTeamName = vm.HomeTeamName,
                GameTime = vm.GameTime,
                PickType = vm.PickType,
                PickSelection = vm.PickSelection,
                Confidence = vm.Confidence,
                Analysis = vm.Analysis,
                EnteredBy = user?.FirstName ?? User.Identity?.Name ?? "Admin",
                CreatedAt = DateTime.UtcNow
            };

            _db.ExpertPicks.Add(pick);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Admin {User} created pick for {GameId} ({League})",
                pick.EnteredBy, pick.GameId, pick.League);

            TempData["SuccessMessage"] = $"Pick saved for {vm.AwayTeamName} @ {vm.HomeTeamName}";
            return RedirectToAction("Index", new { league = vm.League });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var pick = await _db.ExpertPicks.FindAsync(id);
            if (pick == null) return NotFound();

            var vm = new AdminPickViewModel
            {
                Id = pick.Id,
                GameId = pick.GameId,
                League = pick.League,
                AwayTeamName = pick.AwayTeamName,
                HomeTeamName = pick.HomeTeamName,
                GameTime = pick.GameTime,
                PickType = pick.PickType,
                PickSelection = pick.PickSelection,
                Confidence = pick.Confidence,
                Analysis = pick.Analysis
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AdminPickViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                return View(vm);
            }

            var pick = await _db.ExpertPicks.FindAsync(vm.Id);
            if (pick == null) return NotFound();

            pick.PickType = vm.PickType;
            pick.PickSelection = vm.PickSelection;
            pick.Confidence = vm.Confidence;
            pick.Analysis = vm.Analysis;
            pick.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Pick updated successfully";
            return RedirectToAction("Index", new { league = vm.League });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var pick = await _db.ExpertPicks.FindAsync(id);
            if (pick == null) return NotFound();

            var league = pick.League;
            _db.ExpertPicks.Remove(pick);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "Pick deleted";
            return RedirectToAction("Index", new { league });
        }

        /// <summary>
        /// API endpoint that returns game details by league and Game ID for auto-filling the form.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetGameDetails(string league, string gameId)
        {
            if (league == "NBA")
            {
                var nbaGames = await _nbaGamesService.GetUpcomingNBAGamesAsync();
                var nbaGame = nbaGames.FirstOrDefault(g => g.GameId == gameId);
                if (nbaGame == null) return NotFound();

                return Json(new
                {
                    awayTeamName = nbaGame.AwayTeamName,
                    homeTeamName = nbaGame.HomeTeamName,
                    gameTime = nbaGame.GameTime.ToString("yyyy-MM-ddTHH:mm")
                });
            }

            if (league == "MLB")
            {
                var mlbGames = await _mlbGamesService.GetTodayGamesAsync();
                var mlbGame = mlbGames.FirstOrDefault(g => BuildMlbGameId(g) == gameId);
                if (mlbGame == null) return NotFound();

                return Json(new
                {
                    awayTeamName = mlbGame.AwayTeam,
                    homeTeamName = mlbGame.HomeTeam,
                    gameTime = mlbGame.GameTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm")
                });
            }

            return NotFound();
        }

        /// <summary>
        /// API endpoint that generates a pregame summary for an MLB game.
        /// Fetches probable pitchers, team records, and betting odds.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMlbPregameSummary(string gameId)
        {
            try
            {
                var mlbGames = await _mlbGamesService.GetTodayGamesAsync();
                var game = mlbGames.FirstOrDefault(g => BuildMlbGameId(g) == gameId);
                if (game == null) return NotFound();

                var sb = new StringBuilder();
                sb.AppendLine($"🏟️ PREGAME SUMMARY: {game.AwayTeam} @ {game.HomeTeam}");
                sb.AppendLine($"📅 {game.GameTime:dddd, MMMM dd, yyyy — h:mm tt}");
                sb.AppendLine();

                // Fetch probable pitchers and team records from MLB Stats API
                await AppendMlbStatsAsync(sb, game);

                // Fetch betting odds from The Odds API
                await AppendMlbOddsAsync(sb, game);

                sb.AppendLine("---");
                sb.AppendLine("✏️ Add your expert analysis below...");

                return Json(new { summary = sb.ToString() });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate MLB pregame summary for {GameId}", gameId);
                return Json(new { summary = $"Could not generate pregame summary. Please write your analysis manually." });
            }
        }

        private async Task AppendMlbStatsAsync(StringBuilder sb, Models.MLB.Game game)
        {
            try
            {
                using var http = new HttpClient { BaseAddress = new Uri("https://statsapi.mlb.com/api/v1/") };

                // Fetch today's schedule with probable pitchers and team records hydrated
                var date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
                var scheduleUrl = $"schedule?sportId=1&date={date}&hydrate=probablePitcher(note),team(record)";
                var schedule = await http.GetFromJsonAsync<JsonElement>(scheduleUrl);

                if (schedule.TryGetProperty("dates", out var dates) && dates.GetArrayLength() > 0)
                {
                    foreach (var dateEntry in dates.EnumerateArray())
                    {
                        if (!dateEntry.TryGetProperty("games", out var games)) continue;

                        foreach (var g in games.EnumerateArray())
                        {
                            var homeId = g.GetProperty("teams").GetProperty("home").GetProperty("team").GetProperty("id").GetInt32();
                            var awayId = g.GetProperty("teams").GetProperty("away").GetProperty("team").GetProperty("id").GetInt32();

                            if (homeId != game.HomeTeamId || awayId != game.AwayTeamId) continue;

                            // Team Records
                            var awayRecord = GetTeamRecord(g, "away");
                            var homeRecord = GetTeamRecord(g, "home");

                            if (!string.IsNullOrEmpty(awayRecord) || !string.IsNullOrEmpty(homeRecord))
                            {
                                sb.AppendLine("📊 TEAM RECORDS:");
                                if (!string.IsNullOrEmpty(awayRecord))
                                    sb.AppendLine($"  {game.AwayTeam}: {awayRecord}");
                                if (!string.IsNullOrEmpty(homeRecord))
                                    sb.AppendLine($"  {game.HomeTeam}: {homeRecord}");
                                sb.AppendLine();
                            }

                            // Probable Pitchers
                            var awayPitcher = GetProbablePitcher(g, "away");
                            var homePitcher = GetProbablePitcher(g, "home");

                            if (!string.IsNullOrEmpty(awayPitcher) || !string.IsNullOrEmpty(homePitcher))
                            {
                                sb.AppendLine("⚾ PROBABLE PITCHERS:");
                                sb.AppendLine($"  {game.AwayTeam}: {awayPitcher ?? "TBD"}");
                                sb.AppendLine($"  {game.HomeTeam}: {homePitcher ?? "TBD"}");
                                sb.AppendLine();
                            }
                            else
                            {
                                sb.AppendLine("⚾ PROBABLE PITCHERS: TBD vs TBD");
                                sb.AppendLine();
                            }

                            return; // Found the matching game
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch MLB stats for pregame summary");
                sb.AppendLine("📊 Team stats unavailable.");
                sb.AppendLine();
            }
        }

        private static string? GetTeamRecord(JsonElement game, string side)
        {
            try
            {
                var team = game.GetProperty("teams").GetProperty(side);
                if (team.TryGetProperty("leagueRecord", out var record))
                {
                    var wins = record.GetProperty("wins").GetInt32();
                    var losses = record.GetProperty("losses").GetInt32();
                    var pct = record.GetProperty("pct").GetString();
                    return $"{wins}-{losses} ({pct})";
                }
            }
            catch { }
            return null;
        }

        private static string? GetProbablePitcher(JsonElement game, string side)
        {
            try
            {
                var team = game.GetProperty("teams").GetProperty(side);
                if (team.TryGetProperty("probablePitcher", out var pitcher))
                {
                    var name = pitcher.GetProperty("fullName").GetString() ?? "TBD";

                    // Try to get the pitcher's note (contains record/ERA if hydrated)
                    if (pitcher.TryGetProperty("note", out var note))
                    {
                        var noteStr = note.GetString();
                        if (!string.IsNullOrEmpty(noteStr))
                            return $"{name} ({noteStr})";
                    }

                    return name;
                }
            }
            catch { }
            return null;
        }

        private async Task AppendMlbOddsAsync(StringBuilder sb, Models.MLB.Game game)
        {
            try
            {
                var oddsData = await _oddsApiClient.GetAsync(
                    "/v4/sports/baseball_mlb/odds?regions=us&markets=spreads,totals,h2h&oddsFormat=american");

                if (oddsData.ValueKind != JsonValueKind.Array) return;

                foreach (var oddsGame in oddsData.EnumerateArray())
                {
                    var homeTeam = oddsGame.GetProperty("home_team").GetString() ?? "";
                    var awayTeam = oddsGame.GetProperty("away_team").GetString() ?? "";

                    // Match by team name (MLB Stats API names match The Odds API names)
                    if (!homeTeam.Equals(game.HomeTeam, StringComparison.OrdinalIgnoreCase) ||
                        !awayTeam.Equals(game.AwayTeam, StringComparison.OrdinalIgnoreCase))
                        continue;

                    decimal? spread = null;
                    decimal? overUnder = null;
                    int? homeML = null;
                    int? awayML = null;

                    if (oddsGame.TryGetProperty("bookmakers", out var bookmakers) &&
                        bookmakers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var bk in bookmakers.EnumerateArray())
                        {
                            if (!bk.TryGetProperty("markets", out var markets) ||
                                markets.ValueKind != JsonValueKind.Array) continue;

                            foreach (var market in markets.EnumerateArray())
                            {
                                var key = market.GetProperty("key").GetString();
                                var outcomes = market.GetProperty("outcomes").EnumerateArray().ToList();

                                if (key == "spreads" && !spread.HasValue)
                                {
                                    var homeOutcome = outcomes.FirstOrDefault(
                                        o => o.GetProperty("name").GetString() == homeTeam);
                                    if (homeOutcome.ValueKind != JsonValueKind.Undefined)
                                        spread = homeOutcome.GetProperty("point").GetDecimal();
                                }
                                else if (key == "totals" && !overUnder.HasValue)
                                {
                                    if (outcomes.Any())
                                        overUnder = outcomes.First().GetProperty("point").GetDecimal();
                                }
                                else if (key == "h2h")
                                {
                                    var ho = outcomes.FirstOrDefault(
                                        o => o.GetProperty("name").GetString() == homeTeam);
                                    var ao = outcomes.FirstOrDefault(
                                        o => o.GetProperty("name").GetString() == awayTeam);
                                    if (ho.ValueKind != JsonValueKind.Undefined && !homeML.HasValue)
                                        homeML = ho.GetProperty("price").GetInt32();
                                    if (ao.ValueKind != JsonValueKind.Undefined && !awayML.HasValue)
                                        awayML = ao.GetProperty("price").GetInt32();
                                }
                            }

                            if (spread.HasValue && overUnder.HasValue && homeML.HasValue) break;
                        }
                    }

                    sb.AppendLine("💰 BETTING LINES:");
                    if (homeML.HasValue && awayML.HasValue)
                        sb.AppendLine($"  Moneyline: {game.AwayTeam} ({FormatML(awayML.Value)}) / {game.HomeTeam} ({FormatML(homeML.Value)})");
                    if (spread.HasValue)
                        sb.AppendLine($"  Run Line: {game.HomeTeam} {(spread.Value > 0 ? "+" : "")}{spread.Value}");
                    if (overUnder.HasValue)
                        sb.AppendLine($"  Over/Under: {overUnder.Value}");
                    sb.AppendLine();
                    return;
                }

                sb.AppendLine("💰 BETTING LINES: Not yet available.");
                sb.AppendLine();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch MLB odds for pregame summary");
                sb.AppendLine("💰 BETTING LINES: Unavailable.");
                sb.AppendLine();
            }
        }

        private static string FormatML(int ml) => ml > 0 ? $"+{ml}" : ml.ToString();

        private async Task PopulateAvailableGamesAsync(AdminPickViewModel vm)
        {
            if (vm.League == "NBA")
            {
                vm.AvailableNbaGames = await GetNbaGameSelectListAsync();
            }

            // Always load MLB games too so they're available when the user switches leagues
            vm.AvailableMlbGames = await GetMlbGameSelectListAsync();

            if (vm.League == "NBA")
            {
                // NBA was already loaded above
            }
            else
            {
                vm.AvailableNbaGames = await GetNbaGameSelectListAsync();
            }
        }

        private async Task<List<ViewModels.NbaGameOption>> GetNbaGameSelectListAsync()
        {
            try
            {
                var games = await _nbaGamesService.GetUpcomingNBAGamesAsync();

                return games
                    .Where(g => g.Status == "Scheduled")
                    .OrderBy(g => g.GameTime)
                    .Select(g => new ViewModels.NbaGameOption
                    {
                        Value = g.GameId,
                        Text = $"{g.AwayTeamName} @ {g.HomeTeamName} — {g.GameTime.ToLocalTime():MMM dd, h:mm tt}",
                        AwayCode = GetNbaTeamCode(g.AwayTeamName ?? ""),
                        HomeCode = GetNbaTeamCode(g.HomeTeamName ?? ""),
                        AwayName = g.AwayTeamName ?? string.Empty,
                        HomeName = g.HomeTeamName ?? string.Empty,
                        GameTime = g.GameTime
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load NBA games for dropdown");
                return new List<ViewModels.NbaGameOption>();
            }
        }

        private async Task<List<ViewModels.MlbGameOption>> GetMlbGameSelectListAsync()
        {
            try
            {
                var games = await _mlbGamesService.GetTodayGamesAsync();

                return games
                    .OrderBy(g => g.GameTime)
                    .Select(g => new ViewModels.MlbGameOption
                    {
                        Value = BuildMlbGameId(g),
                        Text = $"{g.AwayTeam} @ {g.HomeTeam} — {g.GameTime:MMM dd, h:mm tt}",
                        AwayCode = GetMlbTeamCode(g.AwayTeam ?? ""),
                        HomeCode = GetMlbTeamCode(g.HomeTeam ?? ""),
                        AwayName = g.AwayTeam ?? string.Empty,
                        HomeName = g.HomeTeam ?? string.Empty,
                        GameTime = g.GameTime
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load MLB games for dropdown");
                return new List<ViewModels.MlbGameOption>();
            }
        }

        /// <summary>
        /// Builds a consistent Game ID for MLB games using team names and date.
        /// Format: awaycode-homecode-YYYYMMDD (e.g. nyy-bos-20260615)
        /// </summary>
        private static string BuildMlbGameId(Models.MLB.Game game)
        {
            var awayCode = GetMlbTeamCode(game.AwayTeam ?? "");
            var homeCode = GetMlbTeamCode(game.HomeTeam ?? "");
            var dateStr = game.GameTime.ToUniversalTime().ToString("yyyyMMdd");
            return $"{awayCode}-{homeCode}-{dateStr}";
        }

        /// <summary>
        /// Maps a full MLB team name to a short code for the Game ID.
        /// </summary>
        private static string GetMlbTeamCode(string teamName)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Arizona Diamondbacks", "ari" },
                { "Atlanta Braves", "atl" },
                { "Baltimore Orioles", "bal" },
                { "Boston Red Sox", "bos" },
                { "Chicago Cubs", "chc" },
                { "Chicago White Sox", "cws" },
                { "Cincinnati Reds", "cin" },
                { "Cleveland Guardians", "cle" },
                { "Colorado Rockies", "col" },
                { "Detroit Tigers", "det" },
                { "Houston Astros", "hou" },
                { "Kansas City Royals", "kc" },
                { "Los Angeles Angels", "laa" },
                { "Los Angeles Dodgers", "lad" },
                { "Miami Marlins", "mia" },
                { "Milwaukee Brewers", "mil" },
                { "Minnesota Twins", "min" },
                { "New York Mets", "nym" },
                { "New York Yankees", "nyy" },
                { "Oakland Athletics", "oak" },
                { "Philadelphia Phillies", "phi" },
                { "Pittsburgh Pirates", "pit" },
                { "San Diego Padres", "sd" },
                { "San Francisco Giants", "sf" },
                { "Seattle Mariners", "sea" },
                { "St. Louis Cardinals", "stl" },
                { "Tampa Bay Rays", "tb" },
                { "Texas Rangers", "tex" },
                { "Toronto Blue Jays", "tor" },
                { "Washington Nationals", "wsh" }
            };

            return mapping.GetValueOrDefault(teamName, teamName.Replace(" ", "").ToLower()[..Math.Min(3, teamName.Length)]);
        }

        /// <summary>
        /// Maps a full NBA team name to a short code used for ESPN CDN logos.
        /// </summary>
        private static string GetNbaTeamCode(string teamName)
        {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Atlanta Hawks", "atl" },
                { "Boston Celtics", "bos" },
                { "Brooklyn Nets", "bkn" },
                { "Charlotte Hornets", "cha" },
                { "Chicago Bulls", "chi" },
                { "Cleveland Cavaliers", "cle" },
                { "Dallas Mavericks", "dal" },
                { "Denver Nuggets", "den" },
                { "Detroit Pistons", "det" },
                { "Golden State Warriors", "gsw" },
                { "Houston Rockets", "hou" },
                { "Indiana Pacers", "ind" },
                { "LA Clippers", "lac" },
                { "Los Angeles Clippers", "lac" },
                { "Los Angeles Lakers", "lal" },
                { "Memphis Grizzlies", "mem" },
                { "Miami Heat", "mia" },
                { "Milwaukee Bucks", "mil" },
                { "Minnesota Timberwolves", "min" },
                { "New Orleans Pelicans", "nop" },
                { "New York Knicks", "nyk" },
                { "Oklahoma City Thunder", "okc" },
                { "Orlando Magic", "orl" },
                { "Philadelphia 76ers", "phi" },
                { "Phoenix Suns", "phx" },
                { "Portland Trail Blazers", "por" },
                { "Sacramento Kings", "sac" },
                { "San Antonio Spurs", "sas" },
                { "Toronto Raptors", "tor" },
                { "Utah Jazz", "uta" },
                { "Washington Wizards", "was" }
            };

            // Fallback: use first 3 non-space characters lowercased
            if (mapping.TryGetValue(teamName, out var code)) return code;
            var cleaned = teamName.Replace(" ", "").ToLower();
            return cleaned.Length >= 3 ? cleaned[..3] : cleaned;
        }
    }
}
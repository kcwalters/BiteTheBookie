using BiteTheBookie.Helpers;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using System.Text.Json;

namespace BiteTheBookie.Services.Implementations;

public class OddsService : IOddsService
{
    private readonly HttpClient _http;
    private readonly TheOddsApiClient? _oddsApiClient;

    public OddsService(HttpClient http, TheOddsApiClient? oddsApiClient = null)
    {
        _http = http;
        _oddsApiClient = oddsApiClient;
    }

    public Task<IEnumerable<HeroOddViewModel>> GetHeroOddsAsync()
        => Task.FromResult(Enumerable.Empty<HeroOddViewModel>());

    public Task<IEnumerable<LiveOddsViewModel>> GetLiveOddsAsync()
        => Task.FromResult(Enumerable.Empty<LiveOddsViewModel>());

    public Task<LeagueOddsViewModel> GetLeagueOddsAsync()
        => Task.FromResult(new LeagueOddsViewModel());

    public async Task<IEnumerable<NFLOddsViewModel>> GetNFLOddsAsync(CancellationToken cancellationToken = default)
    {
        if (_oddsApiClient == null)
        {
            return Enumerable.Empty<NFLOddsViewModel>();
        }

        try
        {
            var response = await _oddsApiClient.GetAsync(
                "sports/americanfootball_nfl/odds/?regions=us&markets=h2h,spreads,totals&oddsFormat=american",
                cancellationToken);

            var odds = new List<NFLOddsViewModel>();

            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var game in response.EnumerateArray())
                {
                    var gameOdds = ParseNFLGame(game);
                    if (gameOdds != null)
                    {
                        odds.Add(gameOdds);
                    }
                }
            }

            return odds;
        }
        catch
        {
            return Enumerable.Empty<NFLOddsViewModel>();
        }
    }

    private NFLOddsViewModel? ParseNFLGame(JsonElement game)
    {
        try
        {
            var gameId = game.GetProperty("id").GetString() ?? "";
            var awayTeam = game.GetProperty("away_team").GetString() ?? "";
            var homeTeam = game.GetProperty("home_team").GetString() ?? "";
            var commenceTime = game.GetProperty("commence_time").GetDateTime();

            var oddsViewModel = new NFLOddsViewModel
            {
                GameId = gameId,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                CommenceTime = commenceTime,
                VenueTimeZoneId = VenueTimeZoneHelper.GetTimeZoneId("NFL", homeTeam)
            };

            if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
            {
                var draftkings = bookmakers.EnumerateArray()
                    .FirstOrDefault(b => b.GetProperty("key").GetString() == "draftkings");

                if (draftkings.ValueKind != JsonValueKind.Undefined)
                {
                    if (draftkings.TryGetProperty("markets", out var markets))
                    {
                        foreach (var market in markets.EnumerateArray())
                        {
                            var marketKey = market.GetProperty("key").GetString();
                            var outcomes = market.GetProperty("outcomes");

                            if (marketKey == "h2h")
                            {
                                ParseMoneyline(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "spreads")
                            {
                                ParseSpreads(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "totals")
                            {
                                ParseTotals(outcomes, oddsViewModel);
                            }
                        }
                    }
                }
            }

            return oddsViewModel;
        }
        catch
        {
            return null;
        }
    }

    private void ParseMoneyline(JsonElement outcomes, string awayTeam, string homeTeam, NFLOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var price = outcome.GetProperty("price").GetInt32();

            if (team == awayTeam)
            {
                model.AwayMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
            else if (team == homeTeam)
            {
                model.HomeMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
        }
    }

    private void ParseSpreads(JsonElement outcomes, string awayTeam, string homeTeam, NFLOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();
            var pointStr = point > 0 ? $"+{point:0.0}" : point.ToString("0.0");

            if (team == awayTeam)
            {
                model.AwaySpread = pointStr;
                model.AwaySpreadPrice = priceStr;
            }
            else if (team == homeTeam)
            {
                model.HomeSpread = pointStr;
                model.HomeSpreadPrice = priceStr;
            }
        }
    }

    private void ParseTotals(JsonElement outcomes, NFLOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var name = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();

            if (name == "Over")
            {
                model.OverPoint = point.ToString("0.0");
                model.OverPrice = priceStr;
            }
            else if (name == "Under")
            {
                model.UnderPoint = point.ToString("0.0");
                model.UnderPrice = priceStr;
            }
        }
    }

    public async Task<IEnumerable<NBAOddsViewModel>> GetNBAOddsAsync(CancellationToken cancellationToken = default)
    {
        if (_oddsApiClient == null)
        {
            return Enumerable.Empty<NBAOddsViewModel>();
        }

        try
        {
            var response = await _oddsApiClient.GetAsync(
                "sports/basketball_nba/odds/?regions=us&markets=h2h,spreads,totals&oddsFormat=american",
                cancellationToken);

            var odds = new List<NBAOddsViewModel>();

            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var game in response.EnumerateArray())
                {
                    var gameOdds = ParseNBAGame(game);
                    if (gameOdds != null)
                    {
                        odds.Add(gameOdds);
                    }
                }
            }

            return odds;
        }
        catch (Exception ex)
        {
            // Log the exception to help debug
            Console.WriteLine($"NBA Odds Error: {ex.Message}");
            return Enumerable.Empty<NBAOddsViewModel>();
        }
    }

    private NBAOddsViewModel? ParseNBAGame(JsonElement game)
    {
        try
        {
            var gameId = game.GetProperty("id").GetString() ?? "";
            var awayTeam = game.GetProperty("away_team").GetString() ?? "";
            var homeTeam = game.GetProperty("home_team").GetString() ?? "";
            var commenceTime = game.GetProperty("commence_time").GetDateTime();

            var oddsViewModel = new NBAOddsViewModel
            {
                GameId = gameId,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                CommenceTime = commenceTime,
                VenueTimeZoneId = VenueTimeZoneHelper.GetTimeZoneId("NBA", homeTeam)
            };

            if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
            {
                var draftkings = bookmakers.EnumerateArray()
                    .FirstOrDefault(b => b.GetProperty("key").GetString() == "draftkings");

                if (draftkings.ValueKind != JsonValueKind.Undefined)
                {
                    if (draftkings.TryGetProperty("markets", out var markets))
                    {
                        foreach (var market in markets.EnumerateArray())
                        {
                            var marketKey = market.GetProperty("key").GetString();
                            var outcomes = market.GetProperty("outcomes");

                            if (marketKey == "h2h")
                            {
                                ParseNBAMoneyline(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "spreads")
                            {
                                ParseNBASpreads(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "totals")
                            {
                                ParseNBATotals(outcomes, oddsViewModel);
                            }
                        }
                    }
                }
            }

            return oddsViewModel;
        }
        catch
        {
            return null;
        }
    }

    private void ParseNBAMoneyline(JsonElement outcomes, string awayTeam, string homeTeam, NBAOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var price = outcome.GetProperty("price").GetInt32();

            if (team == awayTeam)
            {
                model.AwayMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
            else if (team == homeTeam)
            {
                model.HomeMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
        }
    }

    private void ParseNBASpreads(JsonElement outcomes, string awayTeam, string homeTeam, NBAOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();
            var pointStr = point > 0 ? $"+{point:0.0}" : point.ToString("0.0");

            if (team == awayTeam)
            {
                model.AwaySpread = pointStr;
                model.AwaySpreadPrice = priceStr;
            }
            else if (team == homeTeam)
            {
                model.HomeSpread = pointStr;
                model.HomeSpreadPrice = priceStr;
            }
        }
    }

    private void ParseNBATotals(JsonElement outcomes, NBAOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var name = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();

            if (name == "Over")
            {
                model.OverPoint = point.ToString("0.0");
                model.OverPrice = priceStr;
            }
            else if (name == "Under")
            {
                model.UnderPoint = point.ToString("0.0");
                model.UnderPrice = priceStr;
            }
        }
    }

    public async Task<IEnumerable<CBBOddsViewModel>> GetCBBOddsAsync(CancellationToken cancellationToken = default)
    {
        if (_oddsApiClient == null)
        {
            return Enumerable.Empty<CBBOddsViewModel>();
        }

        try
        {
            var response = await _oddsApiClient.GetAsync(
                "sports/basketball_ncaab/odds/?regions=us&markets=h2h,spreads,totals&oddsFormat=american",
                cancellationToken);

            var odds = new List<CBBOddsViewModel>();

            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var game in response.EnumerateArray())
                {
                    var gameOdds = ParseCBBGame(game);
                    if (gameOdds != null)
                    {
                        odds.Add(gameOdds);
                    }
                }
            }

            return odds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CBB Odds Error: {ex.Message}");
            return Enumerable.Empty<CBBOddsViewModel>();
        }
    }

    private CBBOddsViewModel? ParseCBBGame(JsonElement game)
    {
        try
        {
            var gameId = game.GetProperty("id").GetString() ?? "";
            var awayTeam = game.GetProperty("away_team").GetString() ?? "";
            var homeTeam = game.GetProperty("home_team").GetString() ?? "";
            var commenceTime = game.GetProperty("commence_time").GetDateTime();

            var oddsViewModel = new CBBOddsViewModel
            {
                GameId = gameId,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                CommenceTime = commenceTime,
                VenueTimeZoneId = VenueTimeZoneHelper.GetTimeZoneId("CBB", homeTeam) // defaults to Eastern for unknown schools
            };

            if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
            {
                var draftkings = bookmakers.EnumerateArray()
                    .FirstOrDefault(b => b.GetProperty("key").GetString() == "draftkings");

                if (draftkings.ValueKind != JsonValueKind.Undefined)
                {
                    if (draftkings.TryGetProperty("markets", out var markets))
                    {
                        foreach (var market in markets.EnumerateArray())
                        {
                            var marketKey = market.GetProperty("key").GetString();
                            var outcomes = market.GetProperty("outcomes");

                            if (marketKey == "h2h")
                            {
                                ParseCBBMoneyline(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "spreads")
                            {
                                ParseCBBSpreads(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "totals")
                            {
                                ParseCBBTotals(outcomes, oddsViewModel);
                            }
                        }
                    }
                }
            }

            return oddsViewModel;
        }
        catch
        {
            return null;
        }
    }

    private void ParseCBBMoneyline(JsonElement outcomes, string awayTeam, string homeTeam, CBBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var price = outcome.GetProperty("price").GetInt32();

            if (team == awayTeam)
            {
                model.AwayMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
            else if (team == homeTeam)
            {
                model.HomeMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
        }
    }

    private void ParseCBBSpreads(JsonElement outcomes, string awayTeam, string homeTeam, CBBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();
            var pointStr = point > 0 ? $"+{point:0.0}" : point.ToString("0.0");

            if (team == awayTeam)
            {
                model.AwaySpread = pointStr;
                model.AwaySpreadPrice = priceStr;
            }
            else if (team == homeTeam)
            {
                model.HomeSpread = pointStr;
                model.HomeSpreadPrice = priceStr;
            }
        }
    }

    private void ParseCBBTotals(JsonElement outcomes, CBBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var name = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();

            if (name == "Over")
            {
                model.OverPoint = point.ToString("0.0");
                model.OverPrice = priceStr;
            }
            else if (name == "Under")
            {
                model.UnderPoint = point.ToString("0.0");
                model.UnderPrice = priceStr;
            }
        }
    }

    public async Task<IEnumerable<CFBOddsViewModel>> GetCFBOddsAsync(CancellationToken cancellationToken = default)
    {
        if (_oddsApiClient == null)
        {
            return Enumerable.Empty<CFBOddsViewModel>();
        }

        try
        {
            var response = await _oddsApiClient.GetAsync(
                "sports/americanfootball_ncaaf/odds/?regions=us&markets=h2h,spreads,totals&oddsFormat=american",
                cancellationToken);

            var odds = new List<CFBOddsViewModel>();

            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var game in response.EnumerateArray())
                {
                    var gameOdds = ParseCFBGame(game);
                    if (gameOdds != null)
                    {
                        odds.Add(gameOdds);
                    }
                }
            }

            return odds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CFB Odds Error: {ex.Message}");
            return Enumerable.Empty<CFBOddsViewModel>();
        }
    }

    private CFBOddsViewModel? ParseCFBGame(JsonElement game)
    {
        try
        {
            var gameId = game.GetProperty("id").GetString() ?? "";
            var awayTeam = game.GetProperty("away_team").GetString() ?? "";
            var homeTeam = game.GetProperty("home_team").GetString() ?? "";
            var commenceTime = game.GetProperty("commence_time").GetDateTime();

            var oddsViewModel = new CFBOddsViewModel
            {
                GameId = gameId,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                CommenceTime = commenceTime,
                VenueTimeZoneId = VenueTimeZoneHelper.GetTimeZoneId("CFB", homeTeam) // defaults to Eastern for unknown schools
            };

            if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
            {
                var draftkings = bookmakers.EnumerateArray()
                    .FirstOrDefault(b => b.GetProperty("key").GetString() == "draftkings");

                if (draftkings.ValueKind != JsonValueKind.Undefined)
                {
                    if (draftkings.TryGetProperty("markets", out var markets))
                    {
                        foreach (var market in markets.EnumerateArray())
                        {
                            var marketKey = market.GetProperty("key").GetString();
                            var outcomes = market.GetProperty("outcomes");

                            if (marketKey == "h2h")
                            {
                                ParseCFBMoneyline(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "spreads")
                            {
                                ParseCFBSpreads(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "totals")
                            {
                                ParseCFBTotals(outcomes, oddsViewModel);
                            }
                        }
                    }
                }
            }

            return oddsViewModel;
        }
        catch
        {
            return null;
        }
    }

    private void ParseCFBMoneyline(JsonElement outcomes, string awayTeam, string homeTeam, CFBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var price = outcome.GetProperty("price").GetInt32();

            if (team == awayTeam)
            {
                model.AwayMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
            else if (team == homeTeam)
            {
                model.HomeMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
        }
    }

    private void ParseCFBSpreads(JsonElement outcomes, string awayTeam, string homeTeam, CFBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();
            var pointStr = point > 0 ? $"+{point:0.0}" : point.ToString("0.0");

            if (team == awayTeam)
            {
                model.AwaySpread = pointStr;
                model.AwaySpreadPrice = priceStr;
            }
            else if (team == homeTeam)
            {
                model.HomeSpread = pointStr;
                model.HomeSpreadPrice = priceStr;
            }
        }
    }

    private void ParseCFBTotals(JsonElement outcomes, CFBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var name = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();

            if (name == "Over")
            {
                model.OverPoint = point.ToString("0.0");
                model.OverPrice = priceStr;
            }
            else if (name == "Under")
            {
                model.UnderPoint = point.ToString("0.0");
                model.UnderPrice = priceStr;
            }
        }
    }

    public async Task<IEnumerable<MLBOddsViewModel>> GetMLBOddsAsync(CancellationToken cancellationToken = default)
    {
        if (_oddsApiClient == null)
        {
            Console.WriteLine("MLB Odds: _oddsApiClient is null");
            return Enumerable.Empty<MLBOddsViewModel>();
        }

        try
        {
            var response = await _oddsApiClient.GetAsync(
                "sports/baseball_mlb/odds/?regions=us&markets=h2h,spreads,totals&oddsFormat=american",
                cancellationToken);

            Console.WriteLine($"MLB API Response Type: {response.ValueKind}");
            
            var odds = new List<MLBOddsViewModel>();

            if (response.ValueKind == JsonValueKind.Array)
            {
                var count = 0;
                foreach (var game in response.EnumerateArray())
                {
                    count++;
                    var gameOdds = ParseMLBGame(game);
                    if (gameOdds != null)
                    {
                        odds.Add(gameOdds);
                        Console.WriteLine($"MLB: Parsed game {count}: {gameOdds.AwayTeam} @ {gameOdds.HomeTeam}");
                    }
                    else
                    {
                        Console.WriteLine($"MLB: Failed to parse game {count}");
                    }
                }
                Console.WriteLine($"MLB: Total games in response: {count}, Parsed successfully: {odds.Count}");
            }
            else
            {
                Console.WriteLine($"MLB: Response is not an array. Raw response: {response.GetRawText()}");
            }

            return odds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MLB Odds Error: {ex.Message}");
            Console.WriteLine($"MLB Stack Trace: {ex.StackTrace}");
            return Enumerable.Empty<MLBOddsViewModel>();
        }
    }

    private static readonly Dictionary<string, string> _mlbTeamTimeZones = new(StringComparer.OrdinalIgnoreCase)
    {
        // Eastern
        { "Baltimore Orioles",      "Eastern Standard Time" },
        { "Boston Red Sox",         "Eastern Standard Time" },
        { "New York Yankees",       "Eastern Standard Time" },
        { "New York Mets",          "Eastern Standard Time" },
        { "Toronto Blue Jays",      "Eastern Standard Time" },
        { "Tampa Bay Rays",         "Eastern Standard Time" },
        { "Atlanta Braves",         "Eastern Standard Time" },
        { "Miami Marlins",          "Eastern Standard Time" },
        { "Philadelphia Phillies",  "Eastern Standard Time" },
        { "Washington Nationals",   "Eastern Standard Time" },
        { "Pittsburgh Pirates",     "Eastern Standard Time" },
        { "Cincinnati Reds",        "Eastern Standard Time" },
        { "Cleveland Guardians",    "Eastern Standard Time" },
        { "Detroit Tigers",         "Eastern Standard Time" },
        // Central
        { "Chicago White Sox",      "Central Standard Time" },
        { "Chicago Cubs",           "Central Standard Time" },
        { "Kansas City Royals",     "Central Standard Time" },
        { "Minnesota Twins",        "Central Standard Time" },
        { "Milwaukee Brewers",      "Central Standard Time" },
        { "St. Louis Cardinals",    "Central Standard Time" },
        { "Houston Astros",         "Central Standard Time" },
        { "Texas Rangers",          "Central Standard Time" },
        // Mountain
        { "Colorado Rockies",       "Mountain Standard Time" },
        { "Arizona Diamondbacks",   "US Mountain Standard Time" }, // AZ doesn't observe DST
        // Pacific
        { "Los Angeles Dodgers",    "Pacific Standard Time" },
        { "Los Angeles Angels",     "Pacific Standard Time" },
        { "San Francisco Giants",   "Pacific Standard Time" },
        { "Oakland Athletics",      "Pacific Standard Time" },
        { "Seattle Mariners",       "Pacific Standard Time" },
        { "San Diego Padres",       "Pacific Standard Time" },
    };

    private MLBOddsViewModel? ParseMLBGame(JsonElement game)
    {
        try
        {
            var gameId = game.GetProperty("id").GetString() ?? "";
            var awayTeam = game.GetProperty("away_team").GetString() ?? "";
            var homeTeam = game.GetProperty("home_team").GetString() ?? "";
            var commenceTime = game.GetProperty("commence_time").GetDateTime();

            var oddsViewModel = new MLBOddsViewModel
            {
                GameId = gameId,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                CommenceTime = commenceTime,
                VenueTimeZoneId = VenueTimeZoneHelper.GetTimeZoneId("MLB", homeTeam)
            };

            if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
            {
                var draftkings = bookmakers.EnumerateArray()
                    .FirstOrDefault(b => b.GetProperty("key").GetString() == "draftkings");

                if (draftkings.ValueKind != JsonValueKind.Undefined)
                {
                    if (draftkings.TryGetProperty("markets", out var markets))
                    {
                        foreach (var market in markets.EnumerateArray())
                        {
                            var marketKey = market.GetProperty("key").GetString();
                            var outcomes = market.GetProperty("outcomes");

                            if (marketKey == "h2h")
                            {
                                ParseMLBMoneyline(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "spreads")
                            {
                                ParseMLBSpreads(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "totals")
                            {
                                ParseMLBTotals(outcomes, oddsViewModel);
                            }
                        }
                    }
                }
            }

            return oddsViewModel;
        }
        catch
        {
            return null;
        }
    }

    private void ParseMLBMoneyline(JsonElement outcomes, string awayTeam, string homeTeam, MLBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var price = outcome.GetProperty("price").GetInt32();

            if (team == awayTeam)
            {
                model.AwayMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
            else if (team == homeTeam)
            {
                model.HomeMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
        }
    }

    private void ParseMLBSpreads(JsonElement outcomes, string awayTeam, string homeTeam, MLBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();
            var pointStr = point > 0 ? $"+{point:0.0}" : point.ToString("0.0");

            if (team == awayTeam)
            {
                model.AwaySpread = pointStr;
                model.AwaySpreadPrice = priceStr;
            }
            else if (team == homeTeam)
            {
                model.HomeSpread = pointStr;
                model.HomeSpreadPrice = priceStr;
            }
        }
    }

    private void ParseMLBTotals(JsonElement outcomes, MLBOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var name = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();

            if (name == "Over")
            {
                model.OverPoint = point.ToString("0.0");
                model.OverPrice = priceStr;
            }
            else if (name == "Under")
            {
                model.UnderPoint = point.ToString("0.0");
                model.UnderPrice = priceStr;
            }
        }
    }

    public async Task<IEnumerable<NHLOddsViewModel>> GetNHLOddsAsync(CancellationToken cancellationToken = default)
    {
        if (_oddsApiClient == null)
        {
            return Enumerable.Empty<NHLOddsViewModel>();
        }

        try
        {
            var response = await _oddsApiClient.GetAsync(
                "sports/icehockey_nhl/odds/?regions=us&markets=h2h,spreads,totals&oddsFormat=american",
                cancellationToken);

            var odds = new List<NHLOddsViewModel>();

            if (response.ValueKind == JsonValueKind.Array)
            {
                foreach (var game in response.EnumerateArray())
                {
                    var gameOdds = ParseNHLGame(game);
                    if (gameOdds != null)
                    {
                        odds.Add(gameOdds);
                    }
                }
            }

            return odds;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"NHL Odds Error: {ex.Message}");
            return Enumerable.Empty<NHLOddsViewModel>();
        }
    }

    private NHLOddsViewModel? ParseNHLGame(JsonElement game)
    {
        try
        {
            var gameId = game.GetProperty("id").GetString() ?? "";
            var awayTeam = game.GetProperty("away_team").GetString() ?? "";
            var homeTeam = game.GetProperty("home_team").GetString() ?? "";
            var commenceTime = game.GetProperty("commence_time").GetDateTime();

            var oddsViewModel = new NHLOddsViewModel
            {
                GameId = gameId,
                AwayTeam = awayTeam,
                HomeTeam = homeTeam,
                CommenceTime = commenceTime
            };

            if (game.TryGetProperty("bookmakers", out var bookmakers) && bookmakers.ValueKind == JsonValueKind.Array)
            {
                var draftkings = bookmakers.EnumerateArray()
                    .FirstOrDefault(b => b.GetProperty("key").GetString() == "draftkings");

                if (draftkings.ValueKind != JsonValueKind.Undefined)
                {
                    if (draftkings.TryGetProperty("markets", out var markets))
                    {
                        foreach (var market in markets.EnumerateArray())
                        {
                            var marketKey = market.GetProperty("key").GetString();
                            var outcomes = market.GetProperty("outcomes");

                            if (marketKey == "h2h")
                            {
                                ParseNHLMoneyline(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "spreads")
                            {
                                ParseNHLSpreads(outcomes, awayTeam, homeTeam, oddsViewModel);
                            }
                            else if (marketKey == "totals")
                            {
                                ParseNHLTotals(outcomes, oddsViewModel);
                            }
                        }
                    }
                }
            }

            return oddsViewModel;
        }
        catch
        {
            return null;
        }
    }

    private void ParseNHLMoneyline(JsonElement outcomes, string awayTeam, string homeTeam, NHLOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var price = outcome.GetProperty("price").GetInt32();

            if (team == awayTeam)
            {
                model.AwayMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
            else if (team == homeTeam)
            {
                model.HomeMoneyline = price > 0 ? $"+{price}" : price.ToString();
            }
        }
    }

    private void ParseNHLSpreads(JsonElement outcomes, string awayTeam, string homeTeam, NHLOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var team = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();
            var pointStr = point > 0 ? $"+{point:0.0}" : point.ToString("0.0");

            if (team == awayTeam)
            {
                model.AwaySpread = pointStr;
                model.AwaySpreadPrice = priceStr;
            }
            else if (team == homeTeam)
            {
                model.HomeSpread = pointStr;
                model.HomeSpreadPrice = priceStr;
            }
        }
    }

    private void ParseNHLTotals(JsonElement outcomes, NHLOddsViewModel model)
    {
        foreach (var outcome in outcomes.EnumerateArray())
        {
            var name = outcome.GetProperty("name").GetString();
            var point = outcome.GetProperty("point").GetDouble();
            var price = outcome.GetProperty("price").GetInt32();

            var priceStr = price > 0 ? $"+{price}" : price.ToString();

            if (name == "Over")
            {
                model.OverPoint = point.ToString("0.0");
                model.OverPrice = priceStr;
            }
            else if (name == "Under")
            {
                model.UnderPoint = point.ToString("0.0");
                model.UnderPrice = priceStr;
            }
        }
    }
}












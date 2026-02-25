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
}




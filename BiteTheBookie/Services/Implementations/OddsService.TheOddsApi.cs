using System.Text.Json;
using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Services.Implementations
{
    public class OddsService : IOddsService
    {
        private readonly ILogger<OddsService> _logger;
        private readonly TheOddsApiClient _client;
        private readonly OddsApiOptions _opts;

        public OddsService(TheOddsApiClient client, ILogger<OddsService> logger, IOptions<OddsApiOptions> opts)
        {
            _client = client;
            _logger = logger;
            _opts = opts.Value;
        }

        public async Task<IEnumerable<HeroOddViewModel>> GetHeroOddsAsync()
        {
            // Lightweight: reuse live odds and project down
            var live = await GetLiveOddsAsync();
            return live.Select(x => new HeroOddViewModel
            {
                GameId = x.GameId,
                AwayAbbrev = x.AwayAbbrev,
                AwayOdds = x.Moneyline,
                HomeAbbrev = x.HomeAbbrev,
                HomeOdds = x.Moneyline
            });
        }

        public async Task<IEnumerable<LiveOddsViewModel>> GetLiveOddsAsync()
        {
            // Example sport key: NFL (americanfootball_nfl)
            // You can parameterize this per league later.
            var sport = "americanfootball_nfl";
            var path = BuildOddsPath(sport);

            try
            {
                var root = await _client.GetAsync(path);
                if (root.ValueKind != JsonValueKind.Array)
                {
                    return Enumerable.Empty<LiveOddsViewModel>();
                }

                var results = new List<LiveOddsViewModel>();
                var i = 0;

                foreach (var ev in root.EnumerateArray())
                {
                    // v4 shape: id, commence_time, home_team, away_team, bookmakers[]
                    var home = ev.TryGetProperty("home_team", out var homeEl) ? homeEl.GetString() ?? "" : "";
                    var away = ev.TryGetProperty("away_team", out var awayEl) ? awayEl.GetString() ?? "" : "";

                    // Extract a single bookmaker/market (first available)
                    var (moneyline, spread, total) = ExtractMarkets(ev);

                    results.Add(new LiveOddsViewModel
                    {
                        GameId = ++i,
                        AwayAbbrev = away,
                        HomeAbbrev = home,
                        AwayScore = 0,
                        HomeScore = 0,
                        Status = "Odds",
                        Moneyline = moneyline,
                        Spread = spread,
                        Total = total
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch live odds.");
                return Enumerable.Empty<LiveOddsViewModel>();
            }
        }

        public Task<LeagueOddsViewModel> GetLeagueOddsAsync()
        {
            // Keep existing contract; can be expanded to multiple sports/league keys.
            return Task.FromResult(new LeagueOddsViewModel());
        }

        private string BuildOddsPath(string sportKey)
        {
            // Example endpoint: /sports/{sportKey}/odds/?apiKey=...&regions=us&markets=h2h,spreads,totals&oddsFormat=american
            var apiKey = Uri.EscapeDataString(_opts.ApiKey ?? string.Empty);
            var regions = Uri.EscapeDataString(_opts.Regions ?? "us");
            var markets = Uri.EscapeDataString(_opts.Markets ?? "h2h,spreads,totals");
            var oddsFormat = Uri.EscapeDataString(_opts.OddsFormat ?? "american");

            return $"sports/{sportKey}/odds/?apiKey={apiKey}&regions={regions}&markets={markets}&oddsFormat={oddsFormat}";
        }

        private static (string moneyline, string spread, string total) ExtractMarkets(JsonElement ev)
        {
            string moneyline = "-";
            string spread = "-";
            string total = "-";

            if (!ev.TryGetProperty("bookmakers", out var bms) || bms.ValueKind != JsonValueKind.Array)
                return (moneyline, spread, total);

            foreach (var bm in bms.EnumerateArray())
            {
                if (!bm.TryGetProperty("markets", out var markets) || markets.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var m in markets.EnumerateArray())
                {
                    var key = m.TryGetProperty("key", out var keyEl) ? keyEl.GetString() : null;
                    if (!m.TryGetProperty("outcomes", out var outcomes) || outcomes.ValueKind != JsonValueKind.Array)
                        continue;

                    if (key == "h2h" && moneyline == "-")
                    {
                        // take first two outcomes prices
                        var prices = outcomes.EnumerateArray()
                            .Select(o => o.TryGetProperty("price", out var p) ? p.ToString() : "")
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Take(2)
                            .ToArray();
                        if (prices.Length > 0)
                            moneyline = string.Join("/", prices);
                    }
                    else if (key == "spreads" && spread == "-")
                    {
                        var points = outcomes.EnumerateArray()
                            .Select(o => o.TryGetProperty("point", out var pt) ? pt.ToString() : "")
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Take(2)
                            .ToArray();
                        if (points.Length > 0)
                            spread = string.Join("/", points);
                    }
                    else if (key == "totals" && total == "-")
                    {
                        var pt = outcomes.EnumerateArray()
                            .Select(o => o.TryGetProperty("point", out var t) ? t.ToString() : "")
                            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
                        if (!string.IsNullOrWhiteSpace(pt))
                            total = pt;
                    }
                }

                // stop after first bookmaker with something
                if (moneyline != "-" || spread != "-" || total != "-")
                    break;
            }

            return (moneyline, spread, total);
        }
    }
}

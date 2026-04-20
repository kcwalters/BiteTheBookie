using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace BiteTheBookie.Services.Implementations
{
    public class NBARosterService : INBARosterService
    {
        private readonly EspnApiClient _espnClient;
        private readonly IMemoryCache _cache;
        private readonly ILogger<NBARosterService> _logger;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        private const string CacheKeyPrefix = "nba_roster_";

        // Team display names — used only when ESPN is unreachable so we still
        // have the correct team name without any stale player data.
        private static readonly Dictionary<string, string> _teamNames = new(StringComparer.OrdinalIgnoreCase)
        {
            { "ATL", "Atlanta Hawks" },       { "BOS", "Boston Celtics" },
            { "BKN", "Brooklyn Nets" },       { "CHA", "Charlotte Hornets" },
            { "CHI", "Chicago Bulls" },       { "CLE", "Cleveland Cavaliers" },
            { "DAL", "Dallas Mavericks" },    { "DEN", "Denver Nuggets" },
            { "DET", "Detroit Pistons" },     { "GSW", "Golden State Warriors" },
            { "HOU", "Houston Rockets" },     { "IND", "Indiana Pacers" },
            { "LAC", "LA Clippers" },         { "LAL", "Los Angeles Lakers" },
            { "MEM", "Memphis Grizzlies" },   { "MIA", "Miami Heat" },
            { "MIL", "Milwaukee Bucks" },     { "MIN", "Minnesota Timberwolves" },
            { "NOP", "New Orleans Pelicans" },{ "NYK", "New York Knicks" },
            { "OKC", "Oklahoma City Thunder" },{ "ORL", "Orlando Magic" },
            { "PHI", "Philadelphia 76ers" },  { "PHX", "Phoenix Suns" },
            { "POR", "Portland Trail Blazers" },{ "SAC", "Sacramento Kings" },
            { "SAS", "San Antonio Spurs" },   { "TOR", "Toronto Raptors" },
            { "UTA", "Utah Jazz" },           { "WAS", "Washington Wizards" },
        };

        public NBARosterService(EspnApiClient espnClient, IMemoryCache cache, ILogger<NBARosterService> logger)
        {
            _espnClient = espnClient;
            _cache = cache;
            _logger = logger;
        }

        /// <inheritdoc/>
        public NBATeamRoster GetTeamRoster(string teamCode)
        {
            if (_cache.TryGetValue<NBATeamRoster>($"{CacheKeyPrefix}{teamCode.ToUpper()}", out var cached) && cached != null)
                return cached;

            // No static fallback — return empty roster; caller must use async version
            return EmptyRoster(teamCode);
        }

        /// <inheritdoc/>
        public async Task<NBATeamRoster> GetTeamRosterAsync(string teamCode, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"{CacheKeyPrefix}{teamCode.ToUpper()}";

            if (_cache.TryGetValue<NBATeamRoster>(cacheKey, out var cached) && cached != null)
            {
                _logger.LogDebug("Returning cached ESPN roster for {Team}", teamCode);
                return cached;
            }

            var espnRoster = await _espnClient.GetTeamRosterAsync(teamCode, cancellationToken);

            if (espnRoster != null && espnRoster.Players.Count > 0)
            {
                _cache.Set(cacheKey, espnRoster, CacheDuration);
                _logger.LogInformation("ESPN roster cached for {Team} ({Count} players)", teamCode, espnRoster.Players.Count);
                return espnRoster;
            }

            // ESPN unreachable — return an empty roster so the simulation
            // prompt explicitly states no roster data is available rather than
            // using stale hardcoded players who may no longer be on the team.
            _logger.LogWarning("ESPN roster unavailable for {Team} — returning empty roster to avoid stale player data", teamCode);
            return EmptyRoster(teamCode);
        }

        private static NBATeamRoster EmptyRoster(string teamCode) => new()
        {
            TeamCode = teamCode.ToUpper(),
            TeamName = _teamNames.GetValueOrDefault(teamCode.ToUpper(), teamCode.ToUpper()),
            Players = new List<NBAPlayer>()
        };
    }
}

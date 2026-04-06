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
        private readonly Dictionary<string, NBATeamRoster> _staticRosters;

        private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);
        private const string CacheKeyPrefix = "nba_roster_";

        public NBARosterService(EspnApiClient espnClient, IMemoryCache cache, ILogger<NBARosterService> logger)
        {
            _espnClient = espnClient;
            _cache = cache;
            _logger = logger;
            _staticRosters = InitializeRosters();
        }

        /// <inheritdoc/>
        public NBATeamRoster GetTeamRoster(string teamCode)
        {
            // Return from cache if a prior async call populated it
            if (_cache.TryGetValue<NBATeamRoster>($"{CacheKeyPrefix}{teamCode.ToUpper()}", out var cached) && cached != null)
                return cached;

            return _staticRosters.GetValueOrDefault(teamCode.ToUpper())
                ?? new NBATeamRoster { TeamCode = teamCode, TeamName = "Unknown Team" };
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

            // Attempt live fetch from ESPN
            var espnRoster = await _espnClient.GetTeamRosterAsync(teamCode, cancellationToken);

            if (espnRoster != null && espnRoster.Players.Count > 0)
            {
                _cache.Set(cacheKey, espnRoster, CacheDuration);
                _logger.LogInformation("ESPN roster cached for {Team} ({Count} players)", teamCode, espnRoster.Players.Count);
                return espnRoster;
            }

            // Fall back to static data
            _logger.LogWarning("ESPN roster unavailable for {Team} — using static fallback", teamCode);
            return _staticRosters.GetValueOrDefault(teamCode.ToUpper())
                ?? new NBATeamRoster { TeamCode = teamCode, TeamName = "Unknown Team" };
        }

        // ── Static fallback data ─────────────────────────────────────────────────

        private static Dictionary<string, NBATeamRoster> InitializeRosters()
        {
            return new Dictionary<string, NBATeamRoster>
            {
                {
                    "BOS", new NBATeamRoster
                    {
                        TeamCode = "BOS", TeamName = "Boston Celtics",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Jayson Tatum",      Position = "F", IsStarter = true  },
                            new() { Name = "Jaylen Brown",      Position = "G", IsStarter = true  },
                            new() { Name = "Kristaps Porzingis",Position = "C", IsStarter = true  },
                            new() { Name = "Jrue Holiday",      Position = "G", IsStarter = true  },
                            new() { Name = "Derrick White",     Position = "G", IsStarter = true  },
                            new() { Name = "Al Horford",        Position = "C", IsStarter = false },
                            new() { Name = "Sam Hauser",        Position = "F", IsStarter = false },
                            new() { Name = "Payton Pritchard",  Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "MIL", new NBATeamRoster
                    {
                        TeamCode = "MIL", TeamName = "Milwaukee Bucks",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Giannis Antetokounmpo", Position = "F", IsStarter = true  },
                            new() { Name = "Damian Lillard",        Position = "G", IsStarter = true  },
                            new() { Name = "Khris Middleton",       Position = "F", IsStarter = true  },
                            new() { Name = "Brook Lopez",           Position = "C", IsStarter = true  },
                            new() { Name = "Gary Trent Jr.",        Position = "G", IsStarter = true  },
                            new() { Name = "Bobby Portis",          Position = "F", IsStarter = false },
                            new() { Name = "Pat Connaughton",       Position = "G", IsStarter = false },
                            new() { Name = "AJ Green",              Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "DEN", new NBATeamRoster
                    {
                        TeamCode = "DEN", TeamName = "Denver Nuggets",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Nikola Jokic",              Position = "C", IsStarter = true  },
                            new() { Name = "Jamal Murray",              Position = "G", IsStarter = true  },
                            new() { Name = "Michael Porter Jr.",        Position = "F", IsStarter = true  },
                            new() { Name = "Aaron Gordon",              Position = "F", IsStarter = true  },
                            new() { Name = "Kentavious Caldwell-Pope",  Position = "G", IsStarter = true  },
                            new() { Name = "Russell Westbrook",         Position = "G", IsStarter = false },
                            new() { Name = "Christian Braun",           Position = "G", IsStarter = false },
                            new() { Name = "Peyton Watson",             Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "UTA", new NBATeamRoster
                    {
                        TeamCode = "UTA", TeamName = "Utah Jazz",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Lauri Markkanen",   Position = "F", IsStarter = true  },
                            new() { Name = "Jordan Clarkson",   Position = "G", IsStarter = true  },
                            new() { Name = "Collin Sexton",     Position = "G", IsStarter = true  },
                            new() { Name = "Walker Kessler",    Position = "C", IsStarter = true  },
                            new() { Name = "John Collins",      Position = "F", IsStarter = true  },
                            new() { Name = "Keyonte George",    Position = "G", IsStarter = false },
                            new() { Name = "Ochai Agbaji",      Position = "G", IsStarter = false },
                            new() { Name = "Simone Fontecchio", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "HOU", new NBATeamRoster
                    {
                        TeamCode = "HOU", TeamName = "Houston Rockets",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Alperen Sengun",  Position = "C", IsStarter = true  },
                            new() { Name = "Jalen Green",     Position = "G", IsStarter = true  },
                            new() { Name = "Dillon Brooks",   Position = "F", IsStarter = true  },
                            new() { Name = "Fred VanVleet",   Position = "G", IsStarter = true  },
                            new() { Name = "Jabari Smith Jr.",Position = "F", IsStarter = true  },
                            new() { Name = "Amen Thompson",   Position = "G", IsStarter = false },
                            new() { Name = "Tari Eason",      Position = "F", IsStarter = false },
                            new() { Name = "Jeff Green",      Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "WAS", new NBATeamRoster
                    {
                        TeamCode = "WAS", TeamName = "Washington Wizards",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Jordan Poole",     Position = "G", IsStarter = true  },
                            new() { Name = "Kyle Kuzma",       Position = "F", IsStarter = true  },
                            new() { Name = "Tyus Jones",       Position = "G", IsStarter = true  },
                            new() { Name = "Daniel Gafford",   Position = "C", IsStarter = true  },
                            new() { Name = "Deni Avdija",      Position = "F", IsStarter = true  },
                            new() { Name = "Corey Kispert",    Position = "G", IsStarter = false },
                            new() { Name = "Bilal Coulibaly",  Position = "G", IsStarter = false },
                            new() { Name = "Marvin Bagley III",Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "LAL", new NBATeamRoster
                    {
                        TeamCode = "LAL", TeamName = "Los Angeles Lakers",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "LeBron James",      Position = "F", IsStarter = true  },
                            new() { Name = "Anthony Davis",     Position = "C", IsStarter = true  },
                            new() { Name = "Austin Reaves",     Position = "G", IsStarter = true  },
                            new() { Name = "D'Angelo Russell",  Position = "G", IsStarter = true  },
                            new() { Name = "Rui Hachimura",     Position = "F", IsStarter = true  },
                            new() { Name = "Jarred Vanderbilt", Position = "F", IsStarter = false },
                            new() { Name = "Gabe Vincent",      Position = "G", IsStarter = false },
                            new() { Name = "Jaxson Hayes",      Position = "C", IsStarter = false }
                        }
                    }
                },
                {
                    "GSW", new NBATeamRoster
                    {
                        TeamCode = "GSW", TeamName = "Golden State Warriors",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Stephen Curry",    Position = "G", IsStarter = true  },
                            new() { Name = "Klay Thompson",    Position = "G", IsStarter = true  },
                            new() { Name = "Andrew Wiggins",   Position = "F", IsStarter = true  },
                            new() { Name = "Draymond Green",   Position = "F", IsStarter = true  },
                            new() { Name = "Kevon Looney",     Position = "C", IsStarter = true  },
                            new() { Name = "Jonathan Kuminga", Position = "F", IsStarter = false },
                            new() { Name = "Moses Moody",      Position = "G", IsStarter = false },
                            new() { Name = "Chris Paul",       Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "PHI", new NBATeamRoster
                    {
                        TeamCode = "PHI", TeamName = "Philadelphia 76ers",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Joel Embiid",           Position = "C", IsStarter = true  },
                            new() { Name = "Tyrese Maxey",          Position = "G", IsStarter = true  },
                            new() { Name = "Tobias Harris",         Position = "F", IsStarter = true  },
                            new() { Name = "Kelly Oubre Jr.",       Position = "F", IsStarter = true  },
                            new() { Name = "De'Anthony Melton",     Position = "G", IsStarter = true  },
                            new() { Name = "Nicolas Batum",         Position = "F", IsStarter = false },
                            new() { Name = "Danuel House Jr.",      Position = "F", IsStarter = false },
                            new() { Name = "Paul Reed",             Position = "C", IsStarter = false }
                        }
                    }
                },
                {
                    "MIA", new NBATeamRoster
                    {
                        TeamCode = "MIA", TeamName = "Miami Heat",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Jimmy Butler",     Position = "F", IsStarter = true  },
                            new() { Name = "Bam Adebayo",      Position = "C", IsStarter = true  },
                            new() { Name = "Tyler Herro",      Position = "G", IsStarter = true  },
                            new() { Name = "Kyle Lowry",       Position = "G", IsStarter = true  },
                            new() { Name = "Caleb Martin",     Position = "F", IsStarter = true  },
                            new() { Name = "Duncan Robinson",  Position = "G", IsStarter = false },
                            new() { Name = "Josh Richardson",  Position = "G", IsStarter = false },
                            new() { Name = "Kevin Love",       Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "BKN", new NBATeamRoster
                    {
                        TeamCode = "BKN", TeamName = "Brooklyn Nets",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Mikal Bridges",         Position = "F", IsStarter = true  },
                            new() { Name = "Cam Thomas",            Position = "G", IsStarter = true  },
                            new() { Name = "Nic Claxton",           Position = "C", IsStarter = true  },
                            new() { Name = "Spencer Dinwiddie",     Position = "G", IsStarter = true  },
                            new() { Name = "Cameron Johnson",       Position = "F", IsStarter = true  },
                            new() { Name = "Dorian Finney-Smith",   Position = "F", IsStarter = false },
                            new() { Name = "Day'Ron Sharpe",        Position = "C", IsStarter = false },
                            new() { Name = "Royce O'Neale",         Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "DAL", new NBATeamRoster
                    {
                        TeamCode = "DAL", TeamName = "Dallas Mavericks",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Luka Doncic",      Position = "G", IsStarter = true  },
                            new() { Name = "Kyrie Irving",     Position = "G", IsStarter = true  },
                            new() { Name = "Dereck Lively II", Position = "C", IsStarter = true  },
                            new() { Name = "Daniel Gafford",   Position = "C", IsStarter = true  },
                            new() { Name = "P.J. Washington",  Position = "F", IsStarter = true  },
                            new() { Name = "Josh Green",       Position = "G", IsStarter = false },
                            new() { Name = "Maxi Kleber",      Position = "F", IsStarter = false },
                            new() { Name = "Tim Hardaway Jr.", Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "PHX", new NBATeamRoster
                    {
                        TeamCode = "PHX", TeamName = "Phoenix Suns",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Kevin Durant",  Position = "F", IsStarter = true  },
                            new() { Name = "Devin Booker",  Position = "G", IsStarter = true  },
                            new() { Name = "Bradley Beal",  Position = "G", IsStarter = true  },
                            new() { Name = "Jusuf Nurkic",  Position = "C", IsStarter = true  },
                            new() { Name = "Grayson Allen", Position = "G", IsStarter = true  },
                            new() { Name = "Eric Gordon",   Position = "G", IsStarter = false },
                            new() { Name = "Drew Eubanks",  Position = "C", IsStarter = false },
                            new() { Name = "Josh Okogie",   Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "LAC", new NBATeamRoster
                    {
                        TeamCode = "LAC", TeamName = "LA Clippers",
                        Players = new List<NBAPlayer>
                        {
                            new() { Name = "Kawhi Leonard",    Position = "F", IsStarter = true  },
                            new() { Name = "Paul George",      Position = "F", IsStarter = true  },
                            new() { Name = "James Harden",     Position = "G", IsStarter = true  },
                            new() { Name = "Ivica Zubac",      Position = "C", IsStarter = true  },
                            new() { Name = "Terance Mann",     Position = "G", IsStarter = true  },
                            new() { Name = "Russell Westbrook",Position = "G", IsStarter = false },
                            new() { Name = "Norman Powell",    Position = "G", IsStarter = false },
                            new() { Name = "Mason Plumlee",    Position = "C", IsStarter = false }
                        }
                    }
                }
            };
        }
    }
}

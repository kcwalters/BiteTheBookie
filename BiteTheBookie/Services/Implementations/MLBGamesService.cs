using System;
using System.Linq;
using System.Collections.Generic;
using BiteTheBookie.Models.MLB;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services
{
    public class MLBGamesService : IMLBGamesService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "https://statsapi.mlb.com/api/v1/";

        public MLBGamesService(HttpClient http) => _http = http;

        public async Task<List<Game>> GetTodayGamesAsync()
        {
            var date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            // hydrate=probablePitcher tells the API to include scheduled starters
            var url = $"schedule?sportId=1&date={date}&hydrate=probablePitcher";
            var schedule = await _http.GetFromJsonAsync<ScheduleResponse>(url);
            var teamUrl = "https://statsapi.mlb.com/api/v1/teams?sportIds=1";
            var teams = await _http.GetFromJsonAsync<TeamsResponse>(teamUrl);

            if (schedule?.Dates == null) return new List<Game>();

            var games = schedule.Dates
                .SelectMany(d => d.Games ?? Enumerable.Empty<GameDto>())
                .Select(g =>
                {
                    var homeTeam = g?.Teams?.Home?.Team;
                    var awayTeam = g?.Teams?.Away?.Team;
                    var homeScore = g?.Teams?.Home?.Score;
                    var awayScore = g?.Teams?.Away?.Score;
                    var status = g?.Status?.DetailedState;

                    var homeTeamInfo = teams?.Teams?.FirstOrDefault(s => s.Id == homeTeam?.Id);
                    var awayTeamInfo = teams?.Teams?.FirstOrDefault(s => s.Id == awayTeam?.Id);

                    string? homeLogo = homeTeamInfo is not null
                        ? $"https://www.mlbstatic.com/team-logos/{homeTeamInfo.Id}.svg"
                        : null;

                    string? awayLogo = awayTeamInfo is not null
                        ? $"https://www.mlbstatic.com/team-logos/{awayTeamInfo.Id}.svg"
                        : null;

                    // Correctly handling GameDate using null-check for g before accessing GameDate
                    var gameTime = g is not null ? g.GameDate.ToLocalTime() : DateTime.MinValue;

                    return new Game
                    {
                        AwayTeam          = awayTeam?.Name ?? string.Empty,
                        AwayTeamId        = awayTeam?.Id ?? 0,
                        HomeTeam          = homeTeam?.Name ?? string.Empty,
                        HomeTeamId        = homeTeam?.Id ?? 0,
                        AwayScore         = awayScore,
                        HomeScore         = homeScore,
                        GameTime          = gameTime,
                        Status            = status ?? string.Empty,
                        HomeTeamLogoUrl   = homeLogo,
                        AwayTeamLogoUrl   = awayLogo,
                        HomeProbablePitcher = g?.Teams?.Home?.ProbablePitcher?.FullName,
                        AwayProbablePitcher = g?.Teams?.Away?.ProbablePitcher?.FullName
                    };
                })
                .ToList();

            return games;
        }

        public async Task<IReadOnlyList<Game>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            try
            {
                var dateStr = date.ToString("yyyy-MM-dd");
                var url = $"schedule?sportId=1&date={dateStr}&hydrate=probablePitcher";
                var schedule = await _http.GetFromJsonAsync<ScheduleResponse>(url);
                var teamUrl = "https://statsapi.mlb.com/api/v1/teams?sportIds=1";
                var teams = await _http.GetFromJsonAsync<TeamsResponse>(teamUrl);

                if (schedule?.Dates == null) return new List<Game>();

                return schedule.Dates
                    .SelectMany(d => d.Games ?? Enumerable.Empty<GameDto>())
                    .Select(g =>
                    {
                        var homeTeamInfo = teams?.Teams?.FirstOrDefault(s => s.Id == g?.Teams?.Home?.Team?.Id);
                        var awayTeamInfo = teams?.Teams?.FirstOrDefault(s => s.Id == g?.Teams?.Away?.Team?.Id);

                        string? homeTeamLogo = homeTeamInfo is not null
                            ? $"https://www.mlbstatic.com/team-logos/{homeTeamInfo.Id}.svg"
                            : null;

                        string? awayTeamLogo = awayTeamInfo is not null
                            ? $"https://www.mlbstatic.com/team-logos/{awayTeamInfo.Id}.svg"
                            : null;

                        // Correctly handling GameDate using null-coalescing operator
                        return new Game
                        {
                            AwayTeam = g?.Teams?.Away?.Team?.Name ?? string.Empty,
                            AwayTeamLogoUrl = awayTeamLogo,
                            AwayScore = g?.Teams?.Away?.Score,
                            HomeTeam = g?.Teams?.Home?.Team?.Name ?? string.Empty,
                            HomeTeamLogoUrl = homeTeamLogo,
                            HomeScore = g?.Teams?.Home?.Score,
                            GameTime = g?.GameDate.ToLocalTime() ?? DateTime.MinValue,
                            Status = g?.Status?.DetailedState ?? string.Empty
                        };
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                return Array.Empty<Game>();
            }
        }
    }
}

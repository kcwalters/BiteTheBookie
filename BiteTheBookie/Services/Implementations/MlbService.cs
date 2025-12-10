using System;
using System.Linq;
using System.Collections.Generic;
using BiteTheBookie.Models.MLB;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services
{
    public class MlbService : IMlbService
    {
        private readonly HttpClient _http;
        private const string BaseUrl = "https://statsapi.mlb.com/api/v1/";

        public MlbService(HttpClient http) => _http = http;

        public async Task<List<Game>> GetTodayGamesAsync()
        {
            var date = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
            var url = $"schedule?sportId=1&date={date}";
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

                    string? homeLogo = null;
                    string? awayLogo = null;

                    if (homeTeamInfo is not null)
                    {
                        homeLogo = $"https://www.mlbstatic.com/team-logos/{homeTeamInfo.Id}.svg";
                    }

                    if (awayTeamInfo is not null)
                    {
                        awayLogo = $"https://www.mlbstatic.com/team-logos/{awayTeamInfo.Id}.svg";
                    }

                    var gameTime = g is not null ? g.GameDate.ToLocalTime() : DateTime.MinValue;

                    return new Game
                    {
                        AwayTeam = awayTeam?.Name,
                        AwayTeamId = awayTeam?.Id ?? 0,
                        HomeTeam = homeTeam?.Name,
                        HomeTeamId = homeTeam?.Id ?? 0,
                        AwayScore = awayScore ?? 0,
                        HomeScore = homeScore ?? 0,
                        GameTime = gameTime,
                        Status = status ?? string.Empty,
                        HomeTeamLogoUrl = homeLogo,
                        AwayTeamLogoUrl = awayLogo
                    };
                })
                .ToList();

            return games;
        }
    }

}

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
            var games = schedule?.Dates
                .SelectMany(d => d.Games)
                .Select(g => new Game
                {
                    AwayTeam = g.Teams.Away.Team.Name,
                    AwayTeamId = g.Teams.Away.Team.Id,
                    HomeTeam = g.Teams.Home.Team.Name,
                    HomeTeamId = g.Teams.Home.Team.Id,
                    AwayScore = g.Teams.Away.Score,
                    HomeScore = g.Teams.Home.Score,
                    GameTime = g.GameDate.ToLocalTime(),
                    Status = g.Status.DetailedState,
                    HomeTeamLogoUrl =  teams.Teams.Where(s=>s.Name == g.Teams.Home.Team.Name).Select(s => s.TeamCode).FirstOrDefault() != null ?
                                  $"https://www.mlbstatic.com/team-logos/{teams.Teams.Where(s => s.Id == g.Teams.Home.Team.Id).Select(s => s.Id).FirstOrDefault()}.svg" : null,
                    AwayTeamLogoUrl = teams.Teams.Where(s => s.Name == g.Teams.Away.Team.Name).Select(s => s.TeamCode).FirstOrDefault() != null ?
                                  $"https://www.mlbstatic.com/team-logos/{teams.Teams.Where(s => s.Id == g.Teams.Away.Team.Id).Select(s => s.Id).FirstOrDefault()}.svg" : null


                })
                .ToList() ?? new List<Game>();

            return games;
        }
    }

}

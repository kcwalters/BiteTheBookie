using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;

namespace BiteTheBookie.Services.Implementations;

public class OddsService : IOddsService
{
    private readonly HttpClient _http;

    public OddsService(HttpClient http)
    {
        _http = http;
    }

    public Task<IEnumerable<HeroOddViewModel>> GetHeroOddsAsync()
        => Task.FromResult(Enumerable.Empty<HeroOddViewModel>());

    public Task<IEnumerable<LiveOddsViewModel>> GetLiveOddsAsync()
        => Task.FromResult(Enumerable.Empty<LiveOddsViewModel>());

    public Task<LeagueOddsViewModel> GetLeagueOddsAsync()
        => Task.FromResult(new LeagueOddsViewModel());
}

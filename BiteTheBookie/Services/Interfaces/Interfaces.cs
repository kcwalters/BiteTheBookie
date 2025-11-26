using BiteTheBookie.ViewModels;
namespace BiteTheBookie.Services.Interfaces
{
    public interface IOddsService
    {
        Task<IEnumerable<HeroOddViewModel>> GetHeroOddsAsync();
        Task<IEnumerable<LiveOddsViewModel>> GetLiveOddsAsync();
        Task<LeagueOddsViewModel> GetLeagueOddsAsync();
    }

    public interface INewsService
    {
        Task<IEnumerable<NewsItemViewModel>> GetLatestNewsAsync(int count = 5);
        Task<NewsItemViewModel> GetNewsByIdAsync(int id);
    }

    public interface IBetSlipService
    {
        Task<BetSlipViewModel> GetBetSlipAsync();
        Task AddBetAsync(AddBetRequest request);
        Task RemoveBetAsync(int gameId);
        Task ClearAsync();
    }
}

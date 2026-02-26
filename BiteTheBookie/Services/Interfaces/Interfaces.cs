using BiteTheBookie.ViewModels;
namespace BiteTheBookie.Services.Interfaces
{
    public interface IOddsService
    {
        Task<IEnumerable<HeroOddViewModel>> GetHeroOddsAsync();
        Task<IEnumerable<LiveOddsViewModel>> GetLiveOddsAsync();
        Task<LeagueOddsViewModel> GetLeagueOddsAsync();
        Task<IEnumerable<NFLOddsViewModel>> GetNFLOddsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<NBAOddsViewModel>> GetNBAOddsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<CBBOddsViewModel>> GetCBBOddsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<MLBOddsViewModel>> GetMLBOddsAsync(CancellationToken cancellationToken = default);
        Task<IEnumerable<NHLOddsViewModel>> GetNHLOddsAsync(CancellationToken cancellationToken = default);
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

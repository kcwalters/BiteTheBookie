using BiteTheBookie.Services.Interfaces;
using BiteTheBookie.ViewModels;
namespace BiteTheBookie.Services.Implementations
{
    public class OddsService : IOddsService
    {
        private readonly HttpClient _http;
        public OddsService(HttpClient http) => _http = http;

        public Task<IEnumerable<HeroOddViewModel>> GetHeroOddsAsync()
            => Task.FromResult(Enumerable.Empty<HeroOddViewModel>());

        public Task<IEnumerable<LiveOddsViewModel>> GetLiveOddsAsync()
            => Task.FromResult(Enumerable.Empty<LiveOddsViewModel>());

        public Task<LeagueOddsViewModel> GetLeagueOddsAsync()
            => Task.FromResult(new LeagueOddsViewModel());
    }

    public class NewsService : INewsService
    {
        public Task<IEnumerable<NewsItemViewModel>> GetLatestNewsAsync(int count = 5)
            => Task.FromResult(Enumerable.Empty<NewsItemViewModel>());

        public Task<NewsItemViewModel> GetNewsByIdAsync(int id)
            => Task.FromResult<NewsItemViewModel?>(null);
    }

    public class BetSlipService : IBetSlipService
    {
        private readonly List<BetSlipItemViewModel> _bets
          = new();

        public Task<BetSlipViewModel> GetBetSlipAsync()
            => Task.FromResult(new BetSlipViewModel { Bets = _bets });

        public Task AddBetAsync(AddBetRequest req)
        {
            _bets.Add(new BetSlipItemViewModel
            {
                GameId = req.GameId,
                Selection = req.Selection,
                OddsDecimal = req.OddsDecimal,
                Stake = req.Stake
            });
            return Task.CompletedTask;
        }

        public Task RemoveBetAsync(int gameId)
        {
            _bets.RemoveAll(b => b.GameId == gameId);
            return Task.CompletedTask;
        }

        public Task ClearAsync()
        {
            _bets.Clear();
            return Task.CompletedTask;
        }
    }
}
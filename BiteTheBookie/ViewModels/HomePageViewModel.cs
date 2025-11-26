using System.Collections.Generic;
using BiteTheBookie.ViewModels;
namespace BiteTheBookie.ViewModels
{
    public class HomePageViewModel
    {
        public IEnumerable<HeroOddViewModel> HeroOdds { get; set; }
        public IEnumerable<NewsItemViewModel> NewsFeed { get; set; }
        public IEnumerable<LiveOddsViewModel> LiveOdds { get; set; }
        public LeagueOddsViewModel LeagueOdds { get; set; }
        public BetSlipViewModel BetSlip { get; set; }

        public HomePageViewModel()
        {
            HeroOdds = new List<HeroOddViewModel>();
            NewsFeed = new List<NewsItemViewModel>();
            LiveOdds = new List<LiveOddsViewModel>();
            LeagueOdds = new LeagueOddsViewModel();
            BetSlip = new BetSlipViewModel();
        }
    }
}

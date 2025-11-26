namespace BiteTheBookie.ViewModels
{
    // BetSlipItemViewModel.cs
    public class BetSlipItemViewModel
    {
        public int GameId { get; set; }
        public string Selection { get; set; }
        public decimal OddsDecimal { get; set; }
        public decimal Stake { get; set; }
    }

    // BetSlipViewModel.cs
    public class BetSlipViewModel
    {
        public IList<BetSlipItemViewModel> Bets { get; set; }
          = new List<BetSlipItemViewModel>();

        public decimal TotalStake
          => Bets.Sum(b => b.Stake);

        public decimal PotentialPayout
          => Bets.Sum(b => b.Stake * b.OddsDecimal);
    }

    // AddBetRequest.cs
    public class AddBetRequest
    {
        public int GameId { get; set; }
        public string Selection { get; set; }
        public decimal OddsDecimal { get; set; }
        public decimal Stake { get; set; }
    }

}

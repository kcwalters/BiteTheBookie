using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class AgainstTheSpreadViewModel
    {
        public string League { get; set; } = "NBA";
        public List<SpreadOpportunity> Opportunities { get; set; } = new();
    }

    public class SpreadOpportunity
    {
        public NBAGameMatchup Game { get; set; } = new();
        public string RecommendedBet { get; set; } = string.Empty; // "Take Home" or "Take Away"
        public decimal Confidence { get; set; } // 0-100
        public string Reasoning { get; set; } = string.Empty;
        public List<string> StatisticalEdges { get; set; } = new();
        public string ValueRating { get; set; } = string.Empty; // "High Value", "Medium Value", etc.
    }
}

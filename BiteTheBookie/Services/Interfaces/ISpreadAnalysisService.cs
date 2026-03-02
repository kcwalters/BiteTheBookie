using BiteTheBookie.Models;
using BiteTheBookie.ViewModels;

namespace BiteTheBookie.Services.Interfaces
{
    public interface ISpreadAnalysisService
    {
        Task<List<SpreadOpportunity>> AnalyzeSpreadOpportunitiesAsync(List<NBAGameMatchup> games, CancellationToken cancellationToken = default);
    }
}

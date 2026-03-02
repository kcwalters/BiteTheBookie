namespace BiteTheBookie.Services.Interfaces
{
    public interface IGameSimulationService
    {
        Task<string> GenerateGameSimulationAsync(string homeTeam, string awayTeam, string league, CancellationToken cancellationToken = default);
    }
}

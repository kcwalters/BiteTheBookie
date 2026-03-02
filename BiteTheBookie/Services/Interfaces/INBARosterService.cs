using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface INBARosterService
    {
        NBATeamRoster GetTeamRoster(string teamCode);
    }
}

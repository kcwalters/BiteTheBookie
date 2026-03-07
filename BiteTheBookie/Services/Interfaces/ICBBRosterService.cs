using BiteTheBookie.Models;

namespace BiteTheBookie.Services.Interfaces
{
    public interface ICBBRosterService
    {
        CBBTeamRoster GetTeamRoster(string teamCode);
    }
}

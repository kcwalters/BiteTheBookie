using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    public class CBBRosterService : ICBBRosterService
    {
        private readonly Dictionary<string, CBBTeamRoster> _rosters;

        public CBBRosterService()
        {
            _rosters = InitializeRosters();
        }

        public CBBTeamRoster GetTeamRoster(string teamCode)
        {
            return _rosters.GetValueOrDefault(teamCode.ToUpper()) 
                ?? new CBBTeamRoster { TeamCode = teamCode, TeamName = "Unknown Team", Conference = "Unknown" };
        }

        private Dictionary<string, CBBTeamRoster> InitializeRosters()
        {
            return new Dictionary<string, CBBTeamRoster>
            {
                {
                    "DUKE", new CBBTeamRoster
                    {
                        TeamCode = "DUKE",
                        TeamName = "Duke Blue Devils",
                        Conference = "ACC",
                        Players = new List<CBBPlayer>
                        {
                            new CBBPlayer { Name = "Kyle Filipowski", Position = "C", IsStarter = true, Year = "SO" },
                            new CBBPlayer { Name = "Tyrese Proctor", Position = "G", IsStarter = true, Year = "SO" },
                            new CBBPlayer { Name = "Jeremy Roach", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Mark Mitchell", Position = "F", IsStarter = true, Year = "SO" },
                            new CBBPlayer { Name = "Ryan Young", Position = "F", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Jared McCain", Position = "G", IsStarter = false, Year = "FR" },
                            new CBBPlayer { Name = "Sean Stewart", Position = "F", IsStarter = false, Year = "FR" }
                        }
                    }
                },
                {
                    "UNC", new CBBTeamRoster
                    {
                        TeamCode = "UNC",
                        TeamName = "North Carolina Tar Heels",
                        Conference = "ACC",
                        Players = new List<CBBPlayer>
                        {
                            new CBBPlayer { Name = "Armando Bacot", Position = "C", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "RJ Davis", Position = "G", IsStarter = true, Year = "JR" },
                            new CBBPlayer { Name = "Caleb Love", Position = "G", IsStarter = true, Year = "JR" },
                            new CBBPlayer { Name = "Harrison Ingram", Position = "F", IsStarter = true, Year = "JR" },
                            new CBBPlayer { Name = "Cormac Ryan", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Seth Trimble", Position = "G", IsStarter = false, Year = "FR" }
                        }
                    }
                },
                {
                    "UK", new CBBTeamRoster
                    {
                        TeamCode = "UK",
                        TeamName = "Kentucky Wildcats",
                        Conference = "SEC",
                        Players = new List<CBBPlayer>
                        {
                            new CBBPlayer { Name = "Rob Dillingham", Position = "G", IsStarter = true, Year = "FR" },
                            new CBBPlayer { Name = "Reed Sheppard", Position = "G", IsStarter = true, Year = "FR" },
                            new CBBPlayer { Name = "Antonio Reeves", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Tre Mitchell", Position = "F", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Ugonna Onyenso", Position = "C", IsStarter = true, Year = "SO" }
                        }
                    }
                },
                {
                    "KU", new CBBTeamRoster
                    {
                        TeamCode = "KU",
                        TeamName = "Kansas Jayhawks",
                        Conference = "Big 12",
                        Players = new List<CBBPlayer>
                        {
                            new CBBPlayer { Name = "Kevin McCullar Jr.", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Dajuan Harris Jr.", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Hunter Dickinson", Position = "C", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "KJ Adams Jr.", Position = "F", IsStarter = true, Year = "JR" },
                            new CBBPlayer { Name = "Johnny Furphy", Position = "F", IsStarter = true, Year = "FR" }
                        }
                    }
                },
                {
                    "GONZ", new CBBTeamRoster
                    {
                        TeamCode = "GONZ",
                        TeamName = "Gonzaga Bulldogs",
                        Conference = "WCC",
                        Players = new List<CBBPlayer>
                        {
                            new CBBPlayer { Name = "Graham Ike", Position = "F", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Nolan Hickman", Position = "G", IsStarter = true, Year = "JR" },
                            new CBBPlayer { Name = "Ryan Nembhard", Position = "G", IsStarter = true, Year = "JR" },
                            new CBBPlayer { Name = "Anton Watson", Position = "F", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Ben Gregg", Position = "F", IsStarter = true, Year = "JR" }
                        }
                    }
                },
                {
                    "CONN", new CBBTeamRoster
                    {
                        TeamCode = "CONN",
                        TeamName = "UConn Huskies",
                        Conference = "Big East",
                        Players = new List<CBBPlayer>
                        {
                            new CBBPlayer { Name = "Tristen Newton", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Cam Spencer", Position = "G", IsStarter = true, Year = "SR" },
                            new CBBPlayer { Name = "Stephon Castle", Position = "G", IsStarter = true, Year = "FR" },
                            new CBBPlayer { Name = "Alex Karaban", Position = "F", IsStarter = true, Year = "SO" },
                            new CBBPlayer { Name = "Donovan Clingan", Position = "C", IsStarter = true, Year = "SO" }
                        }
                    }
                }
            };
        }
    }
}

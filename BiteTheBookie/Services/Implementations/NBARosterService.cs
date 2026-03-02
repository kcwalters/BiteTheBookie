using BiteTheBookie.Models;
using BiteTheBookie.Services.Interfaces;

namespace BiteTheBookie.Services.Implementations
{
    public class NBARosterService : INBARosterService
    {
        private readonly Dictionary<string, NBATeamRoster> _rosters;

        public NBARosterService()
        {
            _rosters = InitializeRosters();
        }

        public NBATeamRoster GetTeamRoster(string teamCode)
        {
            return _rosters.GetValueOrDefault(teamCode.ToUpper()) 
                ?? new NBATeamRoster { TeamCode = teamCode, TeamName = "Unknown Team" };
        }

        private Dictionary<string, NBATeamRoster> InitializeRosters()
        {
            return new Dictionary<string, NBATeamRoster>
            {
                {
                    "BOS", new NBATeamRoster
                    {
                        TeamCode = "BOS",
                        TeamName = "Boston Celtics",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Jayson Tatum", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Jaylen Brown", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Kristaps Porzingis", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Jrue Holiday", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Derrick White", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Al Horford", Position = "C", IsStarter = false },
                            new NBAPlayer { Name = "Sam Hauser", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Payton Pritchard", Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "MIL", new NBATeamRoster
                    {
                        TeamCode = "MIL",
                        TeamName = "Milwaukee Bucks",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Giannis Antetokounmpo", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Damian Lillard", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Khris Middleton", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Brook Lopez", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Gary Trent Jr.", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Bobby Portis", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Pat Connaughton", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "AJ Green", Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "DEN", new NBATeamRoster
                    {
                        TeamCode = "DEN",
                        TeamName = "Denver Nuggets",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Nikola Jokic", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Jamal Murray", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Michael Porter Jr.", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Aaron Gordon", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Kentavious Caldwell-Pope", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Russell Westbrook", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Christian Braun", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Peyton Watson", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "UTA", new NBATeamRoster
                    {
                        TeamCode = "UTA",
                        TeamName = "Utah Jazz",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Lauri Markkanen", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Jordan Clarkson", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Collin Sexton", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Walker Kessler", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "John Collins", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Keyonte George", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Ochai Agbaji", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Simone Fontecchio", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "HOU", new NBATeamRoster
                    {
                        TeamCode = "HOU",
                        TeamName = "Houston Rockets",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Alperen Sengun", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Jalen Green", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Dillon Brooks", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Fred VanVleet", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Jabari Smith Jr.", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Amen Thompson", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Tari Eason", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Jeff Green", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "WAS", new NBATeamRoster
                    {
                        TeamCode = "WAS",
                        TeamName = "Washington Wizards",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Jordan Poole", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Kyle Kuzma", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Tyus Jones", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Daniel Gafford", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Deni Avdija", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Corey Kispert", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Bilal Coulibaly", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Marvin Bagley III", Position = "F", IsStarter = false }
                        }
                    }
                }
            };
        }
    }
}

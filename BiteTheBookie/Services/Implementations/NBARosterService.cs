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
                },
                {
                    "LAL", new NBATeamRoster
                    {
                        TeamCode = "LAL",
                        TeamName = "Los Angeles Lakers",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "LeBron James", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Anthony Davis", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Austin Reaves", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "D'Angelo Russell", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Rui Hachimura", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Jarred Vanderbilt", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Gabe Vincent", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Jaxson Hayes", Position = "C", IsStarter = false }
                        }
                    }
                },
                {
                    "GSW", new NBATeamRoster
                    {
                        TeamCode = "GSW",
                        TeamName = "Golden State Warriors",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Stephen Curry", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Klay Thompson", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Andrew Wiggins", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Draymond Green", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Kevon Looney", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Jonathan Kuminga", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Moses Moody", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Chris Paul", Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "PHI", new NBATeamRoster
                    {
                        TeamCode = "PHI",
                        TeamName = "Philadelphia 76ers",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Joel Embiid", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Tyrese Maxey", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Tobias Harris", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Kelly Oubre Jr.", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "De'Anthony Melton", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Nicolas Batum", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Danuel House Jr.", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Paul Reed", Position = "C", IsStarter = false }
                        }
                    }
                },
                {
                    "MIA", new NBATeamRoster
                    {
                        TeamCode = "MIA",
                        TeamName = "Miami Heat",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Jimmy Butler", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Bam Adebayo", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Tyler Herro", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Kyle Lowry", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Caleb Martin", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Duncan Robinson", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Josh Richardson", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Kevin Love", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "BKN", new NBATeamRoster
                    {
                        TeamCode = "BKN",
                        TeamName = "Brooklyn Nets",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Mikal Bridges", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Cam Thomas", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Nic Claxton", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Spencer Dinwiddie", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Cameron Johnson", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Dorian Finney-Smith", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Day'Ron Sharpe", Position = "C", IsStarter = false },
                            new NBAPlayer { Name = "Royce O'Neale", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "DAL", new NBATeamRoster
                    {
                        TeamCode = "DAL",
                        TeamName = "Dallas Mavericks",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Luka Doncic", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Kyrie Irving", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Dereck Lively II", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Daniel Gafford", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "P.J. Washington", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Josh Green", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Maxi Kleber", Position = "F", IsStarter = false },
                            new NBAPlayer { Name = "Tim Hardaway Jr.", Position = "G", IsStarter = false }
                        }
                    }
                },
                {
                    "PHX", new NBATeamRoster
                    {
                        TeamCode = "PHX",
                        TeamName = "Phoenix Suns",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Kevin Durant", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Devin Booker", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Bradley Beal", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Jusuf Nurkic", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Grayson Allen", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Eric Gordon", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Drew Eubanks", Position = "C", IsStarter = false },
                            new NBAPlayer { Name = "Josh Okogie", Position = "F", IsStarter = false }
                        }
                    }
                },
                {
                    "LAC", new NBATeamRoster
                    {
                        TeamCode = "LAC",
                        TeamName = "LA Clippers",
                        Players = new List<NBAPlayer>
                        {
                            new NBAPlayer { Name = "Kawhi Leonard", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "Paul George", Position = "F", IsStarter = true },
                            new NBAPlayer { Name = "James Harden", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Ivica Zubac", Position = "C", IsStarter = true },
                            new NBAPlayer { Name = "Terance Mann", Position = "G", IsStarter = true },
                            new NBAPlayer { Name = "Russell Westbrook", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Norman Powell", Position = "G", IsStarter = false },
                            new NBAPlayer { Name = "Mason Plumlee", Position = "C", IsStarter = false }
                        }
                    }
                }
            };
        }
    }
}

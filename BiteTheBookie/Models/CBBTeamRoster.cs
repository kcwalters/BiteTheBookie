namespace BiteTheBookie.Models
{
    public class CBBTeamRoster
    {
        public string TeamCode { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string Conference { get; set; } = string.Empty;
        public List<CBBPlayer> Players { get; set; } = new List<CBBPlayer>();
    }
}

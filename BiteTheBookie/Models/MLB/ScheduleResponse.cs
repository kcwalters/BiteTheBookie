namespace BiteTheBookie.Models.MLB
{
    // Models/MLB/ScheduleResponse.cs
    public class ScheduleResponse
    {
        public List<DateInfo> Dates { get; set; }
    }
    public class DateInfo
    {
        public List<GameDto> Games { get; set; }
    }
    public class GameDto
    {
        public DateTime GameDate { get; set; }
        public TeamsInfo Teams { get; set; }
        public StatusInfo Status { get; set; }
    }
    public class TeamsInfo
    {
        public TeamDetail Away { get; set; }
        public TeamDetail Home { get; set; }
    }
    public class TeamDetail
    {
        public Team Team { get; set; }
        public int? Score { get; set; }
    }
    public class Team {
        public int Id { get; set; }
        public string Name { get; set; } 
        public string TeamCode { get; set; }
    }
    public class Score { public int Runs { get; set; } }
    public class StatusInfo { public string DetailedState { get; set; } }

}

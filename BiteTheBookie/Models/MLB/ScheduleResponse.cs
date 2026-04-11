namespace BiteTheBookie.Models.MLB
{
    // Models/MLB/ScheduleResponse.cs
    public class ScheduleResponse
    {
        public List<DateInfo> Dates { get; set; } = new();
    }

    public class DateInfo
    {
        public List<GameDto> Games { get; set; } = new();
    }

    public class GameDto
    {
        public DateTime GameDate { get; set; }
        public TeamsInfo Teams { get; set; } = new();
        public StatusInfo Status { get; set; } = new();
    }

    public class TeamsInfo
    {
        public TeamDetail Away { get; set; } = new();
        public TeamDetail Home { get; set; } = new();
    }

    public class TeamDetail
    {
        public Team Team { get; set; } = new();
        public int? Score { get; set; }

        /// <summary>Populated when the schedule is hydrated with probablePitcher.</summary>
        public ProbablePitcherInfo? ProbablePitcher { get; set; }
    }

    public class ProbablePitcherInfo
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TeamCode { get; set; } = string.Empty;
    }

    public class Score { public int Runs { get; set; } }

    public class StatusInfo
    {
        public string DetailedState { get; set; } = string.Empty;
    }
}

namespace BiteTheBookie.ViewModels;

public class ExpertPickSummary
{
    public int Id { get; set; }
    public string GameId { get; set; } = string.Empty;
    public string League { get; set; } = string.Empty;
    public string AwayTeamName { get; set; } = string.Empty;
    public string HomeTeamName { get; set; } = string.Empty;
    public DateTime GameTime { get; set; }
    public string PickType { get; set; } = string.Empty;
    public string PickSelection { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string? Analysis { get; set; }
    public string EnteredBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
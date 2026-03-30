namespace BiteTheBookie.ViewModels;

public class AdminPicksListViewModel
{
    public string SelectedLeague { get; set; } = "NBA";
    public List<ExpertPickSummary> Picks { get; set; } = [];
}
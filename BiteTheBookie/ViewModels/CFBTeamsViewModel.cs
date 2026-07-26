namespace BiteTheBookie.ViewModels
{
    public class CFBTeamViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public string EspnUrl { get; set; } = string.Empty;
        public string Conference { get; set; } = string.Empty;
    }

    public class CFBConferenceViewModel
    {
        public string Conference { get; set; } = string.Empty;
        public List<CFBTeamViewModel> Teams { get; set; } = new List<CFBTeamViewModel>();
    }

    public class CFBTeamsViewModel
    {
        public List<CFBConferenceViewModel> Conferences { get; set; } = new List<CFBConferenceViewModel>();
    }
}

using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class PicksIndexViewModel
    {
        public string League { get; set; } = "NBA";
        public List<NBAGameMatchup> Games { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
}

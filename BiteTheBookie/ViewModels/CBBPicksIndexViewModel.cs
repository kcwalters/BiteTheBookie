using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class CBBPicksIndexViewModel
    {
        public string League { get; set; } = string.Empty;
        public List<CBBGameMatchup> Games { get; set; } = new List<CBBGameMatchup>();
    }
}

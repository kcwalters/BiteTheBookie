using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class CFBPicksIndexViewModel
    {
        public string League { get; set; } = string.Empty;
        public List<CFBGameMatchup> Games { get; set; } = new List<CFBGameMatchup>();
    }
}

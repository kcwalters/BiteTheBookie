using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BiteTheBookie.ViewModels
{
    public class AdminPickViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Game ID is required")]
        public string GameId { get; set; } = string.Empty;

        [Required(ErrorMessage = "League is required")]
        public string League { get; set; } = "NBA";

        [Required(ErrorMessage = "Away team is required")]
        public string AwayTeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Home team is required")]
        public string HomeTeamName { get; set; } = string.Empty;

        public DateTime GameTime { get; set; } = DateTime.UtcNow;

        [Required(ErrorMessage = "Pick type is required")]
        public string PickType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pick selection is required")]
        public string PickSelection { get; set; } = string.Empty;

        [Range(1, 10, ErrorMessage = "Confidence must be between 1 and 10")]
        public int Confidence { get; set; } = 5;

        public string? Analysis { get; set; }

        /// <summary>
        /// Available NBA games for the Game ID dropdown.
        /// </summary>
        public List<NbaGameOption> AvailableNbaGames { get; set; } = new();

        /// <summary>
        /// Available MLB games for the Game ID dropdown.
        /// </summary>
        public List<MlbGameOption> AvailableMlbGames { get; set; } = new();
    }

    public class MlbGameOption
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string AwayCode { get; set; } = string.Empty;
        public string HomeCode { get; set; } = string.Empty;
        public string AwayName { get; set; } = string.Empty;
        public string HomeName { get; set; } = string.Empty;
        public DateTime GameTime { get; set; }
    }

    public class NbaGameOption
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string AwayCode { get; set; } = string.Empty;
        public string HomeCode { get; set; } = string.Empty;
        public string AwayName { get; set; } = string.Empty;
        public string HomeName { get; set; } = string.Empty;
        public DateTime GameTime { get; set; }
    }
}
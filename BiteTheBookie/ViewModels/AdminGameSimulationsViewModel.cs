using System;
using System.Collections.Generic;
using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class AdminGameSimulationsViewModel
    {
        public IReadOnlyList<GameSimulation> Simulations { get; set; } = new List<GameSimulation>();

        /// <summary>Distinct leagues available for the league filter dropdown.</summary>
        public IReadOnlyList<string> Leagues { get; set; } = new List<string>();

        // Filters
        public string? League { get; set; }
        public string? Search { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        /// <summary>past, future, or all (default).</summary>
        public string Timeframe { get; set; } = "all";

        // Sorting
        public string SortBy { get; set; } = "gamedate";
        public string SortDir { get; set; } = "desc";

        public int TotalCount { get; set; }
    }
}

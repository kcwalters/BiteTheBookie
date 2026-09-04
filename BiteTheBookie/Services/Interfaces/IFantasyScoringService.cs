using System.Text.Json;
using BiteTheBookie.Models.Fantasy;

namespace BiteTheBookie.Services.Interfaces
{
    /// <summary>
    /// Converts real box-score statistics into fantasy points using configurable PPR rules.
    /// </summary>
    public interface IFantasyScoringService
    {
        /// <summary>
        /// Computes fantasy points for an offensive player from an ESPN box-score statistics element.
        /// </summary>
        decimal ScoreOffense(FantasyStatLine stats);

        /// <summary>
        /// Computes fantasy points for a team defense/special teams unit.
        /// </summary>
        decimal ScoreDefense(FantasyDefenseStatLine stats);
    }

    /// <summary>
    /// Normalized offensive stat line extracted from real ESPN box-score data.
    /// </summary>
    public sealed class FantasyStatLine
    {
        public double PassingYards { get; set; }
        public int PassingTouchdowns { get; set; }
        public int Interceptions { get; set; }
        public double RushingYards { get; set; }
        public int RushingTouchdowns { get; set; }
        public double ReceivingYards { get; set; }
        public int Receptions { get; set; }
        public int ReceivingTouchdowns { get; set; }
        public int FumblesLost { get; set; }
        public int TwoPointConversions { get; set; }
    }

    /// <summary>
    /// Normalized team defense/special teams stat line from real ESPN box-score data.
    /// </summary>
    public sealed class FantasyDefenseStatLine
    {
        public int Sacks { get; set; }
        public int Interceptions { get; set; }
        public int FumbleRecoveries { get; set; }
        public int DefensiveTouchdowns { get; set; }
        public int Safeties { get; set; }
        public int PointsAllowed { get; set; }
    }
}

using BiteTheBookie.Models.Fantasy;
using BiteTheBookie.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Converts normalized real box-score stat lines into fantasy points using the
    /// configurable <see cref="FantasyScoringOptions"/> (PPR by default).
    /// </summary>
    public class FantasyScoringService : IFantasyScoringService
    {
        private readonly FantasyScoringOptions _options;

        public FantasyScoringService(IOptions<FantasyScoringOptions> options)
        {
            _options = options?.Value ?? new FantasyScoringOptions();
        }

        public decimal ScoreOffense(FantasyStatLine s)
        {
            if (s is null) return 0m;

            decimal points = 0m;

            if (_options.PassingYardsPerPoint > 0)
                points += (decimal)s.PassingYards / _options.PassingYardsPerPoint;
            points += s.PassingTouchdowns * _options.PassingTouchdown;
            points += s.Interceptions * _options.Interception;

            if (_options.RushingYardsPerPoint > 0)
                points += (decimal)s.RushingYards / _options.RushingYardsPerPoint;
            points += s.RushingTouchdowns * _options.RushingTouchdown;

            if (_options.ReceivingYardsPerPoint > 0)
                points += (decimal)s.ReceivingYards / _options.ReceivingYardsPerPoint;
            points += s.Receptions * _options.Reception;
            points += s.ReceivingTouchdowns * _options.ReceivingTouchdown;

            points += s.FumblesLost * _options.FumbleLost;
            points += s.TwoPointConversions * _options.TwoPointConversion;

            return Math.Round(points, 2);
        }

        public decimal ScoreDefense(FantasyDefenseStatLine s)
        {
            if (s is null) return 0m;

            decimal points = 0m;
            points += s.Sacks * _options.Sack;
            points += s.Interceptions * _options.DefInterception;
            points += s.FumbleRecoveries * _options.FumbleRecovery;
            points += s.DefensiveTouchdowns * _options.DefensiveTouchdown;
            points += s.Safeties * _options.Safety;

            // Standard DFS points-allowed tiers.
            points += s.PointsAllowed switch
            {
                0 => _options.ShutoutBonus,
                <= 6 => 7m,
                <= 13 => 4m,
                <= 20 => 1m,
                <= 27 => 0m,
                <= 34 => -1m,
                _ => -4m
            };

            return Math.Round(points, 2);
        }
    }
}

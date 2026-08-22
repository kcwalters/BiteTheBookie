using BiteTheBookie.Data;
using BiteTheBookie.Models;
using Microsoft.EntityFrameworkCore;

namespace BiteTheBookie.Services
{
    /// <summary>
    /// Outcome of a request to view a premium expert pick.
    /// </summary>
    public sealed record ExpertPickAccessResult(
        bool Granted,
        bool AlreadyUnlocked,
        int WeeklyLimit,
        int WeeklyUsed,
        int WeeklyRemaining,
        string? Reason)
    {
        public bool Unlimited => WeeklyLimit == int.MaxValue;
    }

    public interface IExpertPickAccessService
    {
        /// <summary>
        /// Attempts to grant access to a game's premium expert picks for the given user/tier,
        /// enforcing the weekly limit. Records the unlock when a new game is granted
        /// for a limited (Pro) tier. One unlocked game counts as one weekly pick.
        /// </summary>
        Task<ExpertPickAccessResult> TryUnlockGameAsync(string userId, SubscriptionTier tier, string gameId, string league, CancellationToken cancellationToken = default);

        /// <summary>Returns how many premium games the user can still unlock this week.</summary>
        Task<int> GetWeeklyRemainingAsync(string userId, SubscriptionTier tier, CancellationToken cancellationToken = default);
    }

    public class ExpertPickAccessService : IExpertPickAccessService
    {
        private readonly ApplicationDbContext _db;

        public ExpertPickAccessService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>Start of the current week (Monday 00:00 UTC).</summary>
        internal static DateTime GetWeekStartUtc(DateTime utcNow)
        {
            var date = utcNow.Date;
            // DayOfWeek: Sunday = 0 ... Saturday = 6. Shift so Monday is the first day.
            int diff = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-diff);
        }

        public async Task<ExpertPickAccessResult> TryUnlockGameAsync(string userId, SubscriptionTier tier, string gameId, string league, CancellationToken cancellationToken = default)
        {
            var limit = MembershipEntitlements.For(tier).WeeklyExpertPickLimit;

            // Free tier: preview only, never grants access to full picks.
            if (limit <= 0)
            {
                return new ExpertPickAccessResult(false, false, limit, 0, 0,
                    "Premium expert picks require a Pro or All Access membership.");
            }

            // All Access (unlimited): grant without tracking.
            if (limit == int.MaxValue)
            {
                return new ExpertPickAccessResult(true, false, limit, 0, int.MaxValue, null);
            }

            // Pro (or any finite-limit tier): enforce the weekly count.
            var weekStart = GetWeekStartUtc(DateTime.UtcNow);

            var weeklyViews = await _db.ExpertPickViews
                .Where(v => v.UserId == userId && v.WeekStartUtc == weekStart)
                .ToListAsync(cancellationToken);

            var used = weeklyViews.Count;

            // Already unlocked this game this week? Re-viewing does not consume another slot.
            if (weeklyViews.Any(v => v.GameId == gameId))
            {
                return new ExpertPickAccessResult(true, true, limit, used, Math.Max(0, limit - used), null);
            }

            if (used >= limit)
            {
                return new ExpertPickAccessResult(false, false, limit, used, 0,
                    $"You've reached your limit of {limit} expert picks this week. Upgrade to All Access for unlimited picks.");
            }

            // Grant and record the unlock.
            _db.ExpertPickViews.Add(new ExpertPickView
            {
                UserId = userId,
                GameId = gameId,
                League = league,
                ViewedAtUtc = DateTime.UtcNow,
                WeekStartUtc = weekStart
            });
            await _db.SaveChangesAsync(cancellationToken);

            var remaining = Math.Max(0, limit - (used + 1));
            return new ExpertPickAccessResult(true, false, limit, used + 1, remaining, null);
        }

        public async Task<int> GetWeeklyRemainingAsync(string userId, SubscriptionTier tier, CancellationToken cancellationToken = default)
        {
            var limit = MembershipEntitlements.For(tier).WeeklyExpertPickLimit;
            if (limit <= 0) return 0;
            if (limit == int.MaxValue) return int.MaxValue;

            var weekStart = GetWeekStartUtc(DateTime.UtcNow);
            var used = await _db.ExpertPickViews
                .CountAsync(v => v.UserId == userId && v.WeekStartUtc == weekStart, cancellationToken);

            return Math.Max(0, limit - used);
        }
    }
}

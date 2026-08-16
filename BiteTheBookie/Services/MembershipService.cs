using BiteTheBookie.Models;

namespace BiteTheBookie.Services
{
    public interface IMembershipService
    {
        SubscriptionTier GetUserMembership(string userId);
        bool CanRunSimulation(string userId);
        int GetRemainingSimulations(string userId);
        void IncrementSimulationUsage(string userId);
        bool UpdateMembershipLevel(string userId, SubscriptionTier newLevel);
    }

    /// <summary>
    /// Tracks a user's tier and daily simulation usage. The daily simulation
    /// count resets automatically at the start of each calendar day (UTC).
    /// </summary>
    public class MembershipService : IMembershipService
    {
        private sealed class Usage
        {
            public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;
            public int DailySimulationCount { get; set; }
            public DateOnly UsageDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
        }

        private readonly Dictionary<string, Usage> _userMemberships = new();
        private readonly object _sync = new();

        private Usage GetOrCreate(string userId)
        {
            if (!_userMemberships.TryGetValue(userId, out var usage))
            {
                usage = new Usage();
                _userMemberships[userId] = usage;
            }

            // Reset the daily count when the calendar day changes.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            if (usage.UsageDate != today)
            {
                usage.UsageDate = today;
                usage.DailySimulationCount = 0;
            }

            return usage;
        }

        public SubscriptionTier GetUserMembership(string userId)
        {
            lock (_sync)
            {
                return GetOrCreate(userId).Tier;
            }
        }

        public bool CanRunSimulation(string userId)
        {
            lock (_sync)
            {
                var usage = GetOrCreate(userId);
                var limit = MembershipEntitlements.For(usage.Tier).DailySimulationLimit;
                return usage.DailySimulationCount < limit;
            }
        }

        public int GetRemainingSimulations(string userId)
        {
            lock (_sync)
            {
                var usage = GetOrCreate(userId);
                var limit = MembershipEntitlements.For(usage.Tier).DailySimulationLimit;
                if (limit == int.MaxValue)
                    return int.MaxValue;
                return Math.Max(0, limit - usage.DailySimulationCount);
            }
        }

        public void IncrementSimulationUsage(string userId)
        {
            lock (_sync)
            {
                var usage = GetOrCreate(userId);
                usage.DailySimulationCount++;
            }
        }

        public bool UpdateMembershipLevel(string userId, SubscriptionTier newLevel)
        {
            if (string.IsNullOrEmpty(userId))
                return false;

            lock (_sync)
            {
                GetOrCreate(userId).Tier = newLevel;
            }

            return true;
        }
    }
}
namespace BiteTheBookie.Models
{
    /// <summary>
    /// Convenience helpers for tier feature access. All values are sourced from
    /// <see cref="MembershipEntitlements"/> so there is a single source of truth.
    /// </summary>
    public static class MembershipFeatures
    {
        public static bool AllowsGameSimulation(SubscriptionTier tier)
            => MembershipEntitlements.For(tier).DailySimulationLimit > 0;

        public static int DailySimulationLimit(SubscriptionTier tier)
            => MembershipEntitlements.For(tier).DailySimulationLimit;

        public static int WeeklyExpertPickLimit(SubscriptionTier tier)
            => MembershipEntitlements.For(tier).WeeklyExpertPickLimit;

        public static string Description(SubscriptionTier tier)
            => MembershipEntitlements.For(tier).Tagline;
    }
}

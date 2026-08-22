namespace BiteTheBookie.Models
{
    /// <summary>
    /// Describes everything a given <see cref="SubscriptionTier"/> is entitled to.
    /// This is the single source of truth for pricing, limits, and feature access
    /// across controllers, services, and views.
    /// </summary>
    public sealed class MembershipEntitlement
    {
        public SubscriptionTier Tier { get; init; }

        /// <summary>Display name shown in the UI (e.g. "All Access").</summary>
        public string DisplayName { get; init; } = string.Empty;

        /// <summary>Short marketing tagline for the tier.</summary>
        public string Tagline { get; init; } = string.Empty;

        /// <summary>Monthly price in USD. 0 for Free.</summary>
        public decimal MonthlyPrice { get; init; }

        /// <summary>Max game simulations allowed per calendar day. Use int.MaxValue for unlimited.</summary>
        public int DailySimulationLimit { get; init; }

        /// <summary>Max premium expert picks a member can view per week. 0 for Free (preview only), int.MaxValue for unlimited.</summary>
        public int WeeklyExpertPickLimit { get; init; }

        // ----- Content / analysis feature flags -----
        public bool FullBlogAccess { get; init; }
        public bool FullMatchupAnalysis { get; init; }
        public bool PremiumArticles { get; init; }

        // ----- Simulation feature flags -----
        public bool FullSimulationExplanations { get; init; }
        public bool WinProbabilities { get; init; }
        public bool ProjectedScores { get; init; }
        public bool AdvancedSimulationTools { get; init; }

        // ----- Fantasy feature flags -----
        public bool AdvancedFantasyLeaderboards { get; init; }
        public bool PlayerProjections { get; init; }
        public bool AdvancedFantasyProjections { get; init; }

        // ----- Expert / betting feature flags -----
        public bool ExpertRecordsAndHistories { get; init; }
        public bool AllExpertPicks { get; init; }
        public bool PlayerProps { get; init; }
        public bool CompleteExpertBettingHistory { get; init; }
        public bool AdvancedBettingMarketAnalysis { get; init; }
        public bool LineMovementTracking { get; init; }
        public bool BestOddsComparison { get; init; }

        // ----- Experience feature flags -----
        public bool AdFreeExperience { get; init; }
        public bool SavedFavorites { get; init; }
        public bool Notifications { get; init; }
        public bool MemberOnlyReports { get; init; }
        public bool EarlyAccessToFeatures { get; init; }
        public bool CustomAlerts { get; init; }

        public bool HasUnlimitedSimulations => DailySimulationLimit == int.MaxValue;
        public bool HasUnlimitedExpertPicks => WeeklyExpertPickLimit == int.MaxValue;
    }

    /// <summary>
    /// Central registry describing the entitlements for each membership tier.
    /// </summary>
    public static class MembershipEntitlements
    {
        private static readonly MembershipEntitlement Free = new()
        {
            Tier = SubscriptionTier.Free,
            DisplayName = "Free",
            Tagline = "Basic sports tools and limited features.",
            MonthlyPrice = 0m,
            DailySimulationLimit = 1,
            WeeklyExpertPickLimit = 0,
            // All premium feature flags default to false for Free.
        };

        private static readonly MembershipEntitlement Pro = new()
        {
            Tier = SubscriptionTier.Pro,
            DisplayName = "Pro",
            Tagline = "Full analysis tools, unlimited simulations, and limited expert picks.",
            MonthlyPrice = 9.99m,
            DailySimulationLimit = int.MaxValue,
            WeeklyExpertPickLimit = 5,
            FullBlogAccess = true,
            FullMatchupAnalysis = true,
            FullSimulationExplanations = true,
            WinProbabilities = true,
            ProjectedScores = true,
            AdvancedFantasyLeaderboards = true,
            PlayerProjections = true,
            ExpertRecordsAndHistories = true,
            AdFreeExperience = true,
            SavedFavorites = true,
            Notifications = true,
        };

        private static readonly MembershipEntitlement AllAccess = new()
        {
            Tier = SubscriptionTier.AllAccess,
            DisplayName = "All Access",
            Tagline = "The complete BiteTheBookie experience.",
            MonthlyPrice = 19.99m,
            DailySimulationLimit = int.MaxValue,
            WeeklyExpertPickLimit = int.MaxValue,
            // Everything in Pro...
            FullBlogAccess = true,
            FullMatchupAnalysis = true,
            FullSimulationExplanations = true,
            WinProbabilities = true,
            ProjectedScores = true,
            AdvancedFantasyLeaderboards = true,
            PlayerProjections = true,
            ExpertRecordsAndHistories = true,
            AdFreeExperience = true,
            SavedFavorites = true,
            Notifications = true,
            // ...plus All Access exclusives.
            AllExpertPicks = true,
            PlayerProps = true,
            CompleteExpertBettingHistory = true,
            AdvancedSimulationTools = true,
            AdvancedBettingMarketAnalysis = true,
            LineMovementTracking = true,
            BestOddsComparison = true,
            AdvancedFantasyProjections = true,
            PremiumArticles = true,
            MemberOnlyReports = true,
            EarlyAccessToFeatures = true,
            CustomAlerts = true,
        };

        /// <summary>Returns the entitlement definition for the given tier.</summary>
        public static MembershipEntitlement For(SubscriptionTier tier) => tier switch
        {
            SubscriptionTier.Pro => Pro,
            SubscriptionTier.AllAccess => AllAccess,
            _ => Free
        };

        /// <summary>All tiers in display order (Free, Pro, All Access).</summary>
        public static IReadOnlyList<MembershipEntitlement> All { get; } = new[] { Free, Pro, AllAccess };
    }
}

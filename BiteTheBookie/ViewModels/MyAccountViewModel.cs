using BiteTheBookie.Models;

namespace BiteTheBookie.ViewModels
{
    public class MyAccountViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public SubscriptionTier SubscriptionTier { get; set; }
        public DateTime? SubscriptionExpiry { get; set; }
        public bool IsPro { get; set; }
        public DateTime MemberSince { get; set; }

        /// <summary>True when the user is on the AllAccess tier with active access.</summary>
        public bool IsAllAccess { get; set; }

        /// <summary>True when the user has cancelled recurring billing but still has access until expiry.</summary>
        public bool SubscriptionCancelled { get; set; }

        /// <summary>True when the user has an active paid subscription that can be upgraded or cancelled.</summary>
        public bool HasActivePaidSubscription { get; set; }

        /// <summary>True when an active Pro user is eligible to upgrade to AllAccess.</summary>
        public bool CanUpgradeToAllAccess { get; set; }
    }
}
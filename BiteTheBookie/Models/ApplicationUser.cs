using Microsoft.AspNetCore.Identity;

namespace BiteTheBookie.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }

        /// <summary>Date of birth (used to enforce the 18+ requirement).</summary>
        public DateTime? DateOfBirth { get; set; }

        /// <summary>Street address (line 1).</summary>
        public string? StreetAddress { get; set; }
        public string? City { get; set; }

        /// <summary>Two-letter US state code.</summary>
        public string? State { get; set; }
        public string? ZipCode { get; set; }

        
        /// <summary>
        /// Free, Pro, or All Access
        /// </summary>
        public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
        
        /// <summary>
        /// When the paid subscription expires (null for free users)
        /// </summary>
        public DateTime? SubscriptionExpiry { get; set; }

        /// <summary>
        /// The PayPal subscription id (I-XXXXXXXXXXXX) for a paid recurring plan, if any.
        /// </summary>
        public string? PayPalSubscriptionId { get; set; }

        /// <summary>
        /// The Stripe subscription id (sub_XXXXXXXX) for a paid recurring plan, if any.
        /// </summary>
        public string? StripeSubscriptionId { get; set; }

        /// <summary>
        /// When the user first registered
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Whether the subscription is currently active
        /// </summary>
        public bool IsPremium => SubscriptionTier != SubscriptionTier.Free 
                                 && SubscriptionExpiry.HasValue 
                                 && SubscriptionExpiry.Value > DateTime.UtcNow;
    }

    public enum SubscriptionTier
    {
        Free = 0,
        Pro = 1,
        AllAccess = 2
    }
}
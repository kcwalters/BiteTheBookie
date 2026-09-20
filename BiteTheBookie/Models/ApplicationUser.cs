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
        /// Free, Pro, or AllAccess
        /// </summary>
        public SubscriptionTier SubscriptionTier { get; set; } = SubscriptionTier.Free;
        
        /// <summary>
        /// When the paid subscription expires (null for free users)
        /// </summary>
        public DateTime? SubscriptionExpiry { get; set; }

        /// <summary>
        /// The active PayPal billing subscription id (I-XXXX...). Needed to revise
        /// (upgrade) or cancel the recurring subscription. Null for free users.
        /// </summary>
        public string? PayPalSubscriptionId { get; set; }

        /// <summary>
        /// True once the user has cancelled recurring billing. They keep paid access
        /// until <see cref="SubscriptionExpiry"/>, after which they revert to Free.
        /// </summary>
        public bool SubscriptionCancelled { get; set; }

        /// <summary>
        /// When the user first registered
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Whether the subscription is currently active
        /// </summary>

        public bool IsPro => (SubscriptionTier == SubscriptionTier.Pro || SubscriptionTier == SubscriptionTier.AllAccess)
                                 && SubscriptionExpiry.HasValue 
                                 && SubscriptionExpiry.Value > DateTime.UtcNow;

        public bool AllAccessUser => SubscriptionTier == SubscriptionTier.AllAccess 
                                        && SubscriptionExpiry.HasValue
                                        && SubscriptionExpiry.Value > DateTime.UtcNow;
    }

    public enum SubscriptionTier
    {
        Free = 0,
        Pro = 1,
        AllAccess = 2,
        Admin = 3,
    }
}
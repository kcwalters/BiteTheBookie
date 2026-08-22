using System.Collections.Generic;

namespace BiteTheBookie.Models
{
    /// <summary>
    /// Strongly-typed settings for the Stripe Checkout (subscription) integration.
    /// Bound from the "Stripe" section of configuration.
    /// </summary>
    public class StripeOptions
    {
        public const string SectionName = "Stripe";

        /// <summary>Publishable (browser-safe) API key.</summary>
        public string PublishableKey { get; set; } = string.Empty;

        /// <summary>Secret API key used for server-side Stripe calls.</summary>
        public string SecretKey { get; set; } = string.Empty;

        /// <summary>
        /// Maps a plan key ("pro", "allaccess") to the Stripe recurring Price id
        /// created in the Stripe dashboard.
        /// </summary>
        public Dictionary<string, string> Prices { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>True when both the secret key and publishable key are set.</summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(PublishableKey);

        /// <summary>Returns the Stripe price id for the given plan key, or null.</summary>
        public string? GetPriceId(string? planKey)
        {
            if (string.IsNullOrWhiteSpace(planKey))
            {
                return null;
            }

            return Prices.TryGetValue(planKey, out var priceId) && !string.IsNullOrWhiteSpace(priceId)
                ? priceId
                : null;
        }
    }
}

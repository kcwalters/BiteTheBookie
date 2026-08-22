using System.Collections.Generic;

namespace BiteTheBookie.Models
{
    /// <summary>
    /// Strongly-typed settings for the PayPal Subscriptions integration.
    /// Bound from the "PayPal" section of configuration.
    /// </summary>
    public class PayPalOptions
    {
        public const string SectionName = "PayPal";

        /// <summary>"sandbox" or "live".</summary>
        public string Mode { get; set; } = "sandbox";

        /// <summary>Base REST API URL (sandbox or live).</summary>
        public string BaseUrl { get; set; } = "https://api-m.sandbox.paypal.com";

        /// <summary>PayPal REST app client id.</summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>PayPal REST app secret.</summary>
        public string Secret { get; set; } = string.Empty;

        /// <summary>
        /// Maps a plan key ("pro", "allaccess") to the PayPal billing Plan id
        /// created in the PayPal dashboard.
        /// </summary>
        public Dictionary<string, string> Plans { get; set; } = new(System.StringComparer.OrdinalIgnoreCase);

        /// <summary>Returns the PayPal plan id for the given plan key, or null.</summary>
        public string? GetPlanId(string? planKey)
        {
            if (string.IsNullOrWhiteSpace(planKey))
            {
                return null;
            }

            return Plans.TryGetValue(planKey, out var planId) && !string.IsNullOrWhiteSpace(planId)
                ? planId
                : null;
        }
    }
}

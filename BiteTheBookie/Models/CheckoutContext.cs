namespace BiteTheBookie.Models
{
    /// <summary>
    /// Describes an in-progress PayPal checkout, persisted in server-side session while the
    /// user is redirected to PayPal to approve payment. When they return we use this to decide
    /// whether to create a brand-new account (<see cref="CheckoutMode.NewAccount"/>), subscribe
    /// an existing signed-in account (<see cref="CheckoutMode.ExistingAccount"/>), or upgrade an
    /// existing subscription (<see cref="CheckoutMode.Upgrade"/>).
    /// </summary>
    public class CheckoutContext
    {
        public const string SessionKey = "CheckoutContext";

        /// <summary>The paid plan being purchased: "pro" or "allaccess".</summary>
        public string Plan { get; set; } = string.Empty;

        /// <summary>How the resulting membership should be provisioned.</summary>
        public CheckoutMode Mode { get; set; }

        /// <summary>
        /// The PayPal subscription id. Set after creation (new/existing) or carried over from the
        /// member's current subscription (upgrade/revise).
        /// </summary>
        public string? SubscriptionId { get; set; }
    }

    public enum CheckoutMode
    {
        /// <summary>A new account is created from <c>PendingRegistration</c> after payment.</summary>
        NewAccount = 0,

        /// <summary>A signed-in account with no paid subscription starts a new subscription.</summary>
        ExistingAccount = 1,

        /// <summary>A signed-in Pro member revises their subscription to All Access.</summary>
        Upgrade = 2
    }
}

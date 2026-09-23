namespace BiteTheBookie.Models
{
    /// <summary>
    /// Holds the registration details captured on the sign-up form while the user
    /// completes PayPal checkout. Persisted in server-side session and used to
    /// create the account only after the subscription payment is approved.
    /// No account exists until <see cref="Plan"/> is paid for.
    /// </summary>
    public class PendingRegistration
    {
        public const string SessionKey = "PendingRegistration";

        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string StreetAddress { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>The paid plan the user is subscribing to: "pro" or "allaccess".</summary>
        public string Plan { get; set; } = string.Empty;
    }
}

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// SMTP configuration for sending transactional email (e.g. account confirmation).
    /// Bind from the "Email" configuration section.
    /// </summary>
    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "BiteTheBookie";

        /// <summary>
        /// When true (default in Development), emails are logged instead of sent so the
        /// app works without live SMTP credentials.
        /// </summary>
        public bool UseDevNoOp { get; set; } = false;
    }
}

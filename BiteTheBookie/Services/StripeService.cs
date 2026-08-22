using BiteTheBookie.Models;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace BiteTheBookie.Services
{
    /// <summary>
    /// Details returned when verifying a Stripe Checkout session.
    /// </summary>
    public class StripeSubscriptionResult
    {
        /// <summary>The Stripe subscription id (sub_...).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>active, trialing, past_due, canceled, incomplete, etc.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>When the current paid period ends / next bill date.</summary>
        public DateTime? CurrentPeriodEnd { get; set; }

        public bool IsActive =>
            string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "trialing", StringComparison.OrdinalIgnoreCase);
    }

    public interface IStripeService
    {
        /// <summary>The publishable key exposed to the browser.</summary>
        string PublishableKey { get; }

        /// <summary>True when Stripe secret and publishable keys are configured.</summary>
        bool IsConfigured { get; }

        /// <summary>Resolves the Stripe recurring price id for an app plan key.</summary>
        string? GetPriceId(string? planKey);

        /// <summary>
        /// Creates a hosted Checkout Session (mode=subscription) and returns its redirect URL.
        /// Returns null if Stripe is not configured or the plan has no price id.
        /// </summary>
        Task<string?> CreateCheckoutSessionAsync(
            string planKey,
            string customerEmail,
            string successUrl,
            string cancelUrl,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a completed Checkout Session and verifies the resulting subscription.
        /// </summary>
        Task<StripeSubscriptionResult?> GetSubscriptionFromSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Thin wrapper over the Stripe Checkout / Subscriptions APIs used to collect
    /// recurring card payments and verify the resulting subscription before granting access.
    /// </summary>
    public class StripeService : IStripeService
    {
        private readonly StripeOptions _options;
        private readonly ILogger<StripeService> _logger;

        public StripeService(IOptions<StripeOptions> options, ILogger<StripeService> logger)
        {
            _options = options.Value;
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_options.SecretKey))
            {
                StripeConfiguration.ApiKey = _options.SecretKey;
            }
        }

        public string PublishableKey => _options.PublishableKey;

        public bool IsConfigured => _options.IsConfigured;

        public string? GetPriceId(string? planKey) => _options.GetPriceId(planKey);

        public async Task<string?> CreateCheckoutSessionAsync(
            string planKey,
            string customerEmail,
            string successUrl,
            string cancelUrl,
            CancellationToken cancellationToken = default)
        {
            if (!_options.IsConfigured)
            {
                _logger.LogWarning("Stripe keys are not configured; cannot create a checkout session.");
                return null;
            }

            var priceId = _options.GetPriceId(planKey);
            if (string.IsNullOrWhiteSpace(priceId))
            {
                _logger.LogWarning("No Stripe price id configured for plan {Plan}.", planKey);
                return null;
            }

            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                CustomerEmail = string.IsNullOrWhiteSpace(customerEmail) ? null : customerEmail,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = 1
                    }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options, cancellationToken: cancellationToken);
            return session.Url;
        }

        public async Task<StripeSubscriptionResult?> GetSubscriptionFromSessionAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            if (!_options.IsConfigured || string.IsNullOrWhiteSpace(sessionId))
            {
                return null;
            }

            var sessionService = new SessionService();
            Session session;
            try
            {
                session = await sessionService.GetAsync(sessionId, cancellationToken: cancellationToken);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe checkout session {SessionId} could not be retrieved.", sessionId);
                return null;
            }

            if (string.IsNullOrWhiteSpace(session.SubscriptionId))
            {
                _logger.LogWarning("Stripe session {SessionId} has no subscription id.", sessionId);
                return null;
            }

            var subscriptionService = new SubscriptionService();
            Subscription subscription;
            try
            {
                subscription = await subscriptionService.GetAsync(session.SubscriptionId, cancellationToken: cancellationToken);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe subscription {SubscriptionId} could not be retrieved.", session.SubscriptionId);
                return null;
            }

            return new StripeSubscriptionResult
            {
                Id = subscription.Id,
                Status = subscription.Status ?? string.Empty,
                CurrentPeriodEnd = null
            };
        }
    }
}

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BiteTheBookie.Models;
using Microsoft.Extensions.Options;

namespace BiteTheBookie.Services
{
    /// <summary>
    /// Details returned when verifying a PayPal subscription.
    /// </summary>
    public class PayPalSubscriptionResult
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>APPROVAL_PENDING, APPROVED, ACTIVE, SUSPENDED, CANCELLED, EXPIRED.</summary>
        public string Status { get; set; } = string.Empty;

        public string? PlanId { get; set; }

        public DateTime? NextBillingTime { get; set; }

        public bool IsActive =>
            string.Equals(Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Status, "APPROVED", StringComparison.OrdinalIgnoreCase);
    }

    public interface IPayPalService
    {
        /// <summary>The client id exposed to the browser for the JS SDK.</summary>
        string ClientId { get; }

        /// <summary>Resolves the PayPal billing plan id for an app plan key.</summary>
        string? GetPlanId(string? planKey);

        /// <summary>Fetches and verifies a subscription by id from PayPal.</summary>
        Task<PayPalSubscriptionResult?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Thin client over the PayPal Subscriptions REST API used to verify that a
    /// subscription approved in the browser is genuinely active before granting access.
    /// </summary>
    public class PayPalService : IPayPalService
    {
        private readonly HttpClient _httpClient;
        private readonly PayPalOptions _options;
        private readonly ILogger<PayPalService> _logger;

        public PayPalService(HttpClient httpClient, IOptions<PayPalOptions> options, ILogger<PayPalService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;

            if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            }
        }

        public string ClientId => _options.ClientId;

        public string? GetPlanId(string? planKey) => _options.GetPlanId(planKey);

        public async Task<PayPalSubscriptionResult?> GetSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return null;
            }

            var accessToken = await GetAccessTokenAsync(cancellationToken);
            if (accessToken is null)
            {
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/billing/subscriptions/{subscriptionId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal subscription lookup for {SubscriptionId} failed with status {Status}.",
                    subscriptionId, response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var dto = await JsonSerializer.DeserializeAsync<SubscriptionDto>(stream, cancellationToken: cancellationToken);
            if (dto is null)
            {
                return null;
            }

            return new PayPalSubscriptionResult
            {
                Id = dto.Id ?? subscriptionId,
                Status = dto.Status ?? string.Empty,
                PlanId = dto.PlanId,
                NextBillingTime = dto.BillingInfo?.NextBillingTime
            };
        }

        private async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.Secret))
            {
                _logger.LogWarning("PayPal ClientId/Secret are not configured; cannot obtain access token.");
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/oauth2/token");
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.Secret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal token request failed with status {Status}.", response.StatusCode);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var token = await JsonSerializer.DeserializeAsync<TokenDto>(stream, cancellationToken: cancellationToken);
            return token?.AccessToken;
        }

        private sealed class TokenDto
        {
            [JsonPropertyName("access_token")]
            public string? AccessToken { get; set; }
        }

        private sealed class SubscriptionDto
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("plan_id")]
            public string? PlanId { get; set; }

            [JsonPropertyName("billing_info")]
            public BillingInfoDto? BillingInfo { get; set; }
        }

        private sealed class BillingInfoDto
        {
            [JsonPropertyName("next_billing_time")]
            public DateTime? NextBillingTime { get; set; }
        }
    }
}

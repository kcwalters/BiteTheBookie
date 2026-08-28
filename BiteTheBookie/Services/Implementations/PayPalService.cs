using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BiteTheBookie.Services.Implementations
{
    /// <summary>
    /// Talks to the PayPal REST API (v1 billing subscriptions) directly over HttpClient.
    /// Handles OAuth token retrieval, subscription creation, and subscription verification.
    /// </summary>
    public class PayPalService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalService> _logger;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _baseUrl;

        public PayPalService(HttpClient httpClient, IConfiguration configuration, ILogger<PayPalService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _clientId = configuration["PayPal:ClientId"] ?? string.Empty;
            _clientSecret = configuration["PayPal:ClientSecret"] ?? string.Empty;
            _baseUrl = string.Equals(configuration["PayPal:Environment"], "live", StringComparison.OrdinalIgnoreCase)
                ? "https://api-m.paypal.com"
                : "https://api-m.sandbox.paypal.com";
        }

        /// <summary>
        /// True when real PayPal credentials are present in configuration.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_clientId) &&
            !string.IsNullOrWhiteSpace(_clientSecret) &&
            !_clientId.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The public PayPal client id, used to load the JS SDK on the client.
        /// </summary>
        public string ClientId => _clientId;

        /// <summary>
        /// Generates a short-lived client token required by the JS SDK card-fields
        /// (Advanced Credit and Debit Card) component for on-site card checkout.
        /// </summary>
        public async Task<string> GenerateClientTokenAsync()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("PayPal is not configured.");
            }

            var accessToken = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/identity/generate-token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.AcceptLanguage.ParseAdd("en_US");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal client token request failed: {Status} {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Unable to generate PayPal client token ({(int)response.StatusCode}). {ExtractErrorDetail(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("client_token").GetString()!;
        }

        /// <summary>
        /// Resolves the configured PayPal billing plan id for an app plan key (pro / allaccess).
        /// </summary>
        public string? GetPlanId(string? plan) => plan?.ToLowerInvariant() switch
        {
            "pro" => _configuration["PayPal:PlanId:Pro"],
            "allaccess" => _configuration["PayPal:PlanId:AllAccess"],
            _ => null
        };

        /// <summary>
        /// Pulls a human-readable message out of a PayPal error JSON response body.
        /// </summary>
        private static string ExtractErrorDetail(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return string.Empty;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("error_description", out var errDesc))
                {
                    return errDesc.GetString() ?? string.Empty;
                }

                var message = root.TryGetProperty("message", out var msg) ? msg.GetString() : null;

                if (root.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var detail in details.EnumerateArray())
                    {
                        var issue = detail.TryGetProperty("issue", out var iss) ? iss.GetString() : null;
                        var description = detail.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                        var combined = string.Join(": ", new[] { issue, description }.Where(s => !string.IsNullOrWhiteSpace(s)));
                        if (!string.IsNullOrWhiteSpace(combined))
                        {
                            return message is null ? combined : $"{message} ({combined})";
                        }
                    }
                }

                return message ?? string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/oauth2/token");
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal token request failed: {Status} {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Unable to authenticate with PayPal ({(int)response.StatusCode}). {ExtractErrorDetail(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("access_token").GetString()!;
        }

        /// <summary>
        /// Creates a PayPal subscription for the given app plan and returns the approval URL
        /// the user must be redirected to in order to approve/pay.
        /// </summary>
        public async Task<string> CreateSubscription(string plan, string returnUrl, string cancelUrl)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("PayPal is not configured.");
            }

            var planId = GetPlanId(plan);
            if (string.IsNullOrWhiteSpace(planId))
            {
                throw new InvalidOperationException($"No PayPal plan id configured for plan '{plan}'.");
            }

            var accessToken = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/billing/subscriptions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new
            {
                plan_id = planId,
                application_context = new
                {
                    brand_name = "BiteTheBookie",
                    user_action = "SUBSCRIBE_NOW",
                    // Don't ask for a shipping address for a digital subscription.
                    shipping_preference = "NO_SHIPPING",
                    // UNRESTRICTED lets buyers pay by debit/credit card without a PayPal
                    // account (guest checkout), provided "PayPal Account Optional" is enabled
                    // on the merchant account.
                    payment_method = new
                    {
                        payer_selected = "PAYPAL",
                        payee_preferred = "UNRESTRICTED"
                    },
                    return_url = returnUrl,
                    cancel_url = cancelUrl
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal subscription creation failed: {Status} {Body}", response.StatusCode, body);
                throw new InvalidOperationException($"Unable to create PayPal subscription ({(int)response.StatusCode}). {ExtractErrorDetail(body)}");
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("links", out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    if (link.TryGetProperty("rel", out var rel) &&
                        string.Equals(rel.GetString(), "approve", StringComparison.OrdinalIgnoreCase))
                    {
                        return link.GetProperty("href").GetString()!;
                    }
                }
            }

            throw new InvalidOperationException("PayPal did not return an approval URL.");
        }

        /// <summary>
        /// Checks each configured billing plan (Pro / AllAccess) at startup and logs a clear
        /// warning if it is missing, unreadable, or not in ACTIVE status. Purely diagnostic;
        /// it never throws so it can't block application startup.
        /// </summary>
        public async Task ValidateConfiguredPlansAsync()
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("PayPal is not configured; skipping billing plan validation.");
                return;
            }

            string accessToken;
            try
            {
                accessToken = await GetAccessTokenAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayPal plan validation could not authenticate; check ClientId/ClientSecret and Environment.");
                return;
            }

            foreach (var plan in new[] { "pro", "allaccess" })
            {
                var planId = GetPlanId(plan);
                if (string.IsNullOrWhiteSpace(planId))
                {
                    _logger.LogWarning("PayPal plan '{Plan}' has no PlanId configured (PayPal:PlanId).", plan);
                    continue;
                }

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/billing/plans/{planId}");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await _httpClient.SendAsync(request);
                    var body = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "PayPal plan '{Plan}' (id {PlanId}) could not be found in the '{Environment}' environment: {Status}. {Detail}",
                            plan, planId, _baseUrl, (int)response.StatusCode, ExtractErrorDetail(body));
                        continue;
                    }

                    using var doc = JsonDocument.Parse(body);
                    var status = doc.RootElement.TryGetProperty("status", out var statusEl)
                        ? statusEl.GetString()
                        : null;

                    if (!string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "PayPal plan '{Plan}' (id {PlanId}) is in status '{Status}', not ACTIVE. Subscriptions will fail until it is activated.",
                            plan, planId, status);
                    }
                    else
                    {
                        _logger.LogInformation("PayPal plan '{Plan}' (id {PlanId}) is ACTIVE.", plan, planId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PayPal plan validation failed for plan '{Plan}' (id {PlanId}).", plan, planId);
                }
            }
        }

        /// <summary>
        /// Verifies a subscription is genuinely ACTIVE or APPROVED with PayPal.
        /// </summary>
        public async Task<bool> VerifySubscription(string subscriptionId)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId)) return false;

            var accessToken = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/billing/subscriptions/{subscriptionId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("PayPal subscription lookup failed: {Status} {Body}", response.StatusCode, body);
                return false;
            }

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var statusEl)
                ? statusEl.GetString()
                : null;

            return string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);
        }
    }
}
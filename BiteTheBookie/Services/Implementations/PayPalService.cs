using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
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

        public PayPalService(HttpClient httpClient, IConfiguration configuration, ILogger<PayPalService> logger, IHostEnvironment hostEnvironment)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _clientId = configuration["PayPal:ClientId"] ?? string.Empty;
            _clientSecret = configuration["PayPal:ClientSecret"] ?? string.Empty;
            var environment = configuration["PayPal:Environment"];

            // Decide whether to use the PayPal sandbox or live endpoint.
            // Rules:
            //  - If PayPal:Environment is explicitly set, honor it ("sandbox" -> sandbox, "live"/"production" -> live).
            //  - Otherwise, fall back to the host environment: use sandbox in Development, live elsewhere.
            const string sandboxUrl = "https://api-m.sandbox.paypal.com";
            const string liveUrl = "https://api-m.paypal.com";

            bool useSandbox;
            if (!string.IsNullOrWhiteSpace(environment))
            {
                useSandbox = string.Equals(environment, "sandbox", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(environment, "development", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(environment, "test", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                useSandbox = hostEnvironment.IsDevelopment();
            }

            _baseUrl = useSandbox ? sandboxUrl : liveUrl;
            _logger.LogInformation("PayPal environment resolved to {Mode} (config: '{Environment}', host: '{HostEnv}').",
                useSandbox ? "SANDBOX" : "LIVE", environment ?? "(none)", hostEnvironment.EnvironmentName);

            if (string.IsNullOrWhiteSpace(_clientId))
            {
                _logger.LogWarning("PayPal ClientId is missing from configuration.");
            }
            else if (_clientId.StartsWith("YOUR-", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("The PayPal ClientId in the configuration appears to be a placeholder.");
            }
            else
            {
                var prefix = _clientId.Length >= 6 ? _clientId.Substring(0, 6) : _clientId;
                _logger.LogInformation("Loaded valid PayPal ClientId: {ClientIdPrefix}", prefix + "***");
            }
            
            if (string.IsNullOrWhiteSpace(_clientSecret))
            {
                _logger.LogWarning("PayPal ClientSecret is missing from configuration.");
            }
            else
            {
                _logger.LogInformation("PayPal ClientSecret successfully loaded (hidden for security). Length: {Length}", _clientSecret.Length);
            }
            
            _logger.LogInformation("Setting PayPal API Base URL: {BaseUrl}", _baseUrl);
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
        /// Verifies that the given billing plan id actually exists (and is ACTIVE) in the
        /// PayPal account tied to the configured credentials/environment.
        ///
        /// This guards against the common misconfiguration where the plan id in appsettings
        /// was created under a different account/environment than the current ClientId. In that
        /// case PayPal's JS SDK fails at checkout time with a cryptic
        /// "subscriptions#RESOURCE_NOT_FOUND" error in the browser. Calling this on the server
        /// lets us detect the problem up front and show the user a clear message instead.
        /// </summary>
        /// <returns>True when the plan exists and is ACTIVE; otherwise false.</returns>
        public async Task<bool> IsPlanActiveAsync(string? planId)
        {
            if (string.IsNullOrWhiteSpace(planId) || !IsConfigured)
            {
                return false;
            }

            try
            {
                var accessToken = await GetAccessTokenAsync();

                var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/billing/plans/{planId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // 404 here almost always means the plan id does not belong to the live/sandbox
                    // account that owns the current ClientId (or was never created / was deleted).
                    _logger.LogError(
                        "PayPal plan verification failed for {PlanId} on {BaseUrl}: {Status} {Body}. " +
                        "The plan id likely does not exist in this PayPal account/environment. " +
                        "Re-create the plans with the current live credentials and update PayPal:PlanId in configuration.",
                        planId, _baseUrl, response.StatusCode, body);
                    return false;
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("status", out var status))
                {
                    var value = status.GetString();
                    var isActive = string.Equals(value, "ACTIVE", StringComparison.OrdinalIgnoreCase);
                    if (!isActive)
                    {
                        _logger.LogWarning("PayPal plan {PlanId} exists but is not ACTIVE (status: {Status}).", planId, value);
                    }
                    return isActive;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while verifying PayPal plan {PlanId}.", planId);
                return false;
            }
        }

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

            var isActive = string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "APPROVED", StringComparison.OrdinalIgnoreCase);

            if (!isActive)
            {
                _logger.LogWarning("PayPal subscription {SubscriptionId} is not active yet (status '{Status}').", subscriptionId, status ?? "(none)");
            }

            return isActive;
        }

        /// <summary>
        /// Cancels an active PayPal billing subscription so no further recurring payments
        /// are taken. Returns true when PayPal accepts the cancellation (HTTP 204).
        /// </summary>
        public async Task<bool> CancelSubscription(string subscriptionId, string? reason = null)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return false;
            }

            if (!IsConfigured)
            {
                throw new InvalidOperationException("PayPal is not configured.");
            }

            var accessToken = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/billing/subscriptions/{subscriptionId}/cancel");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var payload = new { reason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by member." : reason };
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            // A successful cancel returns 204 No Content.
            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            var body = await response.Content.ReadAsStringAsync();

            // Treat an already-cancelled/inactive subscription as success so the member
            // isn't blocked from downgrading in our system.
            var detail = ExtractErrorDetail(body);
            if (detail.Contains("SUBSCRIPTION_STATUS_INVALID", StringComparison.OrdinalIgnoreCase)
                || body.Contains("SUBSCRIPTION_STATUS_INVALID", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("PayPal subscription {SubscriptionId} was already inactive when cancelling.", subscriptionId);
                return true;
            }

            _logger.LogError("PayPal subscription cancel failed: {Status} {Body}", response.StatusCode, body);
            return false;
        }
    }
}
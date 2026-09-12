using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace BiteTheBookie.Pages.Diagnostics
{
    public class OutboundIpModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OutboundIpModel> _logger;

        public string ResultMessage { get; private set; } = string.Empty;

        public OutboundIpModel(IHttpClientFactory httpClientFactory, ILogger<OutboundIpModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task OnGetAsync()
        {
            var providers = new[]
            {
                "https://api.ipify.org",
                "https://ifconfig.me/ip",
                "https://icanhazip.com"
            };

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            foreach (var url in providers)
            {
                try
                {
                    var ip = (await client.GetStringAsync(url)).Trim();
                    if (!string.IsNullOrWhiteSpace(ip))
                    {
                        _logger.LogInformation("Detected outbound IP {Ip} via {Url}", ip, url);
                        ResultMessage = $"Outbound IP (as seen by {url}): {ip}";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Outbound IP lookup failed via {Url}", url);
                }
            }

            ResultMessage = "Could not determine the outbound IP address. All lookup providers failed (see logs).";
        }
    }
}

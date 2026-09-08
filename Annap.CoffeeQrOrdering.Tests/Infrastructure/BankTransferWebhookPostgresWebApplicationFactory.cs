using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

/// <summary>Test host with dev bank-transfer webhook enabled for Phase 4A webhook integration tests.</summary>
public sealed class BankTransferWebhookPostgresWebApplicationFactory : AnnapPostgresWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureTestWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BankTransfer:Enabled"] = "true",
                ["BankTransfer:Webhook:DevWebhookEnabled"] = "true",
                ["BankTransfer:Webhook:Secret"] = "test-webhook-secret-16"
            });
        });
    }
}

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

/// <summary>Test host with BankTransfer frozen (Enabled=false) for default-config regression tests.</summary>
public sealed class BankTransferDisabledPostgresWebApplicationFactory : AnnapPostgresWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureTestWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BankTransfer:Enabled"] = "false",
                ["BankTransfer:BankBin"] = "",
                ["BankTransfer:AccountNumber"] = "",
                ["BankTransfer:AccountName"] = ""
            });
        });
    }
}

using Annap.CoffeeQrOrdering.Web;
using Annap.CoffeeQrOrdering.Web.Extensions;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class ProductionInfrastructureConfigurationTests
{
    [Fact]
    public void Database_url_enforces_ssl_in_production()
    {
        var value = InfrastructureEnvironment.ConvertDatabaseUrlToNpgsql(
            "postgresql://annap:secret@example.neon.tech/annap",
            enforceSsl: true);

        var csb = new NpgsqlConnectionStringBuilder(value);
        Assert.Equal(SslMode.Require, csb.SslMode);
    }

    [Fact]
    public void Explicit_connection_string_enforces_ssl_in_production()
    {
        var value = InfrastructureEnvironment.NormalizeConnectionString(
            "Host=example.neon.tech;Database=annap;Username=annap;Password=secret",
            enforceSsl: true);

        var csb = new NpgsqlConnectionStringBuilder(value);
        Assert.Equal(SslMode.Require, csb.SslMode);
    }

    [Fact]
    public void Production_connection_rejects_disabled_ssl()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            InfrastructureEnvironment.NormalizeConnectionString(
                "Host=example.neon.tech;Database=annap;Username=annap;Password=secret;SSL Mode=Disable",
                enforceSsl: true));

        Assert.Contains("SSL", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compose_postgres_host_does_not_force_ssl_in_production()
    {
        var value = InfrastructureEnvironment.NormalizeConnectionString(
            "Host=postgres;Database=annap;Username=postgres;Password=secret",
            enforceSsl: true);

        var csb = new NpgsqlConnectionStringBuilder(value);
        Assert.NotEqual(SslMode.Require, csb.SslMode);
    }

    [Fact]
    public void Menu_media_resolver_accepts_only_secure_Cloudinary_delivery_urls()
    {
        const string cloudinary =
            "https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/item.webp";

        var resolved = MenuMediaResolver.TryResolveCardImageUrl(
            null,
            null,
            cloudinary,
            null,
            "Drink",
            "Coffee");

        Assert.Equal(cloudinary, resolved);
        Assert.False(MenuMediaResolver.IsCloudinaryUrl(
            "http://res.cloudinary.com/annap/image/upload/item.webp"));
        Assert.False(MenuMediaResolver.IsCloudinaryUrl(
            "https://example.com/image/upload/item.webp"));
    }

    [Fact]
    public void Cloudinary_public_id_is_recovered_from_stored_url_for_delete()
    {
        var publicId = MenuImageStorage.TryGetCloudinaryPublicId(
            "https://res.cloudinary.com/annap/image/upload/v1780000000/annap/menu-items/item-poster.webp");

        Assert.Equal("annap/menu-items/item-poster", publicId);
    }

    [Fact]
    public void Superseded_local_url_is_marked_for_delete_after_Cloudinary_replace()
    {
        Assert.True(MenuImageStorage.ShouldDeleteSupersededUrl(
            "/media/menu-items/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.webp",
            "https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.webp"));
        Assert.False(MenuImageStorage.ShouldDeleteSupersededUrl(
            "https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/item.webp",
            "https://res.cloudinary.com/annap/image/upload/v2/annap/menu-items/item.webp"));
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/item.webp", false)]
    [InlineData("/media/menu-items/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.webp", false)]
    [InlineData("https://cdn.example.com/menu/item.webp", true)]
    [InlineData("http://evil.example/x.png", true)]
    public void Media_resolver_purges_unsupported_remote_urls(
        string url,
        bool unsupported)
    {
        Assert.Equal(unsupported, MenuMediaResolver.IsCloudinaryUrl(url) == false && !MenuImagePaths.IsManagedUrl(url));
    }

    [Fact]
    public void DataProtection_keys_survive_a_new_application_instance()
    {
        var keysPath = Path.Combine(Path.GetTempPath(), $"annap-dp-{Guid.NewGuid():N}");
        try
        {
            var protectedValue = ProtectWithNewApplication(keysPath, "release-cookie");
            var unprotectedValue = UnprotectWithNewApplication(keysPath, protectedValue);

            Assert.Equal("release-cookie", unprotectedValue);
            Assert.NotEmpty(Directory.EnumerateFiles(keysPath, "key-*.xml"));
        }
        finally
        {
            if (Directory.Exists(keysPath))
                Directory.Delete(keysPath, recursive: true);
        }
    }

    private static string ProtectWithNewApplication(string keysPath, string value)
    {
        using var app = BuildApplication(keysPath);
        return app.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("release-test")
            .Protect(value);
    }

    private static string UnprotectWithNewApplication(string keysPath, string value)
    {
        using var app = BuildApplication(keysPath);
        return app.Services
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("release-test")
            .Unprotect(value);
    }

    private static WebApplication BuildApplication(string keysPath)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["DataProtection:KeysPath"] = keysPath,
            ["DataProtection:ApplicationName"] = "Annap.Release.Tests"
        });
        builder.AddAnnapWebServices(8080);
        return builder.Build();
    }
}

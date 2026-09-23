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
    // ── NormalizeConnectionString (ConnectionStrings__DefaultConnection path) ──

    [Fact]
    public void NormalizeConnectionString_accepts_standard_Npgsql_key_value_string()
    {
        var result = InfrastructureEnvironment.NormalizeConnectionString(
            "Host=db.example.com;Database=mydb;Username=myuser;Password=secret",
            enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("db.example.com", csb.Host);
        Assert.Equal("mydb", csb.Database);
        Assert.Equal("myuser", csb.Username);
        Assert.Equal("secret", csb.Password);
    }

    [Theory]
    [InlineData("postgresql://user:pass@host.example.com/db")]
    [InlineData("postgres://user:pass@host.example.com/db")]
    [InlineData("postgresql://user:pass@host.example.com:5432/db")]
    public void NormalizeConnectionString_converts_postgres_uri(string uri)
    {
        var result = InfrastructureEnvironment.NormalizeConnectionString(uri, enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("host.example.com", csb.Host);
        Assert.Equal("db", csb.Database);
        Assert.Equal("user", csb.Username);
        Assert.Equal("pass", csb.Password);
        if (uri.Contains(":5432"))
            Assert.Equal(5432, csb.Port);
    }

    [Fact]
    public void NormalizeConnectionString_enforces_ssl_when_flag_is_true()
    {
        var result = InfrastructureEnvironment.NormalizeConnectionString(
            "postgresql://user:pass@neon.example.com/neondb",
            enforceSsl: true);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(SslMode.Require, csb.SslMode);
    }

    [Fact]
    public void NormalizeConnectionString_applies_sslmode_from_uri_query()
    {
        var result = InfrastructureEnvironment.NormalizeConnectionString(
            "postgresql://user:pass@neon.example.com/neondb?sslmode=require",
            enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(SslMode.Require, csb.SslMode);
    }

    [Fact]
    public void NormalizeConnectionString_applies_channel_binding_from_uri_query()
    {
        var result = InfrastructureEnvironment.NormalizeConnectionString(
            "postgresql://user:pass@neon.example.com/neondb?sslmode=require&channel_binding=require",
            enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal(SslMode.Require, csb.SslMode);
        Assert.Equal(ChannelBinding.Require, csb.ChannelBinding);
    }

    [Fact]
    public void NormalizeConnectionString_decodes_url_encoded_password()
    {
        // password = "p@ss:word/"  →  percent-encoded = "p%40ss%3Aword%2F"
        var uri = "postgresql://user:p%40ss%3Aword%2F@host.example.com/mydb";
        var result = InfrastructureEnvironment.NormalizeConnectionString(uri, enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("p@ss:word/", csb.Password);
        Assert.Equal("host.example.com", csb.Host);
    }

    // ── ConvertDatabaseUrlToNpgsql (DATABASE_URL path) ──

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
    public void ConvertDatabaseUrlToNpgsql_applies_channel_binding_require()
    {
        var value = InfrastructureEnvironment.ConvertDatabaseUrlToNpgsql(
            "postgresql://user:pass@host/db?sslmode=require&channel_binding=require",
            enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(value);
        Assert.Equal(SslMode.Require, csb.SslMode);
        Assert.Equal(ChannelBinding.Require, csb.ChannelBinding);
    }

    [Fact]
    public void ConvertDatabaseUrlToNpgsql_decodes_url_encoded_credentials()
    {
        // username = "user@name"  →  "user%40name"
        // password = "pass:word"  →  "pass%3Aword"
        var uri = "postgresql://user%40name:pass%3Aword@host.example.com/db";
        var result = InfrastructureEnvironment.ConvertDatabaseUrlToNpgsql(uri, enforceSsl: false);

        var csb = new NpgsqlConnectionStringBuilder(result);
        Assert.Equal("user@name", csb.Username);
        Assert.Equal("pass:word", csb.Password);
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

    // ── ApplyDeliveryTransforms (Cloudinary delivery-time transforms) ──────────────

    [Fact]
    public void ApplyDeliveryTransforms_appends_to_existing_transformation_segment()
    {
        // Simulates a URL returned from Cloudinary upload with the upload-time
        // w_800,h_800,c_limit,q_auto:good transformation already applied.
        var stored =
            "https://res.cloudinary.com/annap/image/upload/w_800,h_800,c_limit,q_auto:good/annap/menu-items/abc123-poster.webp";

        var result = MenuMediaResolver.ApplyDeliveryTransforms(stored, "f_auto,q_auto");

        Assert.Equal(
            "https://res.cloudinary.com/annap/image/upload/w_800,h_800,c_limit,q_auto:good,f_auto,q_auto/annap/menu-items/abc123-poster.webp",
            result);
    }

    [Fact]
    public void ApplyDeliveryTransforms_inserts_before_version_segment()
    {
        // URL with a version prefix but no transformation segment — the delivery
        // transforms are inserted as a new segment between /upload/ and v…/ .
        var stored =
            "https://res.cloudinary.com/annap/image/upload/v1/annap/menu-items/abc123-poster.webp";

        var result = MenuMediaResolver.ApplyDeliveryTransforms(stored, "f_auto,q_auto");

        Assert.Equal(
            "https://res.cloudinary.com/annap/image/upload/f_auto,q_auto/v1/annap/menu-items/abc123-poster.webp",
            result);
    }

    [Fact]
    public void ApplyDeliveryTransforms_is_idempotent_when_already_transformed()
    {
        // A URL that has already had f_auto,q_auto appended must not produce a
        // second pair on a repeated call (AllTransformsPresent guard).
        var transformed =
            "https://res.cloudinary.com/annap/image/upload/w_800,h_800,c_limit,q_auto:good,f_auto,q_auto/annap/menu-items/abc123-poster.webp";

        var result = MenuMediaResolver.ApplyDeliveryTransforms(transformed, "f_auto,q_auto");

        Assert.Equal(transformed, result);
    }

    [Theory]
    [InlineData("https://example.com/images/photo.jpg")]
    [InlineData("/media/menu-items/abc123.webp")]
    [InlineData("/images/menu-fallback.svg")]
    public void ApplyDeliveryTransforms_passes_through_non_Cloudinary_urls(string url)
    {
        Assert.Equal(url, MenuMediaResolver.ApplyDeliveryTransforms(url, "f_auto,q_auto"));
    }

    [Theory]
    [InlineData(null, "f_auto,q_auto", "")]
    [InlineData("", "f_auto,q_auto", "")]
    [InlineData("  ", "f_auto,q_auto", "")]
    [InlineData("https://res.cloudinary.com/annap/image/upload/v1/annap/x.webp", "", "https://res.cloudinary.com/annap/image/upload/v1/annap/x.webp")]
    public void ApplyDeliveryTransforms_handles_null_empty_and_whitespace(string? url, string transforms, string expected)
    {
        Assert.Equal(expected, MenuMediaResolver.ApplyDeliveryTransforms(url, transforms));
    }

    [Fact]
    public void ApplyDeliveryTransforms_preserves_public_id_folder_and_version()
    {
        var stored =
            "https://res.cloudinary.com/demo/image/upload/v1712345678/my-folder/sub-folder/item-poster.webp";

        var result = MenuMediaResolver.ApplyDeliveryTransforms(stored, "f_auto,q_auto");

        Assert.Contains("v1712345678", result);
        Assert.Contains("my-folder/sub-folder/item-poster.webp", result);
        Assert.StartsWith("https://res.cloudinary.com/demo/image/upload/f_auto,q_auto/v", result);
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

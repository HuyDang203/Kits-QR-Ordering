using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Auth;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Services;
using Annap.CoffeeQrOrdering.Infrastructure.KiotViet.Workers;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>((_, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.CommandTimeout(45);
                    // Enables pgvector mappings via Pgvector.EntityFrameworkCore
                    npgsql.UseVector();
                });

            if (configuration.GetValue("Diagnostics:VerboseSqlLogging", false))
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }
        });

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddScoped<IMenuInventoryGate, MenuInventoryGate>();

        services.AddMemoryCache();

        // ── KiotViet Integration ──────────────────────────────────────────
        services.Configure<KiotVietOptions>(configuration.GetSection(KiotVietOptions.SectionName));

        // Singleton: token cache must survive across scoped lifetimes; SemaphoreSlim is per-instance.
        services.AddSingleton<KiotVietTokenProvider>();

        // Transient: IHttpClientFactory rebuilds the handler pipeline per logical client;
        // DelegatingHandler instances must not be shared across pipelines.
        services.AddTransient<KiotVietAuthDelegatingHandler>();
        services.AddTransient<KiotVietRetailerHeaderHandler>();

        // Token client — plain HTTP, no auth, used only to acquire OAuth2 tokens.
        services.AddHttpClient(KiotVietNamedHttpClients.Token, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // API client — BaseAddress from config, Bearer + Retailer headers on every call.
        // Auth handler is outermost: its 401-retry loop re-traverses the Retailer handler so
        // all headers are present on both the first attempt and the token-rotation retry.
        // BaseAddress is resolved at first-use via (sp, client) factory — safe if config is empty.
        services.AddHttpClient(KiotVietNamedHttpClients.Api, (sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<KiotVietOptions>>().Value;
            client.BaseAddress = opts.ResolvedBaseUri();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddHttpMessageHandler<KiotVietAuthDelegatingHandler>()
        .AddHttpMessageHandler<KiotVietRetailerHeaderHandler>();

        services.AddScoped<IKiotVietOrderSyncService, KiotVietOrderSyncService>();
        services.AddHostedService<KvOrderDispatchWorker>();

        return services;
    }
}


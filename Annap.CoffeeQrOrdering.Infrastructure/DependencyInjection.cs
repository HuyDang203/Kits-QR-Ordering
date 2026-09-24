using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        // PricingService reads the PricingPromotionSettings singleton and
        // applies the global discount factor on catalog render and at
        // order submit time. Single arithmetic path; no in-memory cache.
        services.AddScoped<IPricingService, PricingService>();

        services.AddMemoryCache();

        return services;
    }
}

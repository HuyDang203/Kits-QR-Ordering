using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Keep Application thin; add services here as the app grows (e.g., MediatR, validators).
        return services;
    }
}


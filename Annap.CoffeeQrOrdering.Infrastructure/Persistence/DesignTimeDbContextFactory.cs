using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence;

/// <summary>Allows <c>dotnet ef</c> without building the Web host project.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        var cs = Environment.GetEnvironmentVariable("ANNAP_DESIGN_PG")
                   ?? "Host=localhost;Port=5432;Database=annap_qr_ordering;Username=postgres;Password=postgres";
        builder.UseNpgsql(cs, npgsql => npgsql.UseVector());
        return new AppDbContext(builder.Options);
    }
}

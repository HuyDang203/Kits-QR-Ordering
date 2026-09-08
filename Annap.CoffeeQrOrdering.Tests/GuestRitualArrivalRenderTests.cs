using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Annap.CoffeeQrOrdering.Tests;

public sealed class GuestRitualArrivalRenderTests(AnnapPostgresWebApplicationFactory factory) : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Valid_seated_qr_arrival_renders_ritual_sommelier_entry()
    {
        await SeedTableAsync("T12");
        var client = factory.CreateClient();

        var response = await client.GetAsync("/table/T12");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("guest-experience-root", html);
        Assert.DoesNotContain("id=\"ge-ritual-begin\"", html);
        Assert.DoesNotContain("ge-atelier-return", html);
        Assert.DoesNotContain("đã được chuẩn bị", html);
        Assert.Contains("Mời bạn", html);
        Assert.Contains("Sẵn sàng phục vụ", html);
        Assert.Contains("data-table-vi=", html);
        Assert.Contains("data-table-en=\"TABLE ", html);
        Assert.Contains("images/arrival/AnnapBackground.webp", html);
        Assert.Contains("id=\"annap-arrival\"", html);
        Assert.Contains("id=\"annap-arrival-invite\"", html);
        Assert.Contains("arrival/arrival.js", html);
        Assert.DoesNotContain("annap-arrival__window", html);
        Assert.DoesNotContain("annap-arrival__cup", html);
        Assert.DoesNotContain("annap-arrival__steam", html);
        Assert.Contains("class=\"annap-arrival\"", html);
        Assert.Contains("role=\"region\"", html);
        Assert.DoesNotContain("class=\"annap-arrival\"\n     role=\"dialog\"", html);
        Assert.Contains("id=\"ge-panel-sommelier\"", html);
        Assert.Contains("id=\"ge-sommelier-step\"", html);
        Assert.Contains("id=\"ge-sommelier-results\"", html);
        Assert.Contains("guest-experience.js", html);
        Assert.DoesNotContain("id=\"guest-arrival-slim\"", html);
        Assert.DoesNotContain("guest-arrival-slim", html);
        Assert.DoesNotContain("arrival-scene", html);
        Assert.DoesNotContain("choreography.js", html);
        Assert.Contains("css/arrival.css", html);
    }

    [Fact]
    public async Task Menu_with_valid_vt_keeps_seated_slim_layout()
    {
        var vt = await SeedTableAsync("T12");
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/Menu/Index?vt={vt:D}");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("annap-seated-guest", html);
        Assert.DoesNotContain("Vui lòng quét mã QR tại bàn để gọi món.", html);
    }

    [Fact]
    public async Task Menu_without_vt_still_blocks_ordering_safely()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/Menu/Index");
        var html = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Vui lòng quét mã QR tại bàn để gọi món.", html);
    }

    [Fact]
    public async Task Sommelier_lite_config_endpoint_still_available()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/guest/sommelier-lite/config");

        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> SeedTableAsync(string displayCode)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var existing = await db.VenueTables.FirstOrDefaultAsync(v => v.DisplayCode == displayCode);
        if (existing is not null)
            return existing.Id;

        var table = new VenueTable
        {
            VenueCode = "annap",
            DisplayCode = displayCode,
            DisplayLabel = displayCode,
            PublicSlug = $"annap-{displayCode.ToLowerInvariant()}",
            IsActive = true
        };
        db.VenueTables.Add(table);
        await db.SaveChangesAsync(CancellationToken.None);
        return table.Id;
    }
}

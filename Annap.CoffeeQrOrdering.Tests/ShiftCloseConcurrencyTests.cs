using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Tests.Infrastructure;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace Annap.CoffeeQrOrdering.Tests;

/// <summary>
/// Blocker B1 regression proof: two (or more) authorized devices pressing "Kết ca" at the same
/// moment must never produce two shift-close rows over the same paid-order window.
/// </summary>
public sealed class ShiftCloseConcurrencyTests(AnnapPostgresWebApplicationFactory factory)
    : IClassFixture<AnnapPostgresWebApplicationFactory>
{
    [Fact]
    public async Task Two_simultaneous_closes_yield_one_row_and_revenue_counted_once()
    {
        var seededTotal = await SeedPaidShiftAsync(70000m, 55000m, 80000m);
        var before = await CountRealClosesAsync();

        var results = await Task.WhenAll(CloseFromFreshScopeAsync(), CloseFromFreshScopeAsync());

        Assert.Equal(1, results.Count(r => r.Success));
        var loser = results.Single(r => !r.Success);
        Assert.False(string.IsNullOrWhiteSpace(loser.ErrorMessage));
        Assert.Null(loser.Entity);

        var newRows = await LoadRealClosesAfterAsync(before);
        var row = Assert.Single(newRows);
        Assert.Equal(seededTotal, row.TotalGrossAmount);
        Assert.Equal(3, row.TotalOrders);
    }

    [Fact]
    public async Task Four_simultaneous_closes_yield_one_row()
    {
        var seededTotal = await SeedPaidShiftAsync(60000m, 45000m);
        var before = await CountRealClosesAsync();

        var results = await Task.WhenAll(
            CloseFromFreshScopeAsync(),
            CloseFromFreshScopeAsync(),
            CloseFromFreshScopeAsync(),
            CloseFromFreshScopeAsync());

        Assert.Equal(1, results.Count(r => r.Success));

        var newRows = await LoadRealClosesAfterAsync(before);
        var row = Assert.Single(newRows);
        Assert.Equal(seededTotal, row.TotalGrossAmount);
        Assert.Equal(2, row.TotalOrders);
    }

    [Fact]
    public async Task Duplicate_window_insert_is_rejected_by_database()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var opened = DateTimeOffset.UtcNow.AddHours(-1);

        db.ShiftCloses.Add(NewClose(opened, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        db.ShiftCloses.Add(NewClose(opened, DateTimeOffset.UtcNow.AddSeconds(1)));
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.True(OrderSubmitHelper.IsUniqueViolation(ex));
    }

    private static ShiftClose NewClose(DateTimeOffset openedAtUtc, DateTimeOffset closedAtUtc) => new()
    {
        OpenedAtUtc = openedAtUtc,
        ClosedAtUtc = closedAtUtc,
        ClosedBy = "concurrency-test",
        TotalOrders = 1,
        TotalGrossAmount = 10000m,
        CashOrCardAmount = 10000m,
        BankTransferAmount = 0,
        UnknownPaymentAmount = 0,
        SnapshotJson = "{}",
        CreatedAtUtc = closedAtUtc
    };

    private async Task<decimal> SeedPaidShiftAsync(params decimal[] amounts)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await ShiftCloseTestHelper.IsolateWindowAsync(db);
        var fixture = await OrderTestSeedHelper.SeedMinimalOrderSubmitDataAsync(db);

        foreach (var amount in amounts)
        {
            var paidAt = ShiftCloseTestHelper.PaidAfterBoundary();
            db.Orders.Add(new Order
            {
                VenueTableId = fixture.VenueTableId,
                TableCode = "T01",
                Status = OrderStatus.Paid,
                PaymentMethod = OrderPaymentMethods.Cash,
                PaymentConfirmedBy = "Concurrency QA",
                PaidAtUtc = paidAt,
                TotalAmount = amount,
                BillNumber = $"C{Guid.NewGuid():N}"[..9].ToUpperInvariant(),
                CreatedAtUtc = paidAt.AddMinutes(-5)
            });
        }

        await db.SaveChangesAsync();
        return amounts.Sum();
    }

    private Task<ShiftCloseResultVm> CloseFromFreshScopeAsync() => Task.Run(async () =>
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IShiftCloseService>();
        return await svc.CloseShiftAsync(TestPrincipal());
    });

    private async Task<int> CountRealClosesAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ShiftCloses.CountAsync(s => s.ClosedBy != "test-isolation");
    }

    private async Task<List<ShiftClose>> LoadRealClosesAfterAsync(int before)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var all = await db.ShiftCloses.AsNoTracking()
            .Where(s => s.ClosedBy != "test-isolation")
            .OrderBy(s => s.ClosedAtUtc)
            .ToListAsync();
        return all.Skip(before).ToList();
    }

    private static ClaimsPrincipal TestPrincipal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "StaffCheckout")], "test"));
}

using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using System.Security.Claims;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

internal static class ShiftCloseTestHelper
{
    /// <summary>
    /// Inserts the newest shift close so preview queries only see orders paid after it.
    /// </summary>
    internal static async Task<DateTimeOffset> IsolateWindowAsync(AppDbContext db)
    {
        var boundary = DateTimeOffset.UtcNow;
        db.ShiftCloses.Add(new ShiftClose
        {
            OpenedAtUtc = boundary.AddHours(-8),
            ClosedAtUtc = boundary,
            ClosedBy = "test-isolation",
            TotalOrders = 0,
            TotalGrossAmount = 0,
            CashOrCardAmount = 0,
            BankTransferAmount = 0,
            UnknownPaymentAmount = 0,
            SnapshotJson = "{}",
            CreatedAtUtc = boundary
        });
        await db.SaveChangesAsync();
        await Task.Delay(100);
        return boundary;
    }

    internal static DateTimeOffset PaidAfterBoundary() => DateTimeOffset.UtcNow;

    internal static ClaimsPrincipal TestCheckoutPrincipal() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "StaffCheckout")], "test"));
}

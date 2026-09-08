using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.Services;

public static class OperationalAudit
{
    public static async Task AppendAsync(
        IApplicationDbContext db,
        string actionKind,
        string? actor,
        Guid? orderId,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var row = new OperationalAuditEntry
        {
            OccurredAtUtc = DateTimeOffset.UtcNow,
            ActionKind = actionKind.Trim(),
            Actor = string.IsNullOrWhiteSpace(actor) ? null : actor.Trim(),
            OrderId = orderId,
            Summary = summary.Length > 2000 ? summary[..1997] + "…" : summary
        };
        await db.OperationalAuditEntries.AddAsync(row, cancellationToken);
    }
}

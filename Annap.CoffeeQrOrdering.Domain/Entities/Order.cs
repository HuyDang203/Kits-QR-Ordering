using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class Order : AuditableEntity
{
    public Guid? VenueTableId { get; set; }
    public VenueTable? VenueTable { get; set; }

    /// <summary>Snapshot of table code at order time (and legacy rows without <see cref="VenueTableId"/>).</summary>
    public string TableCode { get; set; } = null!;

    /// <summary>Opaque guest credential for track/reconnect without accounts. Null on legacy rows.</summary>
    public string? GuestSessionToken { get; set; }

    /// <summary>Guest submit idempotency key (header or body). Unique when set.</summary>
    public string? SubmitIdempotencyKey { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Submitted;

    /// <summary>Last time <see cref="Status"/> changed (brew timer / floor pacing).</summary>
    public DateTimeOffset? StatusChangedAtUtc { get; set; }

    /// <summary>When checkout staff confirmed payment.</summary>
    public DateTimeOffset? PaidAtUtc { get; set; }

    /// <summary>Staff identity that confirmed payment.</summary>
    public string? PaymentConfirmedBy { get; set; }

    /// <summary>Individual staff account that confirmed payment (null for shared-password or webhook).</summary>
    public Guid? PaymentConfirmedByAccountId { get; set; }

    /// <summary>Optional payment method label (cash, card, etc.).</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Optional order-level note from the guest (e.g. less ice, no cream).</summary>
    public string? CustomerNote { get; set; }

    /// <summary>Human-readable bill number shown on electronic bill.</summary>
    public string? BillNumber { get; set; }

    /// <summary>When barista marked the order complete.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Staff display name that completed the order.</summary>
    public string? CompletedBy { get; set; }

    /// <summary>Individual staff account that completed the order (null for shared barista).</summary>
    public Guid? CompletedByAccountId { get; set; }

    /// <summary>Optional staff name (cookie identity) brewing this ticket.</summary>
    public string? BrewingOwnerStaffName { get; set; }

    /// <summary>Optional staff name carrying the handoff.</summary>
    public string? ServingOwnerStaffName { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    public decimal TotalAmount { get; set; }

    public void RecalculateTotals()
    {
        TotalAmount = Items.Sum(i => i.UnitPrice * i.Quantity);
    }
}


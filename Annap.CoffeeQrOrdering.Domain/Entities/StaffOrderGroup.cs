using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>
/// Staff-side grouping of multiple independent Orders into a single visual
/// aggregate for the staff board. This entity is strictly a presentation layer
/// concern. The underlying Orders and OrderItems are never modified.
/// </summary>
public sealed class StaffOrderGroup : AuditableEntity
{
    /// <summary>Staff display name that created the group.</summary>
    public string CreatedByStaffName { get; set; } = null!;

    /// <summary>Staff account id (null for shared-password / anonymous staff).</summary>
    public Guid? CreatedByStaffAccountId { get; set; }

    public List<StaffOrderGroupMember> Members { get; set; } = [];
}
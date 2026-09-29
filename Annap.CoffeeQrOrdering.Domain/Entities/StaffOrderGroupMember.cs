namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>
/// Join row binding a single Order into a <see cref="StaffOrderGroup"/>.
/// Membership is the only thing stored here; the underlying Order is untouched.
/// </summary>
public sealed class StaffOrderGroupMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid GroupId { get; set; }
    public StaffOrderGroup Group { get; set; } = null!;

    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string AddedByStaffName { get; set; } = null!;

    public Guid? AddedByStaffAccountId { get; set; }
}
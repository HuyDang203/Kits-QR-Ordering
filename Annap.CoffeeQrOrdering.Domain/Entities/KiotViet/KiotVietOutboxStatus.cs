namespace Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;

public enum KiotVietOutboxStatus
{
    Pending = 0,
    Processing = 1,
    Succeeded = 2,
    Failed = 3,
    DeadLettered = 4
}

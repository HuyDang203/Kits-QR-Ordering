namespace Annap.CoffeeQrOrdering.Domain.Entities;

public enum OrderStatus
{
    Draft = 0,
    Submitted = 1,
    InProgress = 2,
    Ready = 3,
    Completed = 4,
    Cancelled = 5,
    /// <summary>Final presentation before calling the guest (customer-facing “finishing touches”).</summary>
    FinishingTouches = 6,
    /// <summary>Staff confirmed payment; order is ready for barista prep.</summary>
    Paid = 7
}


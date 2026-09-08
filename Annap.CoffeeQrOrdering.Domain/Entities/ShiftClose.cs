namespace Annap.CoffeeQrOrdering.Domain.Entities;



/// <summary>Recorded end-of-shift summary for counter staff (Kết ca).</summary>

public sealed class ShiftClose

{

    public Guid Id { get; set; } = Guid.NewGuid();



    public DateTimeOffset OpenedAtUtc { get; set; }



    public DateTimeOffset ClosedAtUtc { get; set; }



    public string ClosedBy { get; set; } = null!;



    public Guid? ClosedByAccountId { get; set; }



    public int TotalOrders { get; set; }



    public decimal TotalGrossAmount { get; set; }



    public decimal CashOrCardAmount { get; set; }



    public decimal BankTransferAmount { get; set; }



    public decimal UnknownPaymentAmount { get; set; }



    public int CashOrCardOrders { get; set; }



    public int BankTransferOrders { get; set; }



    public int UnknownPaymentOrders { get; set; }



    public string SnapshotJson { get; set; } = null!;



    public string? Notes { get; set; }



    public DateTimeOffset CreatedAtUtc { get; set; }

}



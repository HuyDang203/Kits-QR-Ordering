namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Canonical guest payment method values persisted on <see cref="Domain.Entities.Order.PaymentMethod"/>.</summary>
internal static class OrderPaymentMethods
{
    public const string Cash = "Cash";
    public const string Card = "Card";
    public const string BankTransfer = "BankTransfer";

    /// <summary>Legacy combined counter payment (pre-split).</summary>
    public const string CashOrCardAtCounter = "CashOrCardAtCounter";

    public static string NormalizeOrDefault(string? raw) => Normalize(raw) ?? Cash;

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t.Equals(Cash, StringComparison.OrdinalIgnoreCase))
            return Cash;
        if (t.Equals(Card, StringComparison.OrdinalIgnoreCase))
            return Card;
        if (t.Equals(BankTransfer, StringComparison.OrdinalIgnoreCase)
            || t.Equals("bank", StringComparison.OrdinalIgnoreCase)
            || t.Equals("transfer", StringComparison.OrdinalIgnoreCase))
            return BankTransfer;
        if (t.Equals(CashOrCardAtCounter, StringComparison.OrdinalIgnoreCase)
            || t.Equals("counter", StringComparison.OrdinalIgnoreCase))
            return CashOrCardAtCounter;

        return null;
    }

    public static (string Vi, string En) Labels(string? method) => method switch
    {
        Cash => ("Tiền mặt", "Cash"),
        Card => ("Thẻ", "Card"),
        BankTransfer => ("Chuyển khoản", "Bank transfer"),
        CashOrCardAtCounter => ("Tiền mặt/thẻ", "Cash/Card (legacy)"),
        _ => ("Chưa rõ", "Unknown")
    };

    public static (string Vi, string En) PendingStatusLabels(string? method) => method switch
    {
        BankTransfer => ("Chờ chuyển khoản", "Waiting for bank transfer"),
        Card => ("Chờ thanh toán bằng thẻ tại quầy", "Waiting for card payment at counter"),
        Cash => ("Chờ thanh toán tiền mặt tại quầy", "Waiting for cash payment at counter"),
        CashOrCardAtCounter => ("Chờ thanh toán tại quầy", "Waiting for counter payment"),
        _ => ("Chờ thanh toán", "Awaiting payment")
    };

    public static string SubmittedBadgeVi(string? method) => method switch
    {
        BankTransfer => "Chuyển khoản · chờ xác nhận",
        Card => "Thẻ · chờ thanh toán",
        Cash => "Tiền mặt · chờ thanh toán",
        CashOrCardAtCounter => "Tại quầy · tiền mặt/thẻ",
        _ => "Chờ thanh toán"
    };

    public static PaymentShiftBucket ClassifyForShiftClose(string? method)
    {
        if (string.Equals(method, Cash, StringComparison.Ordinal))
            return PaymentShiftBucket.Cash;
        if (string.Equals(method, Card, StringComparison.Ordinal))
            return PaymentShiftBucket.Card;
        if (string.Equals(method, BankTransfer, StringComparison.Ordinal))
            return PaymentShiftBucket.BankTransfer;
        if (string.Equals(method, CashOrCardAtCounter, StringComparison.Ordinal))
            return PaymentShiftBucket.LegacyCashOrCard;
        return PaymentShiftBucket.Unknown;
    }
}

internal enum PaymentShiftBucket
{
    Cash,
    Card,
    BankTransfer,
    LegacyCashOrCard,
    Unknown
}

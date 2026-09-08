namespace Annap.CoffeeQrOrdering.Web;

/// <summary>Bank transfer QR configuration (appsettings:BankTransfer). Production values via env vars.</summary>
public sealed class BankTransferOptions
{
    public const string SectionName = "BankTransfer";

    public bool Enabled { get; set; }

    public string Provider { get; set; } = "VietQR";

    public string BankBin { get; set; } = "";

    public string BankName { get; set; } = "";

    public string AccountNumber { get; set; } = "";

    public string AccountName { get; set; } = "";

    /// <summary>Transfer memo template. Use <c>{Reference}</c> for bill number.</summary>
    public string DescriptionTemplate { get; set; } = "KITS {Reference}";

    /// <summary>
    /// Optional VietQR image URL template. Placeholders:
    /// {bankBin}, {accountNumber}, {accountName}, {amount}, {memo}, {reference}
    /// </summary>
    public string QrImageUrlTemplate { get; set; } = "";

    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(BankBin)
        && !string.IsNullOrWhiteSpace(AccountNumber)
        && !string.IsNullOrWhiteSpace(AccountName);

    public string DefaultQrImageUrlTemplate =>
        "https://img.vietqr.io/image/{bankBin}-{accountNumber}-compact2.png?amount={amount}&addInfo={memo}&accountName={accountName}";

    public BankTransferWebhookOptions Webhook { get; set; } = new();
}

/// <summary>Bank transfer webhook settings (appsettings:BankTransfer:Webhook).</summary>
public sealed class BankTransferWebhookOptions
{
    public bool DevWebhookEnabled { get; set; }

    public string Secret { get; set; } = "";
}

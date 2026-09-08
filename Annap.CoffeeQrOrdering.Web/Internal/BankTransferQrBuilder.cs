using System.Globalization;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal sealed record BankTransferQrDto(
    bool Enabled,
    string Status,
    string Message,
    string? Provider = null,
    long? Amount = null,
    string? AmountFormatted = null,
    string? Reference = null,
    string? Memo = null,
    string? BankName = null,
    string? BankBin = null,
    string? AccountNumber = null,
    string? AccountName = null,
    string? QrImageUrl = null)
{
    public static BankTransferQrDto Unavailable(string messageVi) =>
        new(false, "unavailable", messageVi);

    public static BankTransferQrDto Paid(string messageVi) =>
        new(true, "paid", messageVi);

    public static BankTransferQrDto Paid(
        BankTransferOptions opts,
        Order order,
        string messageVi,
        string reference,
        string memo) =>
        new(
            true,
            "paid",
            messageVi,
            opts.Provider,
            (long)order.TotalAmount,
            VndMoneyFormatter.Format(order.TotalAmount),
            reference,
            memo,
            string.IsNullOrWhiteSpace(opts.BankName) ? null : opts.BankName.Trim(),
            string.IsNullOrWhiteSpace(opts.BankBin) ? null : opts.BankBin.Trim(),
            string.IsNullOrWhiteSpace(opts.AccountNumber) ? null : opts.AccountNumber.Trim(),
            string.IsNullOrWhiteSpace(opts.AccountName) ? null : opts.AccountName.Trim());

    public static BankTransferQrDto Pending(
        BankTransferOptions opts,
        Order order,
        string reference,
        string memo,
        string qrImageUrl) =>
        new(
            true,
            "pending",
            "Vui lòng chuyển đúng số tiền và nội dung để nhân viên xác nhận nhanh hơn.",
            opts.Provider,
            (long)order.TotalAmount,
            VndMoneyFormatter.Format(order.TotalAmount),
            reference,
            memo,
            string.IsNullOrWhiteSpace(opts.BankName) ? null : opts.BankName.Trim(),
            opts.BankBin.Trim(),
            opts.AccountNumber.Trim(),
            opts.AccountName.Trim(),
            qrImageUrl);

}

internal sealed class BankTransferQrBuilder(IOptions<BankTransferOptions> options)
{
    private const string UnavailableMessageVi =
        "Chuyển khoản hiện chưa khả dụng. Vui lòng thanh toán tại quầy.";

    private const string PaidMessageVi = "Thanh toán đã được xác nhận.";

    private readonly BankTransferOptions _opts = options.Value;

    public bool IsConfigured => _opts.IsConfigured;

    public BankTransferQrDto BuildGuestAvailability() =>
        _opts.IsConfigured
            ? new BankTransferQrDto(true, "available", "Chuyển khoản khả dụng.")
            : BankTransferQrDto.Unavailable(UnavailableMessageVi);

    public BankTransferQrDto? BuildForTrack(Order order)
    {
        if (!string.Equals(order.PaymentMethod, OrderPaymentMethods.BankTransfer, StringComparison.Ordinal))
            return null;

        return Build(order);
    }

    public BankTransferQrDto Build(Order order)
    {
        if (!string.Equals(order.PaymentMethod, OrderPaymentMethods.BankTransfer, StringComparison.Ordinal))
            return BankTransferQrDto.Unavailable("Đơn này không dùng chuyển khoản.");

        if (order.Status == OrderStatus.Cancelled)
            return BankTransferQrDto.Unavailable("Đơn đã bị hủy.");

        if (order.Status == OrderStatus.Completed)
            return BuildPaid(order);

        if (!StaffOrderBoardColumnHelper.IsAwaitingPayment(order.Status)
            && order.Status != OrderStatus.Paid
            && !StaffOrderBoardColumnHelper.IsPaidForPrep(order.Status))
        {
            return BuildPaid(order);
        }

        if (StaffOrderBoardColumnHelper.IsPaidForPrep(order.Status) || order.Status == OrderStatus.Paid)
            return BuildPaid(order);

        if (!_opts.IsConfigured)
            return BankTransferQrDto.Unavailable(UnavailableMessageVi);

        if (order.TotalAmount <= 0)
            return BankTransferQrDto.Unavailable("Số tiền đơn không hợp lệ.");

        var reference = OrderBillHelper.EnsureBillNumber(order);
        var memo = BuildMemoForOrder(order);
        var qrImageUrl = BuildQrImageUrl(order, reference, memo);
        if (string.IsNullOrWhiteSpace(qrImageUrl))
            return BankTransferQrDto.Unavailable(UnavailableMessageVi);

        return BankTransferQrDto.Pending(_opts, order, reference, memo, qrImageUrl);
    }

    private BankTransferQrDto BuildPaid(Order order)
    {
        var reference = OrderBillHelper.EnsureBillNumber(order);
        var memo = BuildMemoForOrder(order);
        return BankTransferQrDto.Paid(_opts, order, PaidMessageVi, reference, memo);
    }

    public string BuildMemoForOrder(Order order)
    {
        var reference = OrderBillHelper.EnsureBillNumber(order);
        return BuildMemo(reference, order.TableCode);
    }

    public string BuildMemo(string reference, string? tableCode = null)
    {
        var template = string.IsNullOrWhiteSpace(_opts.DescriptionTemplate)
            ? "KITS {Reference}"
            : _opts.DescriptionTemplate.Trim();
        return template
            .Replace("{Reference}", reference.Trim(), StringComparison.OrdinalIgnoreCase)
            .Replace("{TableCode}", (tableCode ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public string? BuildQrImageUrl(Order order, string reference, string memo)
    {
        if (!_opts.IsConfigured)
            return null;

        var template = string.IsNullOrWhiteSpace(_opts.QrImageUrlTemplate)
            ? _opts.DefaultQrImageUrlTemplate
            : _opts.QrImageUrlTemplate.Trim();

        var amount = ((long)order.TotalAmount).ToString(CultureInfo.InvariantCulture);
        var bankBin = _opts.BankBin.Trim();
        var accountNumber = _opts.AccountNumber.Trim();
        var accountName = _opts.AccountName.Trim();

        return template
            .Replace("{bankBin}", bankBin, StringComparison.OrdinalIgnoreCase)
            .Replace("{accountNumber}", accountNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{reference}", reference, StringComparison.OrdinalIgnoreCase)
            .Replace("{amount}", Uri.EscapeDataString(amount), StringComparison.OrdinalIgnoreCase)
            .Replace("{memo}", Uri.EscapeDataString(memo), StringComparison.OrdinalIgnoreCase)
            .Replace("{accountName}", Uri.EscapeDataString(accountName), StringComparison.OrdinalIgnoreCase);
    }

    public static string MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return "—";
        var digits = accountNumber.Trim();
        if (digits.Length <= 4)
            return "****";
        return new string('*', digits.Length - 4) + digits[^4..];
    }
}

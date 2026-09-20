using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal sealed record OrderBillLineDto(
    string Name,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string? Notes,
    string? CustomerNote = null);

internal sealed record OrderBillDto(
    string ShopName,
    string BillNumber,
    Guid OrderId,
    string TableCode,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string PaymentStatus,
    string PaymentStatusLabelVi,
    string PaymentStatusLabelEn,
    string ThankYouMessageVi,
    string ThankYouMessageEn,
    decimal TotalAmount,
    IReadOnlyList<OrderBillLineDto> Items,
    string BillKind = "paid",
    string TitleVi = "Hóa đơn đã thanh toán",
    string TitleEn = "Paid receipt",
    string TotalLabelVi = "Tổng cộng",
    string TotalLabelEn = "Total",
    string? PaymentMethod = null,
    string? PaymentMethodLabelVi = null,
    string? PaymentMethodLabelEn = null,
    string? PaymentConfirmedBy = null,
    string? CompletedBy = null,
    string? ProvisionalNoteVi = null,
    string? ProvisionalNoteEn = null,
    string? FulfillmentMode = null,
    string? FulfillmentModeLabelVi = null,
    string? FulfillmentModeLabelEn = null,
    string? ServingLocationVi = null,
    string? ServingLocationEn = null);

internal static class OrderBillHelper
{
    public const string ShopName = "Kits Coffee Atelier";

    public static string EnsureBillNumber(Order order)
    {
        if (!string.IsNullOrWhiteSpace(order.BillNumber))
            return order.BillNumber.Trim();

        var shortId = order.Id.ToString("N")[..8].ToUpperInvariant();
        return $"A{shortId}";
    }

    public static OrderBillDto Build(Order order, bool includePendingSummary = false) =>
        includePendingSummary && StaffOrderBoardColumnHelper.IsAwaitingPayment(order.Status)
            ? BuildCheckBill(order)
            : BuildPaidReceipt(order);

    public static OrderBillDto BuildCheckBill(Order order)
    {
        var core = BuildCore(order);
        var (methodVi, methodEn) = OrderPaymentMethods.Labels(order.PaymentMethod);
        var (fulfillmentVi, fulfillmentEn) = FulfillmentModes.Labels(order.FulfillmentMode);
        var (servingVi, servingEn) = ResolveServingLocation(order);
        return core with
        {
            BillKind = "provisional",
            TitleVi = "Phiếu kiểm đơn",
            TitleEn = "Order check bill",
            TotalLabelVi = "Tạm tính",
            TotalLabelEn = "Estimated total",
            PaymentStatus = "pending_payment",
            PaymentStatusLabelVi = "Chờ thanh toán",
            PaymentStatusLabelEn = "Awaiting payment",
            PaymentMethod = order.PaymentMethod,
            PaymentMethodLabelVi = methodVi,
            PaymentMethodLabelEn = methodEn,
            ProvisionalNoteVi = "Phiếu này dùng để kiểm tra món, chưa phải hóa đơn đã thanh toán.",
            ProvisionalNoteEn = "This check bill is for review only and is not a paid receipt.",
            ThankYouMessageVi = "",
            ThankYouMessageEn = "",
            FulfillmentMode = order.FulfillmentMode,
            FulfillmentModeLabelVi = fulfillmentVi,
            FulfillmentModeLabelEn = fulfillmentEn,
            ServingLocationVi = servingVi,
            ServingLocationEn = servingEn
        };
    }

    public static OrderBillDto BuildPaidReceipt(Order order)
    {
        var core = BuildCore(order);
        var (status, labelVi, labelEn) = ResolvePaymentLabels(order.Status);
        var (methodVi, methodEn) = OrderPaymentMethods.Labels(order.PaymentMethod);
        var (fulfillmentVi, fulfillmentEn) = FulfillmentModes.Labels(order.FulfillmentMode);
        var (servingVi, servingEn) = ResolveServingLocation(order);
        return core with
        {
            BillKind = "paid",
            TitleVi = "Hóa đơn đã thanh toán",
            TitleEn = "Paid receipt",
            TotalLabelVi = "Tổng cộng",
            TotalLabelEn = "Total",
            PaymentStatus = status,
            PaymentStatusLabelVi = labelVi,
            PaymentStatusLabelEn = labelEn,
            PaymentMethod = order.PaymentMethod,
            PaymentMethodLabelVi = methodVi,
            PaymentMethodLabelEn = methodEn,
            PaymentConfirmedBy = order.PaymentConfirmedBy,
            CompletedBy = order.CompletedBy,
            ThankYouMessageVi = "Cảm ơn bạn đã ghé Kits.",
            ThankYouMessageEn = "Thank you for visiting Kits.",
            FulfillmentMode = order.FulfillmentMode,
            FulfillmentModeLabelVi = fulfillmentVi,
            FulfillmentModeLabelEn = fulfillmentEn,
            ServingLocationVi = servingVi,
            ServingLocationEn = servingEn
        };
    }

    /// <summary>
    /// Returns the customer-facing "serving location" label.
    /// For Takeaway orders, this is always "Take away" — never a table code.
    /// For Dine-in orders, this is "Bàn {TableCode}".
    /// For legacy null orders, defaults to the table code.
    /// </summary>
    private static (string Vi, string En) ResolveServingLocation(Order order)
    {
        if (string.Equals(order.FulfillmentMode, FulfillmentModes.Takeaway, StringComparison.Ordinal))
        {
            return ("Take away", "Take away");
        }
        if (string.Equals(order.FulfillmentMode, FulfillmentModes.DineIn, StringComparison.Ordinal))
        {
            return ($"Bàn {order.TableCode}", $"Table {order.TableCode}");
        }
        // Legacy null: preserve historical table code.
        if (!string.IsNullOrWhiteSpace(order.TableCode))
        {
            return ($"Bàn {order.TableCode}", $"Table {order.TableCode}");
        }
        return ("Không xác định", "Unknown");
    }

    private static OrderBillDto BuildCore(Order order)
    {
        var billNumber = EnsureBillNumber(order);
        var lines = order.Items.Select(i => new OrderBillLineDto(
            i.MenuItemName ?? i.MenuItem?.Name ?? "—",
            i.Quantity,
            i.UnitPrice,
            i.UnitPrice * i.Quantity,
            string.IsNullOrWhiteSpace(i.Notes) ? null : i.Notes.Trim(),
            string.IsNullOrWhiteSpace(i.CustomerNote) ? null : i.CustomerNote.Trim())).ToList();

        var snapshotTotal = lines.Sum(l => l.LineTotal);
        return new OrderBillDto(
            ShopName,
            billNumber,
            order.Id,
            order.TableCode,
            order.CreatedAtUtc,
            order.PaidAtUtc,
            order.CompletedAtUtc,
            "unavailable",
            "—",
            "—",
            "",
            "",
            snapshotTotal,
            lines);
    }

    private static (string Status, string LabelVi, string LabelEn) ResolvePaymentLabels(OrderStatus status) =>
        status switch
        {
            OrderStatus.Paid or OrderStatus.InProgress or OrderStatus.FinishingTouches or OrderStatus.Ready =>
                ("paid", "Đã thanh toán", "Paid"),
            OrderStatus.Completed =>
                ("completed", "Đã thanh toán", "Paid"),
            _ => ("unavailable", "—", "—")
        };

    public static bool CanExposeBillToGuest(OrderStatus status) =>
        status is OrderStatus.Paid
            or OrderStatus.InProgress
            or OrderStatus.FinishingTouches
            or OrderStatus.Ready
            or OrderStatus.Completed;
}

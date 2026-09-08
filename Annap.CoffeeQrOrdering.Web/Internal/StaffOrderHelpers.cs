using System.Security.Claims;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Security;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class StaffOrderBoardNotes
{
    public static string? Format(Order o)
    {
        var parts = o.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.CustomerNote))
            .Select(i => $"{(i.MenuItemName ?? i.MenuItem?.Name ?? "—")}: {i.CustomerNote!.Trim()}")
            .ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}

internal static class StaffOrderPacingHelper
{
    public static string Resolve(Order o)
    {
        var ageMins = (DateTimeOffset.UtcNow - o.CreatedAtUtc).TotalMinutes;
        return o.Status switch
        {
            OrderStatus.Submitted when ageMins > 6 => "watch",
            OrderStatus.Paid when ageMins > 10 => "watch",
            OrderStatus.InProgress when ageMins > 12 => "watch",
            OrderStatus.FinishingTouches when ageMins > 8 => "watch",
            OrderStatus.Ready when ageMins > 5 => "brisk",
            OrderStatus.Ready => "steady",
            _ when ageMins > 22 => "watch",
            _ => "steady"
        };
    }
}

/// <summary>Checkout workflow board columns.</summary>
internal static class StaffOrderBoardColumnHelper
{
    public const string Submitted = "submitted";
    public const string Paid = "paid";
    public const string Completed = "completed";

    public static string ToColumn(OrderStatus status) => status switch
    {
        OrderStatus.Draft or OrderStatus.Submitted => Submitted,
        OrderStatus.Paid or OrderStatus.InProgress or OrderStatus.FinishingTouches or OrderStatus.Ready => Paid,
        OrderStatus.Completed => Completed,
        OrderStatus.Cancelled => "cancelled",
        _ => Submitted
    };

    public static bool IsAwaitingPayment(OrderStatus status) =>
        status is OrderStatus.Draft or OrderStatus.Submitted;

    public static bool IsPaidForPrep(OrderStatus status) =>
        status is OrderStatus.Paid
            or OrderStatus.InProgress
            or OrderStatus.FinishingTouches
            or OrderStatus.Ready;

    public static bool CanMarkPaid(OrderStatus status) =>
        status is OrderStatus.Draft or OrderStatus.Submitted;

    public static bool CanComplete(OrderStatus status) =>
        status is OrderStatus.Paid
            or OrderStatus.InProgress
            or OrderStatus.FinishingTouches
            or OrderStatus.Ready;
}

internal static class StaffOrderStatusHelper
{
    public static string ToStaffStatus(OrderStatus s) => s switch
    {
        OrderStatus.Draft => "submitted",
        OrderStatus.Submitted => "submitted",
        OrderStatus.Paid => "paid",
        OrderStatus.InProgress => "preparing",
        OrderStatus.FinishingTouches => "finishing",
        OrderStatus.Ready => "ready",
        OrderStatus.Completed => "completed",
        OrderStatus.Cancelled => "cancelled",
        _ => "submitted"
    };

    public static OrderStatus ParseStaffStatus(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "submitted" or "pending" => OrderStatus.Submitted,
            "paid" => OrderStatus.Paid,
            "preparing" => OrderStatus.InProgress,
            "finishing" => OrderStatus.FinishingTouches,
            "ready" => OrderStatus.Ready,
            "completed" or "served" => OrderStatus.Completed,
            _ => throw new ArgumentException("Unknown status.")
        };
    }
}

/// <summary>
/// Legacy admin PATCH rules. Payment and completion use dedicated workflow endpoints.
/// </summary>
internal static class LegacyStaffStatusPatchHelper
{
    public static bool IsAllowed(OrderStatus current, OrderStatus next)
    {
        if (current == next)
            return true;

        if (current is OrderStatus.Cancelled or OrderStatus.Completed)
            return false;

        if (current is OrderStatus.Draft or OrderStatus.Submitted)
            return false;

        if (next is OrderStatus.Draft or OrderStatus.Submitted or OrderStatus.Paid or OrderStatus.Completed)
            return false;

        return current is OrderStatus.Paid
                or OrderStatus.InProgress
                or OrderStatus.FinishingTouches
                or OrderStatus.Ready
            && next is OrderStatus.InProgress
                or OrderStatus.FinishingTouches
                or OrderStatus.Ready;
    }

    public static string BlockedMessage(OrderStatus current, OrderStatus next) =>
        current is OrderStatus.Draft or OrderStatus.Submitted
            ? "Awaiting-payment orders must be confirmed with mark-paid."
            : next is OrderStatus.Completed
                ? "Use the complete endpoint to close this order."
                : next is OrderStatus.Paid
                    ? "Use mark-paid to confirm payment."
                    : "This status change is not allowed on the legacy patch endpoint.";
}

/// <summary>
/// Forward-only café floor transitions for PATCH /api/staff/orders/{id}/status (legacy admin path).
/// </summary>
internal static class StaffOrderStatusTransitionHelper
{
    private static int WorkflowRank(OrderStatus status) => status switch
    {
        OrderStatus.Draft => 0,
        OrderStatus.Submitted => 1,
        OrderStatus.Paid => 2,
        OrderStatus.InProgress => 3,
        OrderStatus.FinishingTouches => 4,
        OrderStatus.Ready => 5,
        OrderStatus.Completed => 6,
        _ => -1
    };

    public static bool IsValidTransition(OrderStatus current, OrderStatus next)
    {
        if (current == next)
            return true;

        if (current == OrderStatus.Cancelled)
            return false;

        if (current == OrderStatus.Completed)
            return false;

        var currentRank = WorkflowRank(current);
        var nextRank = WorkflowRank(next);
        if (currentRank < 0 || nextRank < 0)
            return false;

        return nextRank > currentRank;
    }
}

internal static class OrderItemPreparationHelper
{
    public static bool IsItemFullyPrepared(OrderItem item) =>
        item.PreparedQuantity >= item.Quantity;

    public static bool IsOrderFullyPrepared(Order order) =>
        order.Items.Count > 0 && order.Items.All(IsItemFullyPrepared);

    public static (int Done, int Total) CountProgress(Order order)
    {
        var total = order.Items.Sum(i => i.Quantity);
        var done = order.Items.Sum(i => Math.Min(i.PreparedQuantity, i.Quantity));
        return (done, total);
    }

    public static bool CanEditPreparation(OrderStatus status) =>
        StaffOrderBoardColumnHelper.CanComplete(status);

    public static bool SetPreparedQuantity(
        OrderItem item,
        int preparedQuantity,
        string? actor,
        Guid? actorAccountId,
        DateTimeOffset now)
    {
        var qty = Math.Clamp(preparedQuantity, 0, item.Quantity);
        if (qty == item.PreparedQuantity)
            return false;

        item.PreparedQuantity = qty;
        if (qty >= item.Quantity)
        {
            item.PreparedAtUtc = now;
            item.PreparedBy = string.IsNullOrWhiteSpace(actor) ? null : actor.Trim();
            item.PreparedByAccountId = actorAccountId;
        }
        else
        {
            item.PreparedAtUtc = null;
            item.PreparedBy = null;
            item.PreparedByAccountId = null;
        }

        return true;
    }

    public static bool MarkItemFullyPrepared(
        OrderItem item,
        bool prepared,
        string? actor,
        Guid? actorAccountId,
        DateTimeOffset now) =>
        SetPreparedQuantity(item, prepared ? item.Quantity : 0, actor, actorAccountId, now);
}

internal static class StaffAuthorizationHelper
{
    public static bool IsAdmin(ClaimsPrincipal user) => user.IsInRole(StaffRoleNames.Admin);

    public static bool CanMarkPaid(ClaimsPrincipal user) =>
        user.IsInRole(StaffRoleNames.Admin) || user.IsInRole(StaffRoleNames.Checkout);

    public static bool CanComplete(ClaimsPrincipal user) =>
        user.IsInRole(StaffRoleNames.Admin) || user.IsInRole(StaffRoleNames.Barista);

    public static bool CanPrepareItems(ClaimsPrincipal user) =>
        user.IsInRole(StaffRoleNames.Admin) || user.IsInRole(StaffRoleNames.Barista);

    public static bool CanManageBills(ClaimsPrincipal user) => IsAdmin(user);

    public static bool CanViewBill(ClaimsPrincipal user) =>
        CanMarkPaid(user) || CanComplete(user);

    public static bool CanViewStaffBoard(ClaimsPrincipal user) =>
        CanMarkPaid(user) || CanComplete(user);

    public static bool CanCloseShift(ClaimsPrincipal user) =>
        user.IsInRole(StaffRoleNames.Admin) || user.IsInRole(StaffRoleNames.Checkout);

    public static IReadOnlyList<string> Roles(ClaimsPrincipal user)
    {
        var roles = new List<string>(3);
        if (user.IsInRole(StaffRoleNames.Admin)) roles.Add(StaffRoleNames.Admin);
        if (user.IsInRole(StaffRoleNames.Checkout)) roles.Add(StaffRoleNames.Checkout);
        if (user.IsInRole(StaffRoleNames.Barista)) roles.Add(StaffRoleNames.Barista);
        return roles;
    }
}

internal static class CustomerTrackStatusHelper
{
    public static (int step, string key, string titleVi, string lineVi, string titleEn, string lineEn, bool isComplete, bool showBill) Resolve(
        OrderStatus s)
    {
        return s switch
        {
            OrderStatus.Draft or OrderStatus.Submitted => (
                1,
                "awaiting_payment",
                "Đơn đã được gửi",
                "Nhân viên sẽ đến kiểm tra lại đơn và hỗ trợ thanh toán.",
                "Your order has been sent",
                "Staff will come to confirm your order and help with payment.",
                false,
                false),
            OrderStatus.Paid or OrderStatus.InProgress or OrderStatus.FinishingTouches or OrderStatus.Ready => (
                2,
                "paid_preparing",
                "Thanh toán thành công",
                "Quầy đang chuẩn bị món của bạn.",
                "Payment successful",
                "The bar is preparing your order.",
                false,
                true),
            OrderStatus.Completed => (
                3,
                "completed",
                "Đơn đã hoàn thành",
                "Cảm ơn bạn đã ghé Annap.",
                "Order complete",
                "Thank you for visiting Annap.",
                true,
                true),
            OrderStatus.Cancelled => (
                0,
                "cancelled",
                "Cập nhật đơn hàng",
                "",
                "Order update",
                "",
                false,
                false),
            _ => (
                1,
                "awaiting_payment",
                "Đơn đã được gửi",
                "Nhân viên sẽ đến kiểm tra lại đơn và hỗ trợ thanh toán.",
                "Your order has been sent",
                "Staff will come to confirm your order and help with payment.",
                false,
                false)
        };
    }
}

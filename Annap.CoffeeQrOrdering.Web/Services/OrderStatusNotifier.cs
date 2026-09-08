using Annap.CoffeeQrOrdering.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace Annap.CoffeeQrOrdering.Web.Services;

public interface IOrderStatusNotifier
{
    Task NotifyGuestOrderAsync(Guid orderId, DateTimeOffset pulseUtc, CancellationToken cancellationToken = default);

    Task NotifyGuestOrderWorkflowAsync(
        Guid orderId,
        object payload,
        CancellationToken cancellationToken = default);

    Task NotifyStaffBoardAsync(CancellationToken cancellationToken = default);

    Task NotifyStaffBoardWorkflowAsync(object payload, CancellationToken cancellationToken = default);
}

public sealed class OrderStatusNotifier(IHubContext<OrderTrackingHub> hub) : IOrderStatusNotifier
{
    public Task NotifyGuestOrderAsync(Guid orderId, DateTimeOffset pulseUtc, CancellationToken cancellationToken = default) =>
        hub.Clients.Group(OrderTrackingHub.GuestOrderGroup(orderId))
            .SendAsync("orderUpdated", new { atUtc = pulseUtc.ToUnixTimeMilliseconds() }, cancellationToken);

    public Task NotifyGuestOrderWorkflowAsync(
        Guid orderId,
        object payload,
        CancellationToken cancellationToken = default) =>
        hub.Clients.Group(OrderTrackingHub.GuestOrderGroup(orderId))
            .SendAsync("orderUpdated", payload, cancellationToken);

    public Task NotifyStaffBoardAsync(CancellationToken cancellationToken = default) =>
        hub.Clients.Group("staff-board")
            .SendAsync("boardRefresh", new { atUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, cancellationToken);

    public Task NotifyStaffBoardWorkflowAsync(object payload, CancellationToken cancellationToken = default) =>
        hub.Clients.Group("staff-board")
            .SendAsync("boardRefresh", payload, cancellationToken);
}

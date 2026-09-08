using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Hubs;

public sealed class OrderTrackingHub(IServiceScopeFactory scopeFactory, HubConnectionRegistry registry) : Hub
{
    public static string GuestOrderGroup(Guid orderId) => $"guest-order-{orderId:N}";

    public async Task JoinGuestOrder(string orderId, string token)
    {
        if (!Guid.TryParse(orderId, out var oid) || string.IsNullOrWhiteSpace(token))
            throw new HubException("Invalid request.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ok = await db.Orders.AsNoTracking()
            .AnyAsync(o =>
                o.Id == oid &&
                o.GuestSessionToken != null &&
                o.GuestSessionToken == token.Trim());
        if (!ok)
            throw new HubException("Unable to follow this order from here.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GuestOrderGroup(oid));
        Context.Items["annap:hub-guest"] = true;
        registry.GuestJoined();
    }

    public async Task JoinStaffBoard()
    {
        if (Context.User?.Identity?.IsAuthenticated != true)
            throw new HubException("Staff sign-in required.");

        var canJoin = Context.User.IsInRole(StaffRoleNames.Admin)
            || Context.User.IsInRole(StaffRoleNames.Checkout)
            || Context.User.IsInRole(StaffRoleNames.Barista);
        if (!canJoin)
            throw new HubException("Staff sign-in required.");

        await Groups.AddToGroupAsync(Context.ConnectionId, "staff-board");
        Context.Items["annap:hub-staff"] = true;
        registry.StaffJoined();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue("annap:hub-guest", out var g) && g is true)
            registry.GuestLeft();
        if (Context.Items.TryGetValue("annap:hub-staff", out var s) && s is true)
            registry.StaffLeft();
        await base.OnDisconnectedAsync(exception);
    }
}

using System.Data;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class OrderSubmitHelper
{
    internal static string? NormalizeIdempotencyKey(HttpRequest request, string? bodyKey)
    {
        var header = request.Headers["Idempotency-Key"].FirstOrDefault();
        var raw = !string.IsNullOrWhiteSpace(header) ? header : bodyKey;
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        raw = raw.Trim();
        if (raw.Length > 120)
            raw = raw[..120];
        return raw;
    }

    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && pg.SqlState == "23505";

    internal static bool IsSerializationConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException pg && (pg.SqlState == "40001" || pg.SqlState == "40P01");

internal static IResult IdempotentOrderResponse(Order order, string token, bool isReplay)
    {
        var body = new
        {
            order.Id,
            order.TableCode,
            order.Status,
            order.TotalAmount,
            guestSessionToken = token,
            trackUrl = $"/track/{order.Id:D}?token={Uri.EscapeDataString(token)}",
            replay = isReplay
        };
        return isReplay
            ? Results.Json(body, statusCode: StatusCodes.Status200OK)
            : Results.Created($"/api/orders/{order.Id}", body);
    }
}

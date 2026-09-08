using System.Text.Json;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

internal static class BankTransferWebhookEndpoints
{
    internal sealed record DevBankTransferWebhookRequest(
        string? Provider,
        string? TransactionId,
        decimal Amount,
        string? Memo,
        DateTimeOffset? ReceivedAtUtc,
        string? AccountNumber,
        string? BankCode);

    public const string WebhookSecretHeader = "X-Annap-Webhook-Secret";

    public static void MapBankTransferWebhookEndpoints(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return;

        app.MapPost("/api/webhooks/bank-transfer/dev", async (
            HttpContext http,
            DevBankTransferWebhookRequest? body,
            IWebHostEnvironment env,
            IOptions<BankTransferOptions> bankOptions,
            IBankTransferConfirmationService confirmationService,
            CancellationToken ct) =>
        {
            var opts = bankOptions.Value;
            if (!opts.Webhook.DevWebhookEnabled)
                return Results.NotFound();

            if (!IsWebhookAuthorized(http, env, opts.Webhook))
                return Results.Unauthorized();

            if (body is null)
                return Results.BadRequest(new { error = "Request body is required." });

            var rawJson = JsonSerializer.Serialize(body);
            var input = new PaymentConfirmationInput(
                string.IsNullOrWhiteSpace(body.Provider) ? DevBankTransferWebhookParser.ProviderName : body.Provider.Trim(),
                body.TransactionId,
                body.Amount,
                body.Memo ?? "",
                body.ReceivedAtUtc ?? DateTimeOffset.UtcNow,
                body.AccountNumber,
                body.BankCode,
                rawJson);

            var result = await confirmationService.ProcessAsync(input, ct);
            return Results.Ok(MapWebhookResponse(result));
        }).AllowAnonymous();
    }

    internal static bool IsWebhookAuthorized(HttpContext http, IWebHostEnvironment env, BankTransferWebhookOptions webhook)
    {
        return env.IsDevelopment();
    }

    private static object MapWebhookResponse(BankTransferConfirmationResult result) =>
        result.Status switch
        {
            "matched" => new
            {
                status = "matched",
                orderId = result.OrderId,
                billNumber = result.BillNumber,
                amount = result.Amount
            },
            "duplicate" => new
            {
                status = "duplicate",
                matchedOrderId = result.MatchedOrderId,
                reason = result.Reason
            },
            "amount_mismatch" => new
            {
                status = "amount_mismatch",
                orderId = result.OrderId,
                billNumber = result.BillNumber,
                expectedAmount = result.Amount,
                reason = result.Reason
            },
            "memo_missing" => new
            {
                status = "memo_missing",
                reason = result.Reason
            },
            "order_already_paid" => new
            {
                status = "order_already_paid",
                matchedOrderId = result.MatchedOrderId,
                reason = result.Reason
            },
            "conflict" => new
            {
                status = "conflict",
                reason = result.Reason
            },
            _ => new
            {
                status = "unmatched",
                reason = result.Reason ?? "No pending BankTransfer order matched memo and amount."
            }
        };
}

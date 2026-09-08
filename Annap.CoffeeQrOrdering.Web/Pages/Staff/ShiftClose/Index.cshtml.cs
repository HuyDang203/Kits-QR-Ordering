using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Globalization;
using System.Text.Json;

namespace Annap.CoffeeQrOrdering.Web.Pages.Staff.ShiftClose;

[Authorize(Policy = "StaffShiftClose")]
public sealed class IndexModel(IShiftCloseService shiftClose) : PageModel
{
    public const int BillListDefault = 10;

    public ShiftClosePreviewVm Preview { get; private set; } = null!;

    public ShiftClosePreviewVm? ClosedSummary { get; private set; }

    public string? ErrorMessage { get; private set; }

    public CultureInfo Vi { get; } = CultureInfo.GetCultureInfo("vi-VN");

    public string Money(decimal v) => VndMoneyFormatter.Format(v);

    public string FormatLocal(DateTimeOffset utc) => AnnapBusinessTime.FormatLocalDateTime(utc);

    public string FormatClock(DateTimeOffset utc) =>
        $"{AnnapBusinessTime.ToLocal(utc):HH:mm}";

    public string FormatCompactWindow(DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        $"{FormatLocal(startUtc)} → {FormatClock(endUtc)}";

    private static readonly JsonSerializerOptions ShiftCloseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (TempData.TryGetValue("ShiftCloseCopyText", out var copyObj))
            ViewData["ShiftCloseCopyText"] = copyObj?.ToString();

        if (TempData["ShiftCloseSuccess"] is string success && success == "1")
        {
            if (TempData["ShiftClosedSummaryJson"] is string json
                && JsonSerializer.Deserialize<ShiftClosePreviewVm>(json, ShiftCloseJsonOptions) is { } closed)
            {
                ClosedSummary = closed;
                Preview = closed;
            }
            else
            {
                Preview = await shiftClose.BuildPreviewAsync(User, cancellationToken);
            }

            ViewData["Title"] = "Đã kết ca";
            return Page();
        }

        Preview = await shiftClose.BuildPreviewAsync(User, cancellationToken);
        ViewData["Title"] = "Kết ca";
        return Page();
    }

    public async Task<IActionResult> OnPostCloseAsync(CancellationToken cancellationToken)
    {
        var result = await shiftClose.CloseShiftAsync(User, cancellationToken);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage;
            Preview = result.ClosedSummary ?? await shiftClose.BuildPreviewAsync(User, cancellationToken);
            ViewData["Title"] = "Kết ca";
            return Page();
        }

        TempData["ShiftCloseSuccess"] = "1";
        if (result.ClosedSummary is not null)
        {
            TempData["ShiftCloseCopyText"] = shiftClose.BuildCopyText(result.ClosedSummary);
            TempData["ShiftClosedSummaryJson"] = JsonSerializer.Serialize(result.ClosedSummary);
        }

        return RedirectToPage();
    }
}

using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.StaffAccounts;

[Authorize(Policy = "StaffAdmin")]
public sealed class IndexModel(
    IStaffAccountService staffAccounts,
    IStaffCredentialFlashStore credentialFlash) : PageModel
{
    public IReadOnlyList<StaffAccountListItemVm> Accounts { get; private set; } = [];

    public string? StatusMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public StaffCredentialRevealVm? CredentialReveal { get; private set; }

    public Guid? EditId { get; private set; }

    [BindProperty]
    public string CreateUsername { get; set; } = "";

    [BindProperty]
    public string CreateDisplayName { get; set; } = "";

    [BindProperty]
    public string CreatePassword { get; set; } = "";

    [BindProperty]
    public string CreateRole { get; set; } = StaffAccountRoles.EmployeeCheckout;

    [BindProperty]
    public Guid EditAccountId { get; set; }

    [BindProperty]
    public string EditDisplayName { get; set; } = "";

    [BindProperty]
    public string EditRole { get; set; } = StaffAccountRoles.EmployeeCheckout;

    [BindProperty]
    public bool EditIsActive { get; set; } = true;

    [BindProperty]
    public Guid ResetAccountId { get; set; }

    [BindProperty]
    public string ResetPassword { get; set; } = "";

    [BindProperty]
    public Guid ToggleAccountId { get; set; }

    public async Task OnGetAsync(string? msg, string? err, Guid? edit, string? flash, CancellationToken cancellationToken)
    {
        StatusMessage = msg;
        ErrorMessage = err;
        EditId = edit;
        if (!string.IsNullOrWhiteSpace(flash))
            CredentialReveal = credentialFlash.Take(flash);

        Accounts = await staffAccounts.ListAsync(cancellationToken);

        if (edit is { } id)
        {
            var row = Accounts.FirstOrDefault(a => a.Id == id);
            if (row is not null)
            {
                EditAccountId = row.Id;
                EditDisplayName = row.DisplayName;
                EditRole = row.Role;
                EditIsActive = row.IsActive;
            }
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var createdBy = User.FindFirst(StaffClaimTypes.DisplayName)?.Value
            ?? User.Identity?.Name;
        var password = CreatePassword;
        var (account, error) = await staffAccounts.CreateAsync(
            new StaffAccountCreateRequest(CreateUsername, CreateDisplayName, password, CreateRole),
            createdBy,
            cancellationToken);
        if (error is not null)
            return RedirectToPage(new { err = error });

        if (account is not null)
        {
            var token = credentialFlash.Store(new StaffCredentialRevealVm(
                StaffCredentialRevealVm.KindCreate,
                account.Username,
                account.DisplayName,
                password));
            return RedirectToPage(new { flash = token });
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(CancellationToken cancellationToken)
    {
        var (_, error) = await staffAccounts.UpdateAsync(
            EditAccountId,
            new StaffAccountUpdateRequest(EditDisplayName, EditIsActive, EditRole),
            cancellationToken);
        if (error is not null)
            return RedirectToPage(new { err = error, edit = EditAccountId });

        return RedirectToPage(new { msg = "Đã cập nhật tài khoản." });
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(CancellationToken cancellationToken)
    {
        var accounts = await staffAccounts.ListAsync(cancellationToken);
        var row = accounts.FirstOrDefault(a => a.Id == ResetAccountId);
        if (row is null)
            return RedirectToPage(new { err = "Không tìm thấy tài khoản." });

        var newPassword = ResetPassword;
        var (success, error) = await staffAccounts.ResetPasswordAsync(ResetAccountId, newPassword, cancellationToken);
        if (!success)
            return RedirectToPage(new { err = error, edit = ResetAccountId });

        var token = credentialFlash.Store(new StaffCredentialRevealVm(
            StaffCredentialRevealVm.KindReset,
            row.Username,
            row.DisplayName,
            newPassword));

        return RedirectToPage(new { flash = token, edit = ResetAccountId });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(CancellationToken cancellationToken)
    {
        var accounts = await staffAccounts.ListAsync(cancellationToken);
        var row = accounts.FirstOrDefault(a => a.Id == ToggleAccountId);
        if (row is null)
            return RedirectToPage(new { err = "Không tìm thấy tài khoản." });

        var (_, error) = await staffAccounts.UpdateAsync(
            ToggleAccountId,
            new StaffAccountUpdateRequest(row.DisplayName, !row.IsActive),
            cancellationToken);
        if (error is not null)
            return RedirectToPage(new { err = error });

        var msg = row.IsActive ? "Đã khóa tài khoản." : "Đã mở khóa tài khoản.";
        return RedirectToPage(new { msg });
    }
}

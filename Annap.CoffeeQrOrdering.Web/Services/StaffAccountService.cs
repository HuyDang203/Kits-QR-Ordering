using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed record StaffAccountListItemVm(
    Guid Id,
    string Username,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc,
    string? CreatedBy);

public sealed record StaffAccountCreateRequest(
    string Username,
    string DisplayName,
    string Password,
    string Role = StaffAccountRoles.EmployeeCheckout);

public sealed record StaffAccountUpdateRequest(
    string DisplayName,
    bool IsActive,
    string? Role = null);

public sealed record StaffAccountResetPasswordRequest(string NewPassword);

public interface IStaffAccountService
{
    Task<IReadOnlyList<StaffAccountListItemVm>> ListAsync(CancellationToken ct = default);
    Task<(StaffAccount? Account, string? Error)> CreateAsync(
        StaffAccountCreateRequest request,
        string? createdBy,
        CancellationToken ct = default);
    Task<(StaffAccount? Account, string? Error)> UpdateAsync(
        Guid id,
        StaffAccountUpdateRequest request,
        CancellationToken ct = default);
    Task<(bool Success, string? Error)> ResetPasswordAsync(
        Guid id,
        string newPassword,
        CancellationToken ct = default);
    Task<StaffAccount?> AuthenticateAsync(string username, string password, CancellationToken ct = default);
    Task RecordLoginAsync(Guid accountId, CancellationToken ct = default);
}

public sealed class StaffAccountService(AppDbContext db) : IStaffAccountService
{
    public const int MinPasswordLength = 8;

    private readonly PasswordHasher<StaffAccount> _hasher = new();

    public async Task<IReadOnlyList<StaffAccountListItemVm>> ListAsync(CancellationToken ct = default) =>
        await db.StaffAccounts.AsNoTracking()
            .OrderBy(a => a.DisplayName)
            .ThenBy(a => a.Username)
            .Select(a => new StaffAccountListItemVm(
                a.Id,
                a.Username,
                a.DisplayName,
                a.Role,
                a.IsActive,
                a.CreatedAtUtc,
                a.LastLoginAtUtc,
                a.CreatedBy))
            .ToListAsync(ct);

    public async Task<(StaffAccount? Account, string? Error)> CreateAsync(
        StaffAccountCreateRequest request,
        string? createdBy,
        CancellationToken ct = default)
    {
        var username = NormalizeUsername(request.Username);
        var displayName = request.DisplayName?.Trim() ?? "";
        var password = request.Password ?? "";

        if (string.IsNullOrWhiteSpace(username))
            return (null, "Tên đăng nhập là bắt buộc.");
        if (string.IsNullOrWhiteSpace(displayName))
            return (null, "Tên hiển thị là bắt buộc.");
        if (password.Length < MinPasswordLength)
            return (null, $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự.");

        var role = NormalizeRole(request.Role);
        if (role is null)
            return (null, "Vai trò không hợp lệ.");

        if (await db.StaffAccounts.AnyAsync(a => a.Username == username, ct))
            return (null, "Tên đăng nhập đã tồn tại.");

        var account = new StaffAccount
        {
            Username = username,
            DisplayName = displayName,
            Role = role,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? null : createdBy.Trim()
        };
        account.PasswordHash = _hasher.HashPassword(account, password);

        db.StaffAccounts.Add(account);
        await db.SaveChangesAsync(ct);
        return (account, null);
    }

    public async Task<(StaffAccount? Account, string? Error)> UpdateAsync(
        Guid id,
        StaffAccountUpdateRequest request,
        CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return (null, "Không tìm thấy tài khoản.");

        var displayName = request.DisplayName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(displayName))
            return (null, "Tên hiển thị là bắt buộc.");

        account.DisplayName = displayName;
        account.IsActive = request.IsActive;
        if (request.Role is not null)
        {
            var role = NormalizeRole(request.Role);
            if (role is null)
                return (null, "Vai trò không hợp lệ.");
            account.Role = role;
        }
        account.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return (account, null);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        Guid id,
        string newPassword,
        CancellationToken ct = default)
    {
        if (newPassword.Length < MinPasswordLength)
            return (false, $"Mật khẩu phải có ít nhất {MinPasswordLength} ký tự.");

        var account = await db.StaffAccounts.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account is null)
            return (false, "Không tìm thấy tài khoản.");

        account.PasswordHash = _hasher.HashPassword(account, newPassword);
        account.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<StaffAccount?> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var normalized = NormalizeUsername(username);
        if (string.IsNullOrWhiteSpace(normalized) || string.IsNullOrEmpty(password))
            return null;

        var account = await db.StaffAccounts.FirstOrDefaultAsync(a => a.Username == normalized, ct);
        if (account is null || !account.IsActive)
            return null;

        var result = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded
            ? account
            : null;
    }

    public async Task RecordLoginAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await db.StaffAccounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);
        if (account is null)
            return;
        account.LastLoginAtUtc = DateTimeOffset.UtcNow;
        account.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    internal static string NormalizeUsername(string? username) =>
        (username ?? "").Trim().ToLowerInvariant();

    internal static string? NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return StaffAccountRoles.EmployeeCheckout;
        var trimmed = role.Trim();
        return StaffAccountRoles.IsValid(trimmed) ? trimmed : null;
    }
}

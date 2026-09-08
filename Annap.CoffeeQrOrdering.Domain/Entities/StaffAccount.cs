namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Individual employee sign-in for floor checkout (mark-paid).</summary>
public sealed class StaffAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Username { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Role { get; set; } = StaffAccountRoles.EmployeeCheckout;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public string? CreatedBy { get; set; }
}

public static class StaffAccountRoles
{
    public const string EmployeeCheckout = "EmployeeCheckout";
    public const string EmployeeBarista = "EmployeeBarista";

    public static bool IsValid(string? role) =>
        role is EmployeeCheckout or EmployeeBarista;

    public static bool IsBarista(string? role) =>
        string.Equals(role, EmployeeBarista, StringComparison.Ordinal);

    public static string LabelVi(string? role) =>
        IsBarista(role) ? "Pha chế" : "Thu ngân";
}

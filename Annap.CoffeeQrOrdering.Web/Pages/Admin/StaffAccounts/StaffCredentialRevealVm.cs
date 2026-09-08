namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.StaffAccounts;

/// <summary>One-time credential display after create/reset (never persisted).</summary>
public sealed record StaffCredentialRevealVm(
    string Kind,
    string Username,
    string DisplayName,
    string TemporaryPassword)
{
    public const string KindCreate = "create";
    public const string KindReset = "reset";

    public bool IsCreate => Kind == KindCreate;

    public string PanelTitle => IsCreate
        ? "Đã tạo tài khoản nhân viên"
        : "Đã đặt lại mật khẩu";

    public string BuildCopyText()
    {
        var lines = new List<string>
        {
            "Tài khoản nhân viên Kits",
            $"Tên đăng nhập: {Username}"
        };

        if (IsCreate && !string.IsNullOrWhiteSpace(DisplayName))
            lines.Add($"Tên hiển thị: {DisplayName}");

        lines.Add($"Mật khẩu tạm thời: {TemporaryPassword}");
        return string.Join(Environment.NewLine, lines);
    }
}

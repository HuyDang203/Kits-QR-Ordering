using System.Net;

namespace Annap.CoffeeQrOrdering.Tests.Infrastructure;

internal static class StaffLoginTestAssertions
{
    internal const string LoginErrorMessage = "Vui lòng kiểm tra tên đăng nhập và mật khẩu.";

    internal static void AssertLoginFailureHtml(string html)
    {
        var decoded = WebUtility.HtmlDecode(html);
        Assert.Contains(LoginErrorMessage, decoded);
    }
}

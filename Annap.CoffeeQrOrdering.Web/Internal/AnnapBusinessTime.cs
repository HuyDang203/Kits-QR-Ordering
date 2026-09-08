namespace Annap.CoffeeQrOrdering.Web.Internal;

/// <summary>Vietnam business calendar for admin reporting (Asia/Ho_Chi_Minh).</summary>
internal static class AnnapBusinessTime
{
    public static TimeZoneInfo Vietnam { get; } = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>Today's calendar date in Vietnam.</summary>
    public static DateTime TodayLocal =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Vietnam).Date;

    /// <summary>Convert inclusive local dates to UTC [start, endExclusive).</summary>
    public static (DateTimeOffset UtcStart, DateTimeOffset UtcEndExclusive) ToUtcRangeInclusive(
        DateTime localFromInclusive,
        DateTime localToInclusive)
    {
        var from = DateTime.SpecifyKind(localFromInclusive.Date, DateTimeKind.Unspecified);
        var toEnd = DateTime.SpecifyKind(localToInclusive.Date.AddDays(1), DateTimeKind.Unspecified);
        var utcStart = TimeZoneInfo.ConvertTimeToUtc(from, Vietnam);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(toEnd, Vietnam);
        return (new DateTimeOffset(utcStart, TimeSpan.Zero), new DateTimeOffset(utcEnd, TimeSpan.Zero));
    }

    public static DateTime ToLocalDate(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc.UtcDateTime, Vietnam).Date;

    public static DateTimeOffset ToLocal(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, Vietnam);

    public static string FormatLocalDate(DateTime localDate) =>
        localDate.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture);

    public static string FormatLocalDateLong(DateTime localDate) =>
        localDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public static string FormatLocalDateTime(DateTimeOffset utc) =>
        $"{ToLocal(utc):dd/MM/yyyy HH:mm}";
}

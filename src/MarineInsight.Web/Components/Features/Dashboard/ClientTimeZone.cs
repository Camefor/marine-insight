using System.Globalization;

namespace MarineInsight.Web.Components.Features.Dashboard;

public static class ClientTimeZone
{
    public static readonly TimeZoneInfo FallbackZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    private static readonly Dictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Asia/Shanghai"] = "北京时间",
            ["Asia/Hong_Kong"] = "香港时间",
            ["Asia/Macau"] = "澳门时间",
            ["Asia/Taipei"] = "台北时间",
            ["Asia/Singapore"] = "新加坡时间",
            ["Asia/Kuala_Lumpur"] = "吉隆坡时间",
            ["Asia/Bangkok"] = "曼谷时间",
            ["Asia/Tokyo"] = "东京时间",
            ["Asia/Seoul"] = "首尔时间",
            ["Australia/Sydney"] = "悉尼时间",
            ["Europe/London"] = "伦敦时间",
            ["America/New_York"] = "纽约时间",
            ["America/Los_Angeles"] = "洛杉矶时间",
            ["UTC"] = "协调世界时",
            ["Etc/UTC"] = "协调世界时",
        };

    public static TimeZoneInfo Resolve(string? ianaId)
    {
        if (string.IsNullOrWhiteSpace(ianaId))
        {
            return FallbackZone;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(ianaId);
        }
        catch (TimeZoneNotFoundException)
        {
            return FallbackZone;
        }
        catch (InvalidTimeZoneException)
        {
            return FallbackZone;
        }
    }

    public static string BuildDisplayLabel(TimeZoneInfo zone)
    {
        var name = DisplayNames.TryGetValue(zone.Id, out var friendly) ? friendly : zone.Id;
        var offset = zone.GetUtcOffset(DateTimeOffset.UtcNow);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        var duration = offset.Duration();
        var offsetText = duration.Minutes == 0
            ? $"{sign}{duration.Hours}"
            : $"{sign}{duration.Hours}:{duration.Minutes:00}";
        return $"{name}（UTC{offsetText}）";
    }

    public static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified));
    }

    public static DateTimeOffset ToLocal(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone);

    public static string FormatLocal(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone).ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static string FormatLocalTime(DateTimeOffset utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTime(utc, zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static DateTime NextLocalHour(TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
        return new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified).AddHours(1);
    }
}

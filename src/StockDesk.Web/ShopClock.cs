using Microsoft.Extensions.Options;
using StockDesk.Infrastructure.Reports;

namespace StockDesk.Web;

/// <summary>Converts stored UTC times to the shop's local time for display.</summary>
public sealed class ShopClock(TimeProvider time, IOptions<ShopOptions> options)
{
    private TimeSpan Offset => TimeSpan.FromMinutes(options.Value.UtcOffsetMinutes);

    public DateTime ToLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Unspecified) + Offset;

    public DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime + Offset);

    /// <summary>UTC start and end of the shop's current local day.</summary>
    public (DateTime FromUtc, DateTime ToUtc) TodayUtcRange()
    {
        var start = DateTime.SpecifyKind(Today.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);
        return (start, start.AddDays(1));
    }
}

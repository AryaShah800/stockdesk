using StockDesk.Application.Abstractions;

namespace StockDesk.Application.Reports;

public sealed record TrendPoint(DateOnly Date, int Bills, decimal Revenue);

public static class SalesTrend
{
    /// <summary>
    /// One point per calendar day in [from, to]. usp_DailySales returns only days that had sales,
    /// so quiet days are filled with zero to keep the chart axis honest.
    /// </summary>
    public static IReadOnlyList<TrendPoint> Fill(IReadOnlyList<DailySalesRow> rows, DateOnly from, DateOnly to)
    {
        if (to < from)
            throw new ArgumentException("The end date is before the start date.", nameof(to));

        var byDay = rows.ToDictionary(r => DateOnly.FromDateTime(r.SaleDate));
        var points = new List<TrendPoint>(to.DayNumber - from.DayNumber + 1);
        for (var day = from; day <= to; day = day.AddDays(1))
            points.Add(byDay.TryGetValue(day, out var row)
                ? new TrendPoint(day, row.Bills, row.Revenue)
                : new TrendPoint(day, 0, 0m));
        return points;
    }
}

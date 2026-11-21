using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Reports;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

public sealed class HomeController(IUnitOfWork uow, ShopClock clock, IReportService reports) : Controller
{
    private const int TrendDays = 14;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (fromUtc, toUtc) = clock.TodayUtcRange();
        var (bills, revenue) = await uow.Sales.TotalsBetweenAsync(fromUtc, toUtc, ct);
        var today = clock.Today;
        var from = today.AddDays(-(TrendDays - 1));
        return View(new DashboardViewModel
        {
            TrendDays = TrendDays,
            Trend = SalesTrend.Fill(await reports.GetDailySalesAsync(from, today, ct), from, today),
            TopProducts = await reports.GetTopProductsAsync(from, today, 5, ct),
            TodayBills = bills,
            TodayRevenue = revenue,
            LowStockProducts = await uow.Products.CountLowStockAsync(ct),
            OpenAlerts = await uow.LowStockAlerts.ListOpenAsync(ct),
            RecentSales = await uow.Sales.ListRecentAsync(5, ct),
        });
    }

    [AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockDesk.Application.Abstractions;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

public sealed class HomeController(IUnitOfWork uow, ShopClock clock) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var (fromUtc, toUtc) = clock.TodayUtcRange();
        var (bills, revenue) = await uow.Sales.TotalsBetweenAsync(fromUtc, toUtc, ct);
        return View(new DashboardViewModel
        {
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

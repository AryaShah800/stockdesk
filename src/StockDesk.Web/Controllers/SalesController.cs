using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Sales;
using StockDesk.Infrastructure.Reports;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

/// <summary>Billing. Admin and Staff can both sell and print bills.</summary>
public sealed class SalesController(IUnitOfWork uow, SaleService sales, ShopClock clock, IOptions<ShopOptions> shop) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await uow.Sales.ListRecentAsync(50, ct));

    [HttpGet]
    public IActionResult Create() => View(new CreateSaleViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateSaleViewModel model, CancellationToken ct)
    {
        if (model.Lines.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one product.");
        if (!ModelState.IsValid)
            return await RedisplayAsync(model, ct);

        var input = new CreateSaleInput(model.CustomerName,
            model.Lines.Select(l => new SaleItemInput(l.ProductId, l.Quantity)).ToList());
        var result = await sales.CreateAsync(input, User.Identity!.Name!, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return await RedisplayAsync(model, ct);
        }

        return RedirectToAction(nameof(Bill), new { id = result.Value });
    }

    /// <summary>Shows the form again with the chosen products, so the cashier does not start over.</summary>
    private async Task<IActionResult> RedisplayAsync(CreateSaleViewModel model, CancellationToken ct)
    {
        var ids = model.Lines.Select(l => l.ProductId).Where(id => id > 0).Distinct().ToList();
        var products = ids.Count == 0 ? [] : await uow.Products.GetByIdsAsync(ids, ct);
        model.Products = products.ToDictionary(p => p.Id);
        return View(model);
    }

    /// <summary>Printable bill. Uses a minimal layout and print styles.</summary>
    public async Task<IActionResult> Bill(int id, CancellationToken ct)
    {
        var sale = await uow.Sales.GetWithLinesAsync(id, ct);
        if (sale is null)
            return NotFound();

        return View(new BillViewModel
        {
            Sale = sale,
            SoldAtLocal = clock.ToLocal(sale.SoldAtUtc),
            ShopName = shop.Value.Name,
            ShopAddress = shop.Value.Address,
            ShopPhone = shop.Value.Phone,
        });
    }
}

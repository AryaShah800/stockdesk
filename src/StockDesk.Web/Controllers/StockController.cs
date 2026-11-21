using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Products;
using StockDesk.Domain.Entities;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

/// <summary>The stock ledger: every change to a product's quantity, and the ways to add to it.</summary>
public sealed class StockController(IUnitOfWork uow, StockLedgerService ledger) : Controller
{
    private const int ReceiveRows = 6;

    public async Task<IActionResult> Ledger(int? productId, CancellationToken ct)
    {
        Product? product = null;
        IReadOnlyList<StockMovement> movements;
        if (productId is { } id)
        {
            product = await uow.Products.GetByIdAsync(id, ct);
            if (product is null)
                return NotFound();
            movements = await ledger.HistoryForProductAsync(id, ct);
        }
        else
        {
            movements = await ledger.RecentAsync(ct);
        }
        return View(new LedgerViewModel { Product = product, Movements = movements, CanAdjust = User.IsInRole(Application.Common.Roles.Admin) });
    }

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> Adjust(int productId, int change, string? reason, CancellationToken ct)
    {
        var result = await ledger.AdjustAsync(productId, change, reason, User.Identity!.Name!, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? "Stock adjusted." : result.Error;
        return RedirectToAction(nameof(Ledger), new { productId });
    }

    [Authorize(Policy = Policies.AdminOnly), HttpGet]
    public async Task<IActionResult> Receive(CancellationToken ct) =>
        View(await WithListsAsync(new ReceiveViewModel { Lines = Enumerable.Range(0, ReceiveRows).Select(_ => new ReceiveLineModel()).ToList() }, ct));

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> Receive(ReceiveViewModel model, CancellationToken ct)
    {
        var lines = model.Lines.Where(l => l.ProductId != 0 && l.Quantity is > 0).ToList();
        if (ModelState.IsValid)
        {
            var result = await ledger.ReceiveAsync(
                new ReceiveStockInput(model.SupplierId!.Value, model.Reference, lines.Select(l => new ReceiveLineInput(l.ProductId, l.Quantity!.Value)).ToList()),
                User.Identity!.Name!, ct);
            if (result.Succeeded)
            {
                TempData["Message"] = $"Received stock for {result.Value} product(s).";
                return RedirectToAction(nameof(Ledger));
            }
            ModelState.AddModelError(string.Empty, result.Error!);
        }
        while (model.Lines.Count < ReceiveRows)
            model.Lines.Add(new ReceiveLineModel());
        return View(await WithListsAsync(model, ct));
    }

    private async Task<ReceiveViewModel> WithListsAsync(ReceiveViewModel model, CancellationToken ct)
    {
        model.Suppliers = (await uow.Suppliers.ListAsync(ct: ct)).OrderBy(s => s.Name).Select(s => new SelectListItem(s.Name, s.Id.ToString()));
        model.Products = (await uow.Products.ListWithDetailsAsync(false, ct)).Select(p => new SelectListItem($"{p.Name} ({p.Sku}), {p.QuantityInStock} in stock", p.Id.ToString()));
        return model;
    }
}

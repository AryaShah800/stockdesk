using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Products;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

public sealed class ProductsController(IUnitOfWork uow, ProductService products) : Controller
{
    public async Task<IActionResult> Index(bool showInactive, CancellationToken ct)
    {
        ViewBag.ShowInactive = showInactive;
        return View(await uow.Products.ListWithDetailsAsync(showInactive, ct));
    }

    /// <summary>Live search used by jQuery on the product list and the new-sale page.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(string? term, CancellationToken ct) =>
        Json(await products.SearchAsync(term, ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var product = await uow.Products.GetWithDetailsAsync(id, ct);
        return product is null ? NotFound() : View(product);
    }

    [Authorize(Policy = Policies.AdminOnly), HttpGet]
    public async Task<IActionResult> Create(CancellationToken ct) =>
        View("Form", await WithListsAsync(new ProductFormViewModel(), ct));

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Form", await WithListsAsync(model, ct));

        var result = await products.CreateAsync(ToInput(model), ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", await WithListsAsync(model, ct));
        }

        TempData["Message"] = $"Added {model.Name}.";
        return RedirectToAction(nameof(Details), new { id = result.Value });
    }

    [Authorize(Policy = Policies.AdminOnly), HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var p = await uow.Products.GetByIdAsync(id, ct);
        if (p is null)
            return NotFound();

        var model = new ProductFormViewModel
        {
            Id = p.Id, Sku = p.Sku, Name = p.Name, CategoryId = p.CategoryId, SupplierId = p.SupplierId,
            CostPrice = p.CostPrice, UnitPrice = p.UnitPrice, OpeningStock = p.QuantityInStock, ReorderLevel = p.ReorderLevel,
        };
        return View("Form", await WithListsAsync(model, ct));
    }

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        // SKU and stock are fixed after creation, so their fields are not posted back.
        ModelState.Remove(nameof(model.Sku));
        ModelState.Remove(nameof(model.OpeningStock));
        if (!ModelState.IsValid)
            return View("Form", await WithListsAsync(model, ct));

        var result = await products.UpdateAsync(id, ToInput(model), ct);
        if (!result.Succeeded)
        {
            if (result.Error == "Product not found.")
                return NotFound();
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", await WithListsAsync(model, ct));
        }

        TempData["Message"] = "Product saved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> Restock(int id, int quantity, CancellationToken ct)
    {
        var result = await products.RestockAsync(id, quantity, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? $"Added {quantity} to stock." : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        var result = await products.SetActiveAsync(id, active, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
            ? (active ? "Product is on sale again." : "Product deactivated. It no longer appears in search or new sales.")
            : result.Error;
        return RedirectToAction(nameof(Details), new { id });
    }

    private static ProductInput ToInput(ProductFormViewModel m) =>
        new(m.Sku, m.Name, m.CategoryId, m.SupplierId, m.CostPrice, m.UnitPrice, m.OpeningStock, m.ReorderLevel);

    private async Task<ProductFormViewModel> WithListsAsync(ProductFormViewModel model, CancellationToken ct)
    {
        var categories = await uow.Categories.ListAsync(ct: ct);
        var suppliers = await uow.Suppliers.ListAsync(ct: ct);
        model.Categories = categories.OrderBy(c => c.Name).Select(c => new SelectListItem(c.Name, c.Id.ToString()));
        model.Suppliers = suppliers.OrderBy(s => s.Name).Select(s => new SelectListItem(s.Name, s.Id.ToString()));
        return model;
    }
}

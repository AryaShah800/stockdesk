using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Catalog;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public sealed class CategoriesController(IUnitOfWork uow, CatalogService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View((await uow.Categories.ListAsync(ct: ct)).OrderBy(c => c.Name).ToList());

    [HttpGet]
    public IActionResult Create() => View("Form", new CategoryFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CategoryFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Form", model);
        var result = await catalog.CreateCategoryAsync(model.Name, model.Description, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        TempData["Message"] = "Category added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var c = await uow.Categories.GetByIdAsync(id, ct);
        return c is null ? NotFound() : View("Form", new CategoryFormViewModel { Id = c.Id, Name = c.Name, Description = c.Description });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, CategoryFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View("Form", model);
        var result = await catalog.UpdateCategoryAsync(id, model.Name, model.Description, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        TempData["Message"] = "Category saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await catalog.DeleteCategoryAsync(id, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? "Category deleted." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Policy = Policies.AdminOnly)]
public sealed class SuppliersController(IUnitOfWork uow, CatalogService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View((await uow.Suppliers.ListAsync(ct: ct)).OrderBy(s => s.Name).ToList());

    [HttpGet]
    public IActionResult Create() => View("Form", new SupplierFormViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(SupplierFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View("Form", model);
        var result = await catalog.CreateSupplierAsync(model.Name, model.Phone, model.Email, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        TempData["Message"] = "Supplier added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var s = await uow.Suppliers.GetByIdAsync(id, ct);
        return s is null ? NotFound() : View("Form", new SupplierFormViewModel { Id = s.Id, Name = s.Name, Phone = s.Phone, Email = s.Email });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, SupplierFormViewModel model, CancellationToken ct)
    {
        model.Id = id;
        if (!ModelState.IsValid)
            return View("Form", model);
        var result = await catalog.UpdateSupplierAsync(id, model.Name, model.Phone, model.Email, ct);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View("Form", model);
        }
        TempData["Message"] = "Supplier saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await catalog.DeleteSupplierAsync(id, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? "Supplier deleted." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}

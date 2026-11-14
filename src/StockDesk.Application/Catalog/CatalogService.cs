using StockDesk.Application.Abstractions;
using StockDesk.Application.Common;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.Application.Catalog;

/// <summary>Categories and suppliers.</summary>
public sealed class CatalogService(IUnitOfWork uow)
{
    public async Task<Result<int>> CreateCategoryAsync(string name, string? description, CancellationToken ct = default)
    {
        try
        {
            var category = new Category(name, description);
            if (await uow.Categories.AnyAsync(c => c.Name == category.Name, ct))
                return Result<int>.Failure($"A category named {category.Name} already exists.");
            uow.Categories.Add(category);
            await uow.SaveChangesAsync(ct);
            return Result<int>.Success(category.Id);
        }
        catch (DomainException ex)
        {
            return Result<int>.Failure(ex.Message);
        }
        catch (PersistenceException ex)
        {
            return Result<int>.Failure(ex.Message);
        }
    }

    public async Task<Result> UpdateCategoryAsync(int id, string name, string? description, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure("Category not found.");
        try
        {
            category.Update(name, description);
            var newName = category.Name;
            if (await uow.Categories.AnyAsync(c => c.Name == newName && c.Id != id, ct))
                return Result.Failure($"A category named {newName} already exists.");
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (PersistenceException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        var category = await uow.Categories.GetByIdAsync(id, ct);
        if (category is null)
            return Result.Failure("Category not found.");
        if (await uow.Products.AnyAsync(p => p.CategoryId == id, ct))
            return Result.Failure("This category still has products. Move or deactivate them first.");
        try
        {
            uow.Categories.Remove(category);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (PersistenceException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result<int>> CreateSupplierAsync(string name, string? phone, string? email, CancellationToken ct = default)
    {
        try
        {
            var supplier = new Supplier(name, phone, email);
            uow.Suppliers.Add(supplier);
            await uow.SaveChangesAsync(ct);
            return Result<int>.Success(supplier.Id);
        }
        catch (DomainException ex)
        {
            return Result<int>.Failure(ex.Message);
        }
        catch (PersistenceException ex)
        {
            return Result<int>.Failure(ex.Message);
        }
    }

    public async Task<Result> UpdateSupplierAsync(int id, string name, string? phone, string? email, CancellationToken ct = default)
    {
        var supplier = await uow.Suppliers.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result.Failure("Supplier not found.");
        try
        {
            supplier.Update(name, phone, email);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }
        catch (PersistenceException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<Result> DeleteSupplierAsync(int id, CancellationToken ct = default)
    {
        var supplier = await uow.Suppliers.GetByIdAsync(id, ct);
        if (supplier is null)
            return Result.Failure("Supplier not found.");
        if (await uow.Products.AnyAsync(p => p.SupplierId == id, ct))
            return Result.Failure("Products still use this supplier. Change them first.");
        try
        {
            uow.Suppliers.Remove(supplier);
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (PersistenceException ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}

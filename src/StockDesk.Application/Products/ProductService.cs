using StockDesk.Application.Abstractions;
using StockDesk.Application.Common;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.Application.Products;

public sealed record ProductInput(
    string Sku,
    string Name,
    int CategoryId,
    int? SupplierId,
    decimal CostPrice,
    decimal UnitPrice,
    int OpeningStock,
    int ReorderLevel);

public sealed record ProductSearchResult(
    int Id,
    string Sku,
    string Name,
    string Category,
    decimal UnitPrice,
    int QuantityInStock,
    bool IsLowStock);

public sealed class ProductService(IUnitOfWork uow)
{
    public const int MaxSearchResults = 20;

    public async Task<Result<int>> CreateAsync(ProductInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            var product = new Product(input.Sku, input.Name, input.CategoryId, input.SupplierId,
                input.CostPrice, input.UnitPrice, input.OpeningStock, input.ReorderLevel);

            var check = await CheckReferencesAsync(input.CategoryId, input.SupplierId, ct);
            if (check is not null)
                return Result<int>.Failure(check);
            if (await uow.Products.SkuExistsAsync(product.Sku, null, ct))
                return Result<int>.Failure($"SKU {product.Sku} is already used.");

            uow.Products.Add(product);
            await uow.SaveChangesAsync(ct);
            return Result<int>.Success(product.Id);
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

    /// <summary>Updates details. SKU and stock are not editable here: stock changes go through restock and sales.</summary>
    public async Task<Result> UpdateAsync(int id, ProductInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var product = await uow.Products.GetByIdAsync(id, ct);
        if (product is null)
            return Result.Failure("Product not found.");

        try
        {
            var check = await CheckReferencesAsync(input.CategoryId, input.SupplierId, ct);
            if (check is not null)
                return Result.Failure(check);

            product.Update(input.Name, input.CategoryId, input.SupplierId, input.CostPrice, input.UnitPrice, input.ReorderLevel);
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

    public async Task<Result> RestockAsync(int id, int quantity, CancellationToken ct = default)
    {
        var product = await uow.Products.GetByIdAsync(id, ct);
        if (product is null)
            return Result.Failure("Product not found.");

        try
        {
            product.Restock(quantity);
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

    /// <summary>Products are deactivated, never deleted, because old bills refer to them.</summary>
    public async Task<Result> SetActiveAsync(int id, bool active, CancellationToken ct = default)
    {
        var product = await uow.Products.GetByIdAsync(id, ct);
        if (product is null)
            return Result.Failure("Product not found.");

        if (active)
            product.Activate();
        else
            product.Deactivate();

        try
        {
            await uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (PersistenceException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    public async Task<IReadOnlyList<ProductSearchResult>> SearchAsync(string? term, CancellationToken ct = default)
    {
        var trimmed = term?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            return [];
        if (trimmed.Length > Product.NameMaxLength)
            trimmed = trimmed[..Product.NameMaxLength];

        var products = await uow.Products.SearchAsync(trimmed, MaxSearchResults, ct);
        return products
            .Select(p => new ProductSearchResult(p.Id, p.Sku, p.Name, p.Category?.Name ?? string.Empty,
                p.UnitPrice, p.QuantityInStock, p.IsLowStock))
            .ToList();
    }

    private async Task<string?> CheckReferencesAsync(int categoryId, int? supplierId, CancellationToken ct)
    {
        if (!await uow.Categories.AnyAsync(c => c.Id == categoryId, ct))
            return "Category not found.";
        if (supplierId is int sid && !await uow.Suppliers.AnyAsync(s => s.Id == sid, ct))
            return "Supplier not found.";
        return null;
    }
}

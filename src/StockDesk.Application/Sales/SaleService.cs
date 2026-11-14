using StockDesk.Application.Abstractions;
using StockDesk.Application.Common;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.Application.Sales;

public sealed record SaleItemInput(int ProductId, int Quantity);

public sealed record CreateSaleInput(string? CustomerName, IReadOnlyList<SaleItemInput> Items);

public sealed class SaleService(IUnitOfWork uow, TimeProvider clock)
{
    /// <summary>
    /// Records a sale. The database trigger reduces stock and raises low-stock alerts in the same
    /// transaction; if another sale took the stock first, nothing is saved and an error is returned.
    /// </summary>
    public async Task<Result<int>> CreateAsync(CreateSaleInput input, string soldBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var items = input.Items ?? [];
        if (items.Count == 0)
            return Result<int>.Failure("A sale needs at least one item.");
        if (items.Count > Sale.MaxLines)
            return Result<int>.Failure($"A bill can have at most {Sale.MaxLines} different products.");

        var ids = items.Select(i => i.ProductId).Distinct().ToList();
        var products = (await uow.Products.GetByIdsAsync(ids, ct)).ToDictionary(p => p.Id);
        var missing = ids.FirstOrDefault(id => !products.ContainsKey(id));
        if (missing != 0 || ids.Contains(0))
            return Result<int>.Failure("One of the products was not found.");

        try
        {
            var sale = Sale.Create(input.CustomerName, soldBy, clock.GetUtcNow().UtcDateTime,
                items.Select(i => (products[i.ProductId], i.Quantity)));
            uow.Sales.Add(sale);
            await uow.SaveChangesAsync(ct);
            return Result<int>.Success(sale.Id);
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
}

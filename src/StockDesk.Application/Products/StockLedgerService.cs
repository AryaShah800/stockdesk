using StockDesk.Application.Abstractions;
using StockDesk.Application.Common;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.Application.Products;

public sealed record ReceiveLineInput(int ProductId, int Quantity);

public sealed record ReceiveStockInput(int SupplierId, string? Reference, IReadOnlyList<ReceiveLineInput> Lines);

/// <summary>
/// Stock changes other than sales. Every change adds a ledger row in the same save as the new quantity, so the
/// ledger and the product quantity cannot drift apart. Sales are written to the ledger by the database trigger.
/// </summary>
public sealed class StockLedgerService(IUnitOfWork uow, TimeProvider clock)
{
    public const int MaxHistory = 100;
    public const int MaxReceiveLines = 100;

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Corrects stock after a count. A reason is required, because this is how stock goes missing.</summary>
    public async Task<Result> AdjustAsync(int productId, int change, string? reason, string performedBy, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return Result.Failure("Give a reason for the adjustment.");
        var product = await uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return Result.Failure("Product not found.");

        try
        {
            product.Adjust(change);
            uow.StockMovements.Add(new StockMovement(product, MovementType.Adjustment, change, product.QuantityInStock,
                performedBy, Now, reason));
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

    /// <summary>
    /// Books a delivery from one supplier: every line adds stock and one ledger row each, all or nothing.
    /// The same product listed twice is added up.
    /// </summary>
    public async Task<Result<int>> ReceiveAsync(ReceiveStockInput input, string performedBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var lines = (input.Lines ?? []).Where(l => l.Quantity != 0 || l.ProductId != 0).ToList();
        if (lines.Count == 0)
            return Result<int>.Failure("Add at least one product to receive.");
        if (lines.Count > MaxReceiveLines)
            return Result<int>.Failure($"A delivery can have at most {MaxReceiveLines} lines.");
        if (lines.Any(l => l.Quantity <= 0))
            return Result<int>.Failure("Every quantity must be at least 1.");

        var supplier = await uow.Suppliers.GetByIdAsync(input.SupplierId, ct);
        if (supplier is null)
            return Result<int>.Failure("Supplier not found.");

        var totals = lines.GroupBy(l => l.ProductId).ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));
        var products = (await uow.Products.GetByIdsAsync(totals.Keys.ToList(), ct)).ToDictionary(p => p.Id);
        if (totals.Keys.Any(id => !products.ContainsKey(id)))
            return Result<int>.Failure("One of the products was not found.");

        try
        {
            foreach (var (productId, quantity) in totals)
            {
                var product = products[productId];
                product.Restock(quantity);
                uow.StockMovements.Add(new StockMovement(product, MovementType.Receipt, quantity, product.QuantityInStock,
                    performedBy, Now, $"Received from {supplier.Name}", supplier.Id, input.Reference));
            }
            await uow.SaveChangesAsync(ct);
            return Result<int>.Success(totals.Count);
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

    public Task<IReadOnlyList<StockMovement>> HistoryForProductAsync(int productId, CancellationToken ct = default) =>
        uow.StockMovements.ListForProductAsync(productId, MaxHistory, ct);

    public Task<IReadOnlyList<StockMovement>> RecentAsync(CancellationToken ct = default) =>
        uow.StockMovements.ListRecentAsync(MaxHistory, ct);
}

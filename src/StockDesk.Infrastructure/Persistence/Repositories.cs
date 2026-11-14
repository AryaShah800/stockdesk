using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Common;
using StockDesk.Domain.Entities;

namespace StockDesk.Infrastructure.Persistence;

internal class Repository<T>(StockDeskDbContext db) : IRepository<T> where T : Entity
{
    protected StockDeskDbContext Db { get; } = db;
    protected DbSet<T> Set => Db.Set<T>();

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        IQueryable<T> query = Set.AsNoTracking();
        if (predicate is not null)
            query = query.Where(predicate);
        return await query.OrderBy(e => e.Id).ToListAsync(ct);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Set.AnyAsync(predicate, ct);

    public void Add(T entity) => Set.Add(entity);

    public void Remove(T entity) => Set.Remove(entity);
}

internal sealed class ProductRepository(StockDeskDbContext db) : Repository<Product>(db), IProductRepository
{
    public async Task<IReadOnlyList<Product>> ListWithDetailsAsync(bool includeInactive, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Include(p => p.Category).Include(p => p.Supplier).AsQueryable();
        if (!includeInactive)
            query = query.Where(p => p.IsActive);
        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public Task<Product?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Set.AsNoTracking().Include(p => p.Category).Include(p => p.Supplier).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default) =>
        await Set.Where(p => ids.Contains(p.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<Product>> SearchAsync(string term, int take, CancellationToken ct = default)
    {
        var sku = term.ToUpperInvariant();
        return await Set.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.IsActive && (p.Name.Contains(term) || p.Sku.Contains(sku)))
            .OrderByDescending(p => p.Sku == sku)
            .ThenByDescending(p => p.Name.StartsWith(term))
            .ThenBy(p => p.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<bool> SkuExistsAsync(string sku, int? exceptProductId, CancellationToken ct = default) =>
        Set.AnyAsync(p => p.Sku == sku && (exceptProductId == null || p.Id != exceptProductId), ct);

    public Task<int> CountLowStockAsync(CancellationToken ct = default) =>
        Set.CountAsync(p => p.IsActive && p.QuantityInStock <= p.ReorderLevel, ct);
}

internal sealed class SaleRepository(StockDeskDbContext db) : Repository<Sale>(db), ISaleRepository
{
    public Task<Sale?> GetWithLinesAsync(int id, CancellationToken ct = default) =>
        Set.AsNoTracking().Include(s => s.Lines).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<Sale>> ListRecentAsync(int take, CancellationToken ct = default) =>
        await Set.AsNoTracking().Include(s => s.Lines).OrderByDescending(s => s.Id).Take(take).ToListAsync(ct);

    public async Task<(int Bills, decimal Revenue)> TotalsBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        var totals = await Set
            .Where(s => s.SoldAtUtc >= fromUtc && s.SoldAtUtc < toUtc)
            .GroupBy(_ => 1)
            .Select(g => new { Bills = g.Count(), Revenue = g.Sum(s => s.Total) })
            .FirstOrDefaultAsync(ct);
        return totals is null ? (0, 0m) : (totals.Bills, totals.Revenue);
    }
}

internal sealed class LowStockAlertRepository(StockDeskDbContext db) : Repository<LowStockAlert>(db), ILowStockAlertRepository
{
    public async Task<IReadOnlyList<LowStockAlert>> ListOpenAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(a => a.Product)
            .Where(a => a.AcknowledgedAtUtc == null)
            .OrderBy(a => a.RaisedAtUtc)
            .ToListAsync(ct);
}

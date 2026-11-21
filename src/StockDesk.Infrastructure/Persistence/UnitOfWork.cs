using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;

namespace StockDesk.Infrastructure.Persistence;

internal sealed class UnitOfWork(StockDeskDbContext db) : IUnitOfWork
{
    private IRepository<Category>? _categories;
    private IRepository<Supplier>? _suppliers;
    private IProductRepository? _products;
    private ISaleRepository? _sales;
    private ILowStockAlertRepository? _alerts;
    private IStockMovementRepository? _movements;

    public IRepository<Category> Categories => _categories ??= new Repository<Category>(db);
    public IRepository<Supplier> Suppliers => _suppliers ??= new Repository<Supplier>(db);
    public IProductRepository Products => _products ??= new ProductRepository(db);
    public ISaleRepository Sales => _sales ??= new SaleRepository(db);
    public ILowStockAlertRepository LowStockAlerts => _alerts ??= new LowStockAlertRepository(db);
    public IStockMovementRepository StockMovements => _movements ??= new StockMovementRepository(db);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Someone else changed this record at the same time. Reload it and try again.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql)
        {
            throw Translate(sql, ex) ?? (Exception)ex;
        }
    }

    internal static PersistenceException? Translate(SqlException sql, Exception outer) => sql.Number switch
    {
        SqlObjects.InsufficientStockError => new InsufficientStockException(sql.Message, outer),
        2601 or 2627 => new DuplicateValueException("That value is already used by another record.", outer),
        547 => new InUseException("This record is still in use by other records.", outer),
        _ => null,
    };
}

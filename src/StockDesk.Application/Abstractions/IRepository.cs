using System.Linq.Expressions;
using StockDesk.Domain.Common;

namespace StockDesk.Application.Abstractions;

/// <summary>Generic data access for one entity type. Changes are saved through <see cref="IUnitOfWork"/>.</summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<T>> ListAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default);

    void Add(T entity);

    void Remove(T entity);
}

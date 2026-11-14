namespace StockDesk.Application.Abstractions;

/// <summary>Base type for save failures the user can act on. Messages are safe to show.</summary>
public abstract class PersistenceException(string message, Exception? inner) : Exception(message, inner);

public sealed class InsufficientStockException(string message, Exception? inner = null)
    : PersistenceException(message, inner);

public sealed class DuplicateValueException(string message, Exception? inner = null)
    : PersistenceException(message, inner);

public sealed class InUseException(string message, Exception? inner = null)
    : PersistenceException(message, inner);

public sealed class ConcurrencyException(string message, Exception? inner = null)
    : PersistenceException(message, inner);

namespace StockDesk.Domain.Common;

internal static class Guard
{
    public const decimal MaxMoney = 9_999_999_999.99m;

    public static string Required(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainException($"{field} is required.");
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters.");
        return trimmed;
    }

    public static string? Optional(string? value, string field, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (trimmed.Length > maxLength)
            throw new DomainException($"{field} must be at most {maxLength} characters.");
        return trimmed;
    }

    public static decimal Money(decimal value, string field)
    {
        if (value < 0)
            throw new DomainException($"{field} cannot be negative.");
        if (decimal.Round(value, 2) != value)
            throw new DomainException($"{field} can have at most 2 decimal places.");
        if (value > MaxMoney)
            throw new DomainException($"{field} is too large.");
        return value;
    }

    public static int NotNegative(int value, string field)
    {
        if (value < 0)
            throw new DomainException($"{field} cannot be negative.");
        return value;
    }
}

namespace StockDesk.Infrastructure.Persistence;

/// <summary>Names and definitions of the T-SQL objects created by migrations.</summary>
public static class SqlObjects
{
    public const string ReduceStockTrigger = "trg_SaleLines_ReduceStock";
    public const string DailySalesProcedure = "usp_DailySales";
    public const string StockValuationProcedure = "usp_StockValuation";

    /// <summary>Error number thrown by the trigger when a sale would take stock below zero.</summary>
    public const int InsufficientStockError = 50001;

    /// <summary>
    /// Runs after sale lines are inserted, inside the same transaction as the sale.
    /// 1. Takes the sold quantities off stock. The UPDATE locks the product rows, so two tills
    ///    selling the same product queue up instead of both reading the old quantity.
    /// 2. If any product went below zero, THROW rolls back the whole sale.
    /// 3. Raises one low-stock alert per product at or below its reorder level, unless an open one exists.
    /// </summary>
    public const string ReduceStockTriggerSql = """
        CREATE TRIGGER dbo.trg_SaleLines_ReduceStock
        ON dbo.SaleLines
        AFTER INSERT
        AS
        BEGIN
            SET NOCOUNT ON;
            IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;

            UPDATE p
               SET p.QuantityInStock = p.QuantityInStock - q.Qty
              FROM dbo.Products AS p
              JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM inserted GROUP BY ProductId) AS q
                ON q.ProductId = p.Id;

            IF EXISTS (SELECT 1
                         FROM dbo.Products AS p
                        WHERE p.QuantityInStock < 0
                          AND p.Id IN (SELECT ProductId FROM inserted))
                THROW 50001, 'Not enough stock for one or more items. Another sale may have used it first.', 1;

            INSERT INTO dbo.LowStockAlerts (ProductId, QuantityAtAlert, ReorderLevel, RaisedAtUtc)
            SELECT p.Id, p.QuantityInStock, p.ReorderLevel, SYSUTCDATETIME()
              FROM dbo.Products AS p
             WHERE p.Id IN (SELECT ProductId FROM inserted)
               AND p.QuantityInStock <= p.ReorderLevel
               AND NOT EXISTS (SELECT 1
                                 FROM dbo.LowStockAlerts AS a
                                WHERE a.ProductId = p.Id
                                  AND a.AcknowledgedAtUtc IS NULL);
        END
        """;

    /// <summary>
    /// The trigger with ledger rows (replaces <see cref="ReduceStockTriggerSql"/>; the first migration still creates
    /// the original). For every inserted sale line it also writes a StockMovements row with the stock after that line.
    /// The rows live in the same transaction as the sale, so a rolled-back sale leaves no ledger rows.
    /// </summary>
    public const string ReduceStockWithLedgerTriggerSql = """
        CREATE TRIGGER dbo.trg_SaleLines_ReduceStock
        ON dbo.SaleLines
        AFTER INSERT
        AS
        BEGIN
            SET NOCOUNT ON;
            IF NOT EXISTS (SELECT 1 FROM inserted) RETURN;

            UPDATE p
               SET p.QuantityInStock = p.QuantityInStock - q.Qty
              FROM dbo.Products AS p
              JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM inserted GROUP BY ProductId) AS q
                ON q.ProductId = p.Id;

            IF EXISTS (SELECT 1
                         FROM dbo.Products AS p
                        WHERE p.QuantityInStock < 0
                          AND p.Id IN (SELECT ProductId FROM inserted))
                THROW 50001, 'Not enough stock for one or more items. Another sale may have used it first.', 1;

            -- one ledger row per sold line; QuantityAfter is the stock right after that line when lines of the
            -- same product are applied in Id order
            INSERT INTO dbo.StockMovements (ProductId, Type, QuantityChange, QuantityAfter, PerformedBy, OccurredAtUtc, Reason, SaleId)
            SELECT i.ProductId,
                   'Sale',
                   -i.Quantity,
                   p.QuantityInStock + SUM(i.Quantity) OVER (PARTITION BY i.ProductId)
                                     - SUM(i.Quantity) OVER (PARTITION BY i.ProductId ORDER BY i.Id ROWS UNBOUNDED PRECEDING),
                   s.SoldBy,
                   s.SoldAtUtc,
                   'Bill ' + 'SD-' + RIGHT('000000' + CAST(s.Id AS varchar(10)), 6),
                   s.Id
              FROM inserted AS i
              JOIN dbo.Products AS p ON p.Id = i.ProductId
              JOIN dbo.Sales AS s ON s.Id = i.SaleId;

            INSERT INTO dbo.LowStockAlerts (ProductId, QuantityAtAlert, ReorderLevel, RaisedAtUtc)
            SELECT p.Id, p.QuantityInStock, p.ReorderLevel, SYSUTCDATETIME()
              FROM dbo.Products AS p
             WHERE p.Id IN (SELECT ProductId FROM inserted)
               AND p.QuantityInStock <= p.ReorderLevel
               AND NOT EXISTS (SELECT 1
                                 FROM dbo.LowStockAlerts AS a
                                WHERE a.ProductId = p.Id
                                  AND a.AcknowledgedAtUtc IS NULL);
        END
        """;

    /// <summary>
    /// Bills, items and revenue per local calendar day. Sales are stored in UTC; @UtcOffsetMinutes
    /// shifts them to the shop's time zone (330 for India). The date range is turned into UTC bounds
    /// so the SoldAtUtc index can be used.
    /// </summary>
    public const string DailySalesProcedureSql = """
        CREATE PROCEDURE dbo.usp_DailySales
            @From date,
            @To date,
            @UtcOffsetMinutes int = 330
        AS
        BEGIN
            SET NOCOUNT ON;
            DECLARE @FromUtc datetime2 = DATEADD(minute, -@UtcOffsetMinutes, CAST(@From AS datetime2));
            DECLARE @ToUtc datetime2 = DATEADD(minute, -@UtcOffsetMinutes, DATEADD(day, 1, CAST(@To AS datetime2)));

            SELECT CAST(CAST(DATEADD(minute, @UtcOffsetMinutes, s.SoldAtUtc) AS date) AS datetime2) AS SaleDate,
                   COUNT(*) AS Bills,
                   SUM(li.Items) AS ItemsSold,
                   CAST(SUM(s.Total) AS decimal(18, 2)) AS Revenue
              FROM dbo.Sales AS s
             CROSS APPLY (SELECT SUM(l.Quantity) AS Items FROM dbo.SaleLines AS l WHERE l.SaleId = s.Id) AS li
             WHERE s.SoldAtUtc >= @FromUtc
               AND s.SoldAtUtc < @ToUtc
             GROUP BY CAST(DATEADD(minute, @UtcOffsetMinutes, s.SoldAtUtc) AS date)
             ORDER BY SaleDate;
        END
        """;

    /// <summary>Units and value of active stock per category, at cost and at selling price.</summary>
    public const string StockValuationProcedureSql = """
        CREATE PROCEDURE dbo.usp_StockValuation
        AS
        BEGIN
            SET NOCOUNT ON;
            SELECT c.Name AS Category,
                   COUNT(*) AS Products,
                   SUM(p.QuantityInStock) AS Units,
                   CAST(SUM(p.QuantityInStock * p.CostPrice) AS decimal(18, 2)) AS CostValue,
                   CAST(SUM(p.QuantityInStock * p.UnitPrice) AS decimal(18, 2)) AS RetailValue
              FROM dbo.Categories AS c
              JOIN dbo.Products AS p ON p.CategoryId = c.Id
             WHERE p.IsActive = 1
             GROUP BY c.Name
             ORDER BY c.Name;
        END
        """;

    /// <summary>
    /// Wraps DDL in EXEC so it runs in its own batch, which keeps idempotent migration scripts valid
    /// (CREATE TRIGGER and CREATE PROCEDURE must be the first statement in a batch).
    /// </summary>
    public static string Exec(string ddl) => "EXEC(N'" + ddl.Replace("'", "''") + "')";
}

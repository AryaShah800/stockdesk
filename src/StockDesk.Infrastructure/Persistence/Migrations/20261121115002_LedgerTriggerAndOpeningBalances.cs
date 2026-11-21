using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LedgerTriggerAndOpeningBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stock that exists today becomes the opening balance, so the ledger adds up from the start.
            migrationBuilder.Sql(@"INSERT INTO dbo.StockMovements (ProductId, Type, QuantityChange, QuantityAfter, PerformedBy, OccurredAtUtc, Reason)
                SELECT Id, 'Opening', QuantityInStock, QuantityInStock, 'migration', SYSUTCDATETIME(), 'Balance when the ledger started'
                  FROM dbo.Products WHERE QuantityInStock > 0");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_SaleLines_ReduceStock");
            migrationBuilder.Sql(SqlObjects.Exec(SqlObjects.ReduceStockWithLedgerTriggerSql));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_SaleLines_ReduceStock");
            migrationBuilder.Sql(SqlObjects.Exec(SqlObjects.ReduceStockTriggerSql));
            migrationBuilder.Sql("DELETE FROM dbo.StockMovements WHERE PerformedBy = 'migration'");
        }
    }
}

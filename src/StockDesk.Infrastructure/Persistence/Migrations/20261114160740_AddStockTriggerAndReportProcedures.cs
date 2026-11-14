using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockDesk.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTriggerAndReportProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Each statement is wrapped in EXEC so idempotent scripts (IF NOT EXISTS ... BEGIN ... END) stay valid.
            migrationBuilder.Sql(SqlObjects.Exec(SqlObjects.ReduceStockTriggerSql));
            migrationBuilder.Sql(SqlObjects.Exec(SqlObjects.DailySalesProcedureSql));
            migrationBuilder.Sql(SqlObjects.Exec(SqlObjects.StockValuationProcedureSql));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_StockValuation");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.usp_DailySales");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS dbo.trg_SaleLines_ReduceStock");
        }
    }
}

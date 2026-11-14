using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using StockDesk.Application.Abstractions;
using StockDesk.Domain.Entities;

namespace StockDesk.IntegrationTests.Persistence;

public sealed class RepositoryTests : IDisposable
{
    private readonly TestDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Search_matches_name_or_sku_ignores_inactive_and_puts_the_exact_sku_first()
    {
        await using (var db = _database.CreateContext())
        {
            var category = new Category("General");
            db.Add(category);
            await db.SaveChangesAsync();
            var hidden = new Product("TEA-OLD", "Tea old stock", category.Id, null, 1m, 2m, 5, 0);
            hidden.Deactivate();
            db.Products.AddRange(
                new Product("CUP-1", "Tea cup", category.Id, null, 1m, 2m, 5, 0),
                new Product("TEA", "Assam leaves", category.Id, null, 1m, 2m, 5, 0),
                new Product("MUG", "Green tea", category.Id, null, 1m, 2m, 5, 0),
                new Product("PEN", "Pen", category.Id, null, 1m, 2m, 5, 0),
                hidden);
            await db.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var results = await _database.CreateUnitOfWork(read).Products.SearchAsync("tea", 10);

        results.Select(p => p.Sku).Should().Equal("TEA", "CUP-1", "MUG");
        results.Should().OnlyContain(p => p.Category != null);
    }

    [Fact]
    public async Task Search_treats_like_wildcards_as_plain_text()
    {
        await _database.SeedProductsAsync(("A_1", 1m, 1, 0), ("AB1", 1m, 1, 0), ("PCT", 1m, 1, 0));

        await using var read = _database.CreateContext();
        var products = _database.CreateUnitOfWork(read).Products;

        (await products.SearchAsync("A_1", 10)).Select(p => p.Sku).Should().Equal("A_1");
        (await products.SearchAsync("%", 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task Duplicate_sku_from_the_database_becomes_a_DuplicateValueException()
    {
        var existing = (await _database.SeedProductsAsync(("DUP", 1m, 1, 0)))[0];
        await using var db = _database.CreateContext();
        var uow = _database.CreateUnitOfWork(db);
        uow.Products.Add(new Product("dup", "Another", existing.CategoryId, null, 1m, 2m, 0, 0));

        await uow.Invoking(u => u.SaveChangesAsync()).Should().ThrowAsync<DuplicateValueException>();
    }

    [Fact]
    public async Task Deleting_a_category_that_has_products_becomes_an_InUseException()
    {
        var product = (await _database.SeedProductsAsync(("A", 1m, 1, 0)))[0];
        await using var db = _database.CreateContext();
        var uow = _database.CreateUnitOfWork(db);
        var category = await uow.Categories.GetByIdAsync(product.CategoryId);
        uow.Categories.Remove(category!);

        await uow.Invoking(u => u.SaveChangesAsync()).Should().ThrowAsync<InUseException>();
    }

    [Fact]
    public async Task Sale_is_read_back_with_its_lines_and_utc_time()
    {
        var products = await _database.SeedProductsAsync(("A", 10m, 10, 0), ("B", 2.5m, 10, 0));
        var soldAt = new DateTime(2026, 11, 14, 9, 15, 0, DateTimeKind.Utc);
        int id;
        await using (var db = _database.CreateContext())
        {
            var fresh = await db.Products.OrderBy(p => p.Id).ToListAsync();
            var sale = Sale.Create("Ravi", "staff", soldAt, [(fresh[0], 2), (fresh[1], 3)]);
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
            id = sale.Id;
        }

        await using var read = _database.CreateContext();
        var loaded = await _database.CreateUnitOfWork(read).Sales.GetWithLinesAsync(id);

        loaded!.Lines.Should().HaveCount(2);
        loaded.Total.Should().Be(27.5m);
        loaded.SoldAtUtc.Should().Be(soldAt);
        loaded.SoldAtUtc.Kind.Should().Be(DateTimeKind.Utc);
        loaded.BillNumber.Should().Be($"SD-{id:D6}");
    }

    [Fact]
    public async Task Totals_between_counts_bills_and_revenue_in_the_window()
    {
        var product = (await _database.SeedProductsAsync(("A", 10m, 100, 0)))[0];
        await using (var db = _database.CreateContext())
        {
            var fresh = await db.Products.SingleAsync();
            db.Sales.AddRange(
                Sale.Create(null, "s", new DateTime(2026, 11, 14, 1, 0, 0, DateTimeKind.Utc), [(fresh, 1)]),
                Sale.Create(null, "s", new DateTime(2026, 11, 14, 2, 0, 0, DateTimeKind.Utc), [(fresh, 2)]),
                Sale.Create(null, "s", new DateTime(2026, 11, 15, 2, 0, 0, DateTimeKind.Utc), [(fresh, 4)]));
            await db.SaveChangesAsync();
        }

        await using var read = _database.CreateContext();
        var sales = _database.CreateUnitOfWork(read).Sales;

        (await sales.TotalsBetweenAsync(new DateTime(2026, 11, 14, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)))
            .Should().Be((2, 30m));
        (await sales.TotalsBetweenAsync(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)))
            .Should().Be((0, 0m));
    }

    [Fact]
    public async Task The_idempotent_migration_script_builds_an_empty_database_and_can_be_replayed()
    {
        await using var context = _database.CreateContext();
        var script = context.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        await context.Database.EnsureDeletedAsync();
        await context.GetService<IRelationalDatabaseCreator>().CreateAsync();
        await context.Database.OpenConnectionAsync();
        var connection = context.Database.GetDbConnection();
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        for (var run = 0; run < 2; run++)
        {
            foreach (var batch in batches.Where(b => !string.IsNullOrWhiteSpace(b)))
            {
                await using var command = connection.CreateCommand();
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync();
            }
        }

        (await context.Database.GetAppliedMigrationsAsync()).Should().Equal(context.Database.GetMigrations());
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM sys.objects WHERE name IN ('trg_SaleLines_ReduceStock', 'usp_DailySales', 'usp_StockValuation')";
        (await check.ExecuteScalarAsync()).Should().Be(3);
    }
}

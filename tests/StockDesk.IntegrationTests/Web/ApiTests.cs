using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using StockDesk.Domain.Entities;

namespace StockDesk.IntegrationTests.Web;

/// <summary>The versioned REST API: authentication, authorisation, filtering, paging, Swagger.</summary>
public sealed class ApiTests(StockDeskApp app) : IClassFixture<StockDeskApp>
{
    [Theory]
    [InlineData("/api/v1/products")]
    [InlineData("/api/v1/products/1")]
    [InlineData("/api/v1/stock-movements")]
    [InlineData("/api/v1/reports/daily-sales")]
    public async Task Anonymous_calls_get_401_not_a_login_redirect(string url)
    {
        var response = await app.CreateBrowser().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        response.Headers.Location.Should().BeNull();
    }

    [Fact]
    public async Task Staff_get_403_on_the_admin_only_report_but_can_read_products_and_the_ledger()
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        (await staff.GetAsync("/api/v1/reports/daily-sales")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.GetAsync("/api/v1/products")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.GetAsync("/api/v1/stock-movements")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Products_can_be_filtered_and_paged()
    {
        await app.Database.SeedProductsAsync(("API-A1", 10m, 50, 5), ("API-A2", 10m, 50, 5), ("API-A3", 10m, 2, 5), ("API-B1", 10m, 50, 5));
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var all = await GetJsonAsync(admin, "/api/v1/products?search=API-A&pageSize=2&page=1");
        all.GetProperty("totalCount").GetInt32().Should().Be(3);
        all.GetProperty("totalPages").GetInt32().Should().Be(2);
        all.GetProperty("items").GetArrayLength().Should().Be(2);

        var second = await GetJsonAsync(admin, "/api/v1/products?search=API-A&pageSize=2&page=2");
        second.GetProperty("items").GetArrayLength().Should().Be(1);

        var low = await GetJsonAsync(admin, "/api/v1/products?search=API-&lowStock=true");
        low.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("sku").GetString()).Should().Equal("API-A3");
    }

    [Theory]
    [InlineData("/api/v1/products?page=0")]
    [InlineData("/api/v1/products?pageSize=101")]
    [InlineData("/api/v1/stock-movements?page=-1")]
    [InlineData("/api/v1/stock-movements?type=Nonsense")]
    [InlineData("/api/v1/reports/daily-sales?from=2025-02-01&to=2025-01-01")]
    public async Task Bad_parameters_return_400(string url)
    {
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Product_detail_returns_the_product_or_404()
    {
        var product = (await app.Database.SeedProductsAsync(("API-DETAIL", 30m, 7, 1)))[0];
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var detail = await GetJsonAsync(staff, $"/api/v1/products/{product.Id}");
        detail.GetProperty("product").GetProperty("sku").GetString().Should().Be("API-DETAIL");
        detail.GetProperty("product").GetProperty("quantityInStock").GetInt32().Should().Be(7);
        detail.GetProperty("costPrice").GetDecimal().Should().Be(15m);

        (await staff.GetAsync("/api/v1/products/99999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Stock_movements_filter_by_product_and_type()
    {
        var products = await app.Database.SeedProductsAsync(("API-M1", 10m, 10, 0), ("API-M2", 10m, 10, 0));
        await using (var db = app.Database.CreateContext())
        {
            var p0 = db.Products.Single(p => p.Id == products[0].Id);
            var p1 = db.Products.Single(p => p.Id == products[1].Id);
            db.StockMovements.Add(new StockMovement(p0, MovementType.Restock, 5, 15, "tester", DateTime.UtcNow, "restock"));
            db.StockMovements.Add(new StockMovement(p0, MovementType.Adjustment, -1, 14, "tester", DateTime.UtcNow, "damage"));
            db.StockMovements.Add(new StockMovement(p1, MovementType.Restock, 3, 13, "tester", DateTime.UtcNow, "restock"));
            await db.SaveChangesAsync();
        }
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var forProduct = await GetJsonAsync(staff, $"/api/v1/stock-movements?productId={products[0].Id}");
        forProduct.GetProperty("totalCount").GetInt32().Should().Be(2);

        var adjustments = await GetJsonAsync(staff, $"/api/v1/stock-movements?productId={products[0].Id}&type=Adjustment");
        var item = adjustments.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("type").GetString().Should().Be("Adjustment");
        item.GetProperty("quantityChange").GetInt32().Should().Be(-1);
        item.GetProperty("sku").GetString().Should().Be("API-M1");
    }

    [Fact]
    public async Task Admin_reads_daily_sales_for_a_sale_made_through_the_app()
    {
        var product = (await app.Database.SeedProductsAsync(("API-SALE", 40m, 10, 0)))[0];
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        await using (var db = app.Database.CreateContext())
        {
            var fresh = db.Products.Single(p => p.Id == product.Id);
            var sale = Sale.Create(null, "tester", DateTime.UtcNow, [(fresh, 2)]);
            db.Sales.Add(sale);
            await db.SaveChangesAsync();
        }

        var rows = await GetJsonAsync(admin, $"/api/v1/reports/daily-sales?from={DateTime.UtcNow.AddDays(-30):yyyy-MM-dd}&to={DateTime.UtcNow.AddDays(2):yyyy-MM-dd}");
        rows.EnumerateArray().Sum(r => r.GetProperty("itemsSold").GetInt32()).Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Swagger_document_needs_sign_in_and_lists_the_v1_endpoints_with_xml_comments()
    {
        (await app.CreateBrowser().GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.Redirect, "the API description needs a sign-in too");
        var response = await (await app.LoginAsync(StockDeskApp.StaffEmail)).GetAsync("/swagger/v1/swagger.json");
        response.StatusCode.Should().Be(HttpStatusCode.OK, response.Headers.Location?.ToString());
        var json = await response.Content.ReadAsStringAsync();

        json.Should().Contain("/api/v1/products").And.Contain("/api/v1/stock-movements").And.Contain("/api/v1/reports/daily-sales");
        json.Should().Contain("Lists products, filtered and paged");
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK, url);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}

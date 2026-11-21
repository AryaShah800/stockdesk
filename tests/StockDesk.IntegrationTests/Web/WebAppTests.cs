using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Products;

namespace StockDesk.IntegrationTests.Web;

public sealed class WebAppTests(StockDeskApp app) : IClassFixture<StockDeskApp>
{
    [Theory]
    [InlineData("/")]
    [InlineData("/Products")]
    [InlineData("/Sales/Create")]
    [InlineData("/Products/Search?term=pen")]
    [InlineData("/Reports/DailySales")]
    public async Task Anonymous_users_are_sent_to_the_login_page(string url)
    {
        var response = await app.CreateBrowser().GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task Login_page_and_static_files_are_public()
    {
        var client = app.CreateBrowser();

        (await client.GetAsync("/Account/Login")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/js/site.js")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Wrong_password_is_rejected_without_a_cookie()
    {
        var client = app.CreateBrowser();

        var response = await StockDeskApp.PostFormAsync(client, "/Account/Login", "/Account/Login", new()
        {
            ["Email"] = StockDeskApp.AdminEmail,
            ["Password"] = "not-the-password",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Email or password is wrong.");
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        cookies.Should().NotContain(c => c.StartsWith("StockDesk.Auth="));
    }

    [Fact]
    public async Task Login_ignores_a_return_url_to_another_site()
    {
        var client = app.CreateBrowser();

        var response = await StockDeskApp.PostFormAsync(client, "/Account/Login", "/Account/Login", new()
        {
            ["Email"] = StockDeskApp.AdminEmail,
            ["Password"] = StockDeskApp.Password,
            ["ReturnUrl"] = "https://evil.example/phish",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Be("/");
    }

    [Theory]
    [InlineData("/Reports/DailySales")]
    [InlineData("/Reports/StockValuation")]
    [InlineData("/Categories")]
    [InlineData("/Suppliers")]
    [InlineData("/Users")]
    [InlineData("/Products/Create")]
    public async Task Staff_cannot_open_admin_pages(string url)
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var response = await staff.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Products")]
    [InlineData("/Sales")]
    [InlineData("/Sales/Create")]
    [InlineData("/Alerts")]
    public async Task Staff_can_use_the_shop_pages(string url)
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        (await staff.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/Reports/DailySales")]
    [InlineData("/Reports/DailySales?from=2026-11-01&to=2026-11-14")]
    [InlineData("/Reports/StockValuation")]
    [InlineData("/Categories")]
    [InlineData("/Suppliers")]
    [InlineData("/Users")]
    [InlineData("/Products/Create")]
    public async Task Admin_can_open_admin_pages(string url)
    {
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        (await admin.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Posts_without_an_antiforgery_token_are_rejected()
    {
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var response = await admin.PostAsync("/Categories/Create",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "Sneaky" }));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Staff_cannot_post_to_admin_actions()
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var response = await StockDeskApp.PostFormAsync(staff, "/Products", "/Categories/Create", new() { ["Name"] = "Sneaky" });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");
        await using var db = app.Database.CreateContext();
        (await db.Categories.AnyAsync(c => c.Name == "Sneaky")).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_adds_a_category_and_product_then_staff_finds_it_with_live_search()
    {
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);
        (await StockDeskApp.PostFormAsync(admin, "/Categories/Create", "/Categories/Create", new() { ["Name"] = "Search test" }))
            .StatusCode.Should().Be(HttpStatusCode.Redirect);
        int categoryId;
        await using (var db = app.Database.CreateContext())
            categoryId = (await db.Categories.SingleAsync(c => c.Name == "Search test")).Id;

        var created = await StockDeskApp.PostFormAsync(admin, "/Products/Create", "/Products/Create", new()
        {
            ["Sku"] = "zz-search-1", ["Name"] = "Zebra search pencil", ["CategoryId"] = categoryId.ToString(),
            ["CostPrice"] = "4.00", ["UnitPrice"] = "7.50", ["OpeningStock"] = "12", ["ReorderLevel"] = "3",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Redirect);
        created.Headers.Location!.ToString().Should().StartWith("/Products/Details/");

        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);
        var results = await staff.GetFromJsonAsync<List<ProductSearchResult>>("/Products/Search?term=zebra");

        results.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Sku = "ZZ-SEARCH-1", Name = "Zebra search pencil", Category = "Search test", UnitPrice = 7.50m, QuantityInStock = 12,
        });
    }

    [Fact]
    public async Task Duplicate_sku_shows_an_error_on_the_form()
    {
        var product = (await app.Database.SeedProductsAsync(("DUP-WEB", 5m, 1, 0)))[0];
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var response = await StockDeskApp.PostFormAsync(admin, "/Products/Create", "/Products/Create", new()
        {
            ["Sku"] = "dup-web", ["Name"] = "Copy", ["CategoryId"] = product.CategoryId.ToString(),
            ["CostPrice"] = "1", ["UnitPrice"] = "2", ["OpeningStock"] = "0", ["ReorderLevel"] = "0",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("SKU DUP-WEB is already used.");
    }

    [Fact]
    public async Task Staff_makes_a_sale_gets_a_printable_bill_and_stock_goes_down()
    {
        var products = await app.Database.SeedProductsAsync(("BILL-A", 12.50m, 20, 2), ("BILL-B", 99m, 5, 4));
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var response = await StockDeskApp.PostFormAsync(staff, "/Sales/Create", "/Sales/Create", new()
        {
            ["CustomerName"] = "Meera",
            ["Lines[0].ProductId"] = products[0].Id.ToString(), ["Lines[0].Quantity"] = "4",
            ["Lines[1].ProductId"] = products[1].Id.ToString(), ["Lines[1].Quantity"] = "1",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var billUrl = response.Headers.Location!.ToString();
        billUrl.Should().StartWith("/Sales/Bill/");
        var bill = await staff.GetStringAsync(billUrl);
        bill.Should().Contain("Meera").And.Contain("Product BILL-A").And.Contain("₹149.00").And.Contain("window.print()");
        bill.Should().Contain(StockDeskApp.StaffEmail);

        (await app.Database.StockOfAsync(products[0].Id)).Should().Be(16);
        (await app.Database.StockOfAsync(products[1].Id)).Should().Be(4);

        var alerts = await staff.GetStringAsync("/Alerts");
        alerts.Should().Contain("Product BILL-B", "selling down to the reorder level raises an alert");
    }

    [Fact]
    public async Task Overselling_shows_the_error_and_keeps_the_chosen_lines()
    {
        var product = (await app.Database.SeedProductsAsync(("FEW", 10m, 2, 0)))[0];
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var response = await StockDeskApp.PostFormAsync(staff, "/Sales/Create", "/Sales/Create", new()
        {
            ["Lines[0].ProductId"] = product.Id.ToString(), ["Lines[0].Quantity"] = "3",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("Only 2 of Product FEW in stock.");
        html.Should().Contain($"data-product-id=\"{product.Id}\"");
        (await app.Database.StockOfAsync(product.Id)).Should().Be(2);
    }

    [Fact]
    public async Task A_sale_with_no_lines_is_rejected()
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        var response = await StockDeskApp.PostFormAsync(staff, "/Sales/Create", "/Sales/Create", new() { ["CustomerName"] = "Nobody" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Add at least one product.");
    }

    [Fact]
    public async Task Admin_restocks_and_the_reports_reflect_stock_and_sales()
    {
        var product = (await app.Database.SeedProductsAsync(("RPT-1", 40m, 1, 0)))[0];
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var restock = await StockDeskApp.PostFormAsync(admin, $"/Products/Details/{product.Id}", $"/Products/Restock/{product.Id}",
            new() { ["quantity"] = "9" });
        restock.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await app.Database.StockOfAsync(product.Id)).Should().Be(10);

        var valuation = await admin.GetStringAsync("/Reports/StockValuation");
        valuation.Should().Contain("General");

        var sale = await StockDeskApp.PostFormAsync(admin, "/Sales/Create", "/Sales/Create", new()
        {
            ["Lines[0].ProductId"] = product.Id.ToString(), ["Lines[0].Quantity"] = "2",
        });
        sale.StatusCode.Should().Be(HttpStatusCode.Redirect);

        // Other tests in this class also sell today, so compare with everything sold so far.
        decimal soldToday;
        await using (var db = app.Database.CreateContext())
            soldToday = await db.Sales.SumAsync(s => s.Total);
        var daily = await admin.GetStringAsync("/Reports/DailySales");
        daily.Should().Contain("₹" + soldToday.ToString("N2"));
        soldToday.Should().BeGreaterThanOrEqualTo(80m);
    }

    [Fact]
    public async Task Admin_creates_a_staff_user_who_can_then_log_in()
    {
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var response = await StockDeskApp.PostFormAsync(admin, "/Users/Create", "/Users/Create", new()
        {
            ["Email"] = "cashier2@stockdesk.test", ["Password"] = "Cashier-Pass-42", ["Role"] = "Staff",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var cashier = app.CreateBrowser();
        var login = await StockDeskApp.PostFormAsync(cashier, "/Account/Login", "/Account/Login", new()
        {
            ["Email"] = "cashier2@stockdesk.test", ["Password"] = "Cashier-Pass-42",
        });
        login.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await cashier.GetAsync("/Users")).Headers.Location!.ToString().Should().Contain("AccessDenied");
    }

    [Fact]
    public async Task Bill_for_an_unknown_sale_is_404()
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        (await staff.GetAsync("/Sales/Bill/999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Logout_ends_the_session()
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        (await StockDeskApp.PostFormAsync(staff, "/", "/Account/Logout", new())).StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await staff.GetAsync("/")).Headers.Location!.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task Dashboard_shows_the_sales_trend_and_top_products_to_staff()
    {
        var products = await app.Database.SeedProductsAsync(("CHART-A", 40m, 50, 0));
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);
        (await StockDeskApp.PostFormAsync(staff, "/Sales/Create", "/Sales/Create", new()
        {
            ["Lines[0].ProductId"] = products[0].Id.ToString(), ["Lines[0].Quantity"] = "3",
        })).StatusCode.Should().Be(HttpStatusCode.Redirect);

        var html = await staff.GetStringAsync("/");

        html.Should().Contain("id=\"sales-trend\"").And.Contain("id=\"top-products\"")
            .And.Contain("Product CHART-A").And.Contain("3 units, CHART-A");
    }

    [Theory]
    [InlineData("/Reports/DailySalesCsv", "Date,Bills,Items sold,Revenue")]
    [InlineData("/Reports/StockValuationCsv", "Category,Products,Units,Value at cost,Value at selling price")]
    [InlineData("/Reports/TopProductsCsv", "SKU,Product,Units sold,Revenue")]
    public async Task Admin_downloads_csv_exports_with_a_header_row(string url, string header)
    {
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var response = await admin.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().EndWith(".csv");
        (await response.Content.ReadAsStringAsync()).Should().Contain(header);
    }

    [Fact]
    public async Task Csv_export_contains_the_sale_and_rejects_a_reversed_range()
    {
        var products = await app.Database.SeedProductsAsync(("CSV-A", 20m, 50, 0));
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);
        await StockDeskApp.PostFormAsync(staff, "/Sales/Create", "/Sales/Create", new()
        {
            ["Lines[0].ProductId"] = products[0].Id.ToString(), ["Lines[0].Quantity"] = "2",
        });
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        (await admin.GetStringAsync("/Reports/TopProductsCsv")).Should().Contain("CSV-A,Product CSV-A,2,40.00");
        (await admin.GetAsync("/Reports/DailySalesCsv?from=2026-11-10&to=2026-11-01")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/Reports/DailySalesCsv")]
    [InlineData("/Reports/StockValuationCsv")]
    [InlineData("/Reports/TopProductsCsv")]
    public async Task Staff_and_anonymous_users_cannot_export_reports(string url)
    {
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);
        var staffResponse = await staff.GetAsync(url);
        staffResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        staffResponse.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");

        var anonymous = await app.CreateBrowser().GetAsync(url);
        anonymous.Headers.Location!.ToString().Should().Contain("/Account/Login");
    }

    [Fact]
    public async Task Staff_can_read_the_ledger_but_not_receive_or_adjust_stock()
    {
        var products = await app.Database.SeedProductsAsync(("LEDGER-A", 10m, 20, 0));
        var staff = await app.LoginAsync(StockDeskApp.StaffEmail);

        (await staff.GetAsync("/Stock/Ledger")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.GetAsync($"/Stock/Ledger?productId={products[0].Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await staff.GetAsync("/Stock/Ledger?productId=99999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var receive = await staff.GetAsync("/Stock/Receive");
        receive.StatusCode.Should().Be(HttpStatusCode.Redirect);
        receive.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");

        var adjust = await StockDeskApp.PostFormAsync(staff, "/Stock/Ledger", "/Stock/Adjust", new()
        {
            ["productId"] = products[0].Id.ToString(), ["change"] = "-5", ["reason"] = "Sneaky",
        });
        adjust.Headers.Location!.ToString().Should().Contain("/Account/AccessDenied");
        (await app.Database.StockOfAsync(products[0].Id)).Should().Be(20);
    }

    [Fact]
    public async Task Admin_receives_stock_from_a_supplier_adjusts_it_and_sees_both_in_the_ledger()
    {
        var products = await app.Database.SeedProductsAsync(("LEDGER-B", 10m, 5, 0));
        int supplierId;
        await using (var db = app.Database.CreateContext())
        {
            var supplier = new StockDesk.Domain.Entities.Supplier("Receiving Test Traders");
            db.Suppliers.Add(supplier);
            await db.SaveChangesAsync();
            supplierId = supplier.Id;
        }
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        (await admin.GetAsync("/Stock/Receive")).StatusCode.Should().Be(HttpStatusCode.OK);
        var received = await StockDeskApp.PostFormAsync(admin, "/Stock/Receive", "/Stock/Receive", new()
        {
            ["SupplierId"] = supplierId.ToString(), ["Reference"] = "DN-77",
            ["Lines[0].ProductId"] = products[0].Id.ToString(), ["Lines[0].Quantity"] = "25",
            ["Lines[1].ProductId"] = "0", ["Lines[1].Quantity"] = "",
        });
        received.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var adjusted = await StockDeskApp.PostFormAsync(admin, $"/Stock/Ledger?productId={products[0].Id}", "/Stock/Adjust", new()
        {
            ["productId"] = products[0].Id.ToString(), ["change"] = "-2", ["reason"] = "Damaged carton",
        });
        adjusted.StatusCode.Should().Be(HttpStatusCode.Redirect);

        (await app.Database.StockOfAsync(products[0].Id)).Should().Be(28);
        var page = await admin.GetStringAsync($"/Stock/Ledger?productId={products[0].Id}");
        page.Should().Contain("Received from Receiving Test Traders").And.Contain("DN-77").And.Contain("Damaged carton")
            .And.Contain("&#x2B;25").And.Contain("-2");
    }

    [Fact]
    public async Task Receiving_with_no_supplier_or_no_lines_shows_an_error_and_changes_nothing()
    {
        var products = await app.Database.SeedProductsAsync(("LEDGER-C", 10m, 5, 0));
        var admin = await app.LoginAsync(StockDeskApp.AdminEmail);

        var response = await StockDeskApp.PostFormAsync(admin, "/Stock/Receive", "/Stock/Receive", new()
        {
            ["Lines[0].ProductId"] = products[0].Id.ToString(), ["Lines[0].Quantity"] = "9",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Choose the supplier.");
        (await app.Database.StockOfAsync(products[0].Id)).Should().Be(5);
    }
}

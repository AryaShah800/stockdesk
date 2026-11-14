using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StockDesk.Application.Abstractions;
using StockDesk.Application.Alerts;
using StockDesk.Application.Common;
using StockDesk.Web.Models;

namespace StockDesk.Web.Controllers;

[Authorize(Policy = Policies.AdminOnly)]
public sealed class ReportsController(IReportService reports, ShopClock clock) : Controller
{
    public async Task<IActionResult> DailySales(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var end = to ?? clock.Today;
        var start = from ?? end.AddDays(-6);
        var model = new DailySalesViewModel { From = start, To = end };
        if (end < start)
            ModelState.AddModelError(string.Empty, "The end date is before the start date.");
        else if (end.DayNumber - start.DayNumber > 366)
            ModelState.AddModelError(string.Empty, "Choose a range of at most one year.");
        else
            model.Rows = await reports.GetDailySalesAsync(start, end, ct);
        return View(model);
    }

    public async Task<IActionResult> StockValuation(CancellationToken ct) =>
        View(await reports.GetStockValuationAsync(ct));
}

public sealed class AlertsController(IUnitOfWork uow, AlertService alerts) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct) => View(await uow.LowStockAlerts.ListOpenAsync(ct));

    [Authorize(Policy = Policies.AdminOnly), HttpPost]
    public async Task<IActionResult> Acknowledge(int id, CancellationToken ct)
    {
        var result = await alerts.AcknowledgeAsync(id, User.Identity!.Name!, ct);
        TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded ? "Alert acknowledged." : result.Error;
        return RedirectToAction(nameof(Index));
    }
}

[Authorize(Policy = Policies.AdminOnly)]
public sealed class UsersController(UserManager<IdentityUser> users) : Controller
{
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var list = await users.Users.OrderBy(u => u.Email).ToListAsync(ct);
        var rows = new List<UserRow>();
        foreach (var user in list)
        {
            var roles = await users.GetRolesAsync(user);
            rows.Add(new UserRow(user.Email ?? user.UserName ?? "?", roles.ToList(), await users.IsLockedOutAsync(user)));
        }
        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateUserViewModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!Roles.All.Contains(model.Role))
            ModelState.AddModelError(nameof(model.Role), "Choose Admin or Staff.");
        if (!ModelState.IsValid)
            return View(model);

        var user = new IdentityUser { UserName = model.Email, Email = model.Email, EmailConfirmed = true };
        var created = await users.CreateAsync(user, model.Password);
        if (created.Succeeded)
            created = await users.AddToRoleAsync(user, model.Role);
        if (!created.Succeeded)
        {
            foreach (var error in created.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            model.Password = string.Empty;
            return View(model);
        }

        TempData["Message"] = $"Created {model.Role} account {model.Email}.";
        return RedirectToAction(nameof(Index));
    }
}

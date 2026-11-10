using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class StoreCreditController : Controller
{
    private readonly StoreCreditService _scService;
    private readonly AppDbContext _db;

    public StoreCreditController(StoreCreditService scService, AppDbContext db)
    {
        _scService = scService; _db = db;
    }

    [PermissionAuthorize("storecredit.manage")]
    public async Task<IActionResult> Index()
    {
        var accounts = await _db.StoreCreditAccounts
            .Include(sc => sc.Customer)
            .Where(sc => sc.Balance > 0 || sc.TotalIssued > 0)
            .OrderByDescending(sc => sc.Balance)
            .ToListAsync();
        return View(accounts);
    }

    [PermissionAuthorize("storecredit.manage")]
    public async Task<IActionResult> Details(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return NotFound();
        var account = await _scService.GetAsync(customerId);
        ViewBag.Customer = customer;
        return View(account);
    }

    [PermissionAuthorize("storecredit.manage")]
    public async Task<IActionResult> Issue(int customerId)
    {
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer == null) return NotFound();
        ViewBag.Customer = customer;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("storecredit.manage")]
    public async Task<IActionResult> Issue(int customerId, decimal amount, string? notes)
    {
        await _scService.IssueCreditAsync(customerId, amount, null, notes ?? "Manual issue");
        TempData["Success"] = $"${amount:0.00} store credit issued.";
        return RedirectToAction(nameof(Details), new { customerId });
    }

    // JSON endpoint for POS balance lookup
    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> Balance(int customerId)
    {
        var account = await _db.StoreCreditAccounts.FirstOrDefaultAsync(sc => sc.CustomerId == customerId);
        return Json(new { balance = account?.Balance ?? 0 });
    }
}

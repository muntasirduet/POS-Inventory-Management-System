using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class LoyaltyController : Controller
{
    private readonly AppDbContext _db;
    public LoyaltyController(AppDbContext db) => _db = db;

    /// <summary>Full loyalty ledger for a customer.</summary>
    [PermissionAuthorize("customers.view")]
    public async Task<IActionResult> Customer(int id)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();

        var account = await _db.LoyaltyAccounts
            .Include(la => la.Transactions.OrderByDescending(t => t.CreatedAt))
                .ThenInclude(t => t.Reward)
            .FirstOrDefaultAsync(la => la.CustomerId == id);

        ViewBag.Customer = customer;
        return View(account);
    }

    /// <summary>Manual points adjustment (admin only).</summary>
    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("settings.manage")]
    public async Task<IActionResult> Adjust(int customerId, int points, string reason)
    {
        var account = await _db.LoyaltyAccounts.FirstOrDefaultAsync(la => la.CustomerId == customerId);
        if (account == null)
        {
            // Create account if it doesn't exist yet
            account = new LoyaltyAccount { CustomerId = customerId };
            _db.LoyaltyAccounts.Add(account);
            await _db.SaveChangesAsync();
        }

        account.Points = Math.Max(0, account.Points + points);
        _db.LoyaltyTransactions.Add(new LoyaltyTransaction
        {
            LoyaltyAccountId = account.Id,
            Type = "Adjustment",
            Points = points,
            BalanceAfter = account.Points,
            Notes = string.IsNullOrWhiteSpace(reason) ? "Manual adjustment" : reason
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Points adjusted by {(points >= 0 ? "+" : "")}{points}. New balance: {account.Points} pts.";
        return RedirectToAction(nameof(Customer), new { id = customerId });
    }
}

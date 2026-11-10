using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class CustomerController : Controller
{
    private readonly AppDbContext _db;
    public CustomerController(AppDbContext db) => _db = db;

    [PermissionAuthorize("customers.view")]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Customers.Include(c => c.LoyaltyAccount).Include(c => c.CustomerGroup).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Phone != null && c.Phone.Contains(search)) || (c.Email != null && c.Email.Contains(search)));
        ViewBag.Search = search;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    [PermissionAuthorize("customers.view")]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers
            .Include(c => c.LoyaltyAccount).ThenInclude(la => la!.Transactions.OrderByDescending(t => t.CreatedAt).Take(20))
            .Include(c => c.Membership)
            .Include(c => c.Sales.OrderByDescending(s => s.CreatedAt).Take(10)).ThenInclude(s => s.Items)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [PermissionAuthorize("customers.create")]
    public async Task<IActionResult> Create()
    {
        ViewBag.CustomerGroups = await _db.CustomerGroups.OrderBy(g => g.Name).ToListAsync();
        return View(new Customer());
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("customers.create")]
    public async Task<IActionResult> Create(Customer customer)
    {
        ModelState.Remove("LoyaltyAccount"); ModelState.Remove("Membership"); ModelState.Remove("Sales"); ModelState.Remove("CustomerGroup");
        if (!ModelState.IsValid) { ViewBag.CustomerGroups = await _db.CustomerGroups.OrderBy(g => g.Name).ToListAsync(); return View(customer); }
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();
        _db.LoyaltyAccounts.Add(new LoyaltyAccount { CustomerId = customer.Id });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer added.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("customers.edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer == null) return NotFound();
        ViewBag.CustomerGroups = await _db.CustomerGroups.OrderBy(g => g.Name).ToListAsync();
        return View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("customers.edit")]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id) return BadRequest();
        ModelState.Remove("LoyaltyAccount"); ModelState.Remove("Membership"); ModelState.Remove("Sales"); ModelState.Remove("CustomerGroup");
        if (!ModelState.IsValid) { ViewBag.CustomerGroups = await _db.CustomerGroups.OrderBy(g => g.Name).ToListAsync(); return View(customer); }
        _db.Customers.Update(customer);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("customers.delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        if (c != null) { c.IsDeleted = true; c.DeletedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, PermissionAuthorize("customers.view")]
    public async Task<IActionResult> Search(string q)
    {
        var customers = await _db.Customers.Where(c => c.Name.Contains(q) || (c.Phone != null && c.Phone.Contains(q))).Take(10)
            .Select(c => new { c.Id, c.Name, c.Phone, c.Email }).ToListAsync();
        return Json(customers);
    }

    // ── Membership management ──────────────────────────────────────────────

    [PermissionAuthorize("customers.edit")]
    public async Task<IActionResult> EditMembership(int id)
    {
        var customer = await _db.Customers.Include(c => c.Membership).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        ViewBag.Customer = customer;
        return View(customer.Membership ?? new POS.Core.Entities.Membership { CustomerId = id });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("customers.edit")]
    public async Task<IActionResult> SaveMembership(POS.Core.Entities.Membership model)
    {
        ModelState.Remove("Customer");
        if (!ModelState.IsValid)
        {
            ViewBag.Customer = await _db.Customers.FindAsync(model.CustomerId);
            return View("EditMembership", model);
        }

        var existing = await _db.Memberships.FirstOrDefaultAsync(m => m.CustomerId == model.CustomerId);
        if (existing == null)
        {
            model.Id = 0;
            _db.Memberships.Add(model);
        }
        else
        {
            existing.MembershipType = model.MembershipType;
            existing.ExpiresAt = model.ExpiresAt;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Membership updated.";
        return RedirectToAction(nameof(Details), new { id = model.CustomerId });
    }
}

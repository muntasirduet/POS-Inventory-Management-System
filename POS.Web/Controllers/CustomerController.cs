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
        var query = _db.Customers.Include(c => c.LoyaltyAccount).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search) || (c.Phone != null && c.Phone.Contains(search)) || (c.Email != null && c.Email.Contains(search)));
        ViewBag.Search = search;
        return View(await query.OrderBy(c => c.Name).ToListAsync());
    }

    [PermissionAuthorize("customers.view")]
    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers.Include(c => c.LoyaltyAccount).Include(c => c.Membership).Include(c => c.Sales).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null) return NotFound();
        return View(customer);
    }

    [PermissionAuthorize("customers.create")]
    public IActionResult Create() => View(new Customer());

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("customers.create")]
    public async Task<IActionResult> Create(Customer customer)
    {
        ModelState.Remove("LoyaltyAccount"); ModelState.Remove("Membership"); ModelState.Remove("Sales");
        if (!ModelState.IsValid) return View(customer);
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
        return View(customer);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("customers.edit")]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id) return BadRequest();
        ModelState.Remove("LoyaltyAccount"); ModelState.Remove("Membership"); ModelState.Remove("Sales");
        if (!ModelState.IsValid) return View(customer);
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
}

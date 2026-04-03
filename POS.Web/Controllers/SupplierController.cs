using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class SupplierController : Controller
{
    private readonly AppDbContext _db;
    public SupplierController(AppDbContext db) => _db = db;

    [PermissionAuthorize("suppliers.view")]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search) || (s.Email != null && s.Email.Contains(search)) || (s.Phone != null && s.Phone.Contains(search)));
        ViewBag.Search = search;
        return View(await query.OrderBy(s => s.Name).ToListAsync());
    }

    [PermissionAuthorize("suppliers.create")]
    public IActionResult Create() => View(new Supplier());

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("suppliers.create")]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        ModelState.Remove("PurchaseOrders");
        if (!ModelState.IsValid) return View(supplier);
        _db.Suppliers.Add(supplier);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Supplier created.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("suppliers.edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier == null) return NotFound();
        return View(supplier);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("suppliers.edit")]
    public async Task<IActionResult> Edit(int id, Supplier supplier)
    {
        if (id != supplier.Id) return BadRequest();
        ModelState.Remove("PurchaseOrders");
        if (!ModelState.IsValid) return View(supplier);
        _db.Suppliers.Update(supplier);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Supplier updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("suppliers.delete")]
    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _db.Suppliers.FindAsync(id);
        if (supplier != null) { _db.Suppliers.Remove(supplier); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Supplier deleted.";
        return RedirectToAction(nameof(Index));
    }
}

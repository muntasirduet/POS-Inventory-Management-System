using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Core.Enums;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class CouponController : Controller
{
    private readonly AppDbContext _db;
    public CouponController(AppDbContext db) => _db = db;

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Index(string? search)
    {
        var query = _db.Coupons.Include(c => c.Customer).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Code.Contains(search));
        ViewBag.Search = search;
        return View(await query.OrderByDescending(c => c.CreatedAt).ToListAsync());
    }

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
        return View(new Coupon { Code = GenerateCode() });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Create(Coupon model)
    {
        ModelState.Remove("Customer");
        if (!ModelState.IsValid)
        {
            ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }
        _db.Coupons.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Coupon '{model.Code}' created.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Edit(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null) return NotFound();
        ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
        return View(coupon);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Edit(int id, Coupon model)
    {
        if (id != model.Id) return BadRequest();
        ModelState.Remove("Customer");
        if (!ModelState.IsValid)
        {
            ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }
        _db.Coupons.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Coupon updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon != null) { _db.Coupons.Remove(coupon); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Coupon deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> MarkUsed(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon != null) { coupon.IsUsed = true; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    private static string GenerateCode() =>
        "CPN-" + Guid.NewGuid().ToString("N")[..8].ToUpper();
}

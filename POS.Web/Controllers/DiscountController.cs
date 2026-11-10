using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class DiscountController : Controller
{
    private readonly AppDbContext _db;
    public DiscountController(AppDbContext db) => _db = db;

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Index()
    {
        var discounts = await _db.Discounts
            .Include(d => d.Category)
            .OrderByDescending(d => d.IsActive)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync();
        return View(discounts);
    }

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(new Discount { IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Create(Discount model)
    {
        ModelState.Remove("Category");
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }
        _db.Discounts.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Discount '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Edit(int id)
    {
        var discount = await _db.Discounts.FindAsync(id);
        if (discount == null) return NotFound();
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(discount);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Edit(int id, Discount model)
    {
        if (id != model.Id) return BadRequest();
        ModelState.Remove("Category");
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }
        _db.Discounts.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Discount updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Delete(int id)
    {
        var discount = await _db.Discounts.FindAsync(id);
        if (discount != null) { _db.Discounts.Remove(discount); await _db.SaveChangesAsync(); }
        TempData["Success"] = "Discount deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var discount = await _db.Discounts.FindAsync(id);
        if (discount != null) { discount.IsActive = !discount.IsActive; await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }
}

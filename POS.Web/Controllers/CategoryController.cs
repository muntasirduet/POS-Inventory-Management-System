using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner,StoreManager")]
public class CategoryController : Controller
{
    private readonly AppDbContext _db;
    public CategoryController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() => View(await _db.Categories.Include(c => c.ParentCategory).ToListAsync());

    public async Task<IActionResult> Create()
    {
        ViewBag.Parents = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name");
        return View(new Category());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        ModelState.Remove("ParentCategory"); ModelState.Remove("SubCategories"); ModelState.Remove("Products");
        if (!ModelState.IsValid) { ViewBag.Parents = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name"); return View(category); }
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category == null) return NotFound();
        ViewBag.Parents = new SelectList(await _db.Categories.Where(c => c.Id != id).ToListAsync(), "Id", "Name", category.ParentCategoryId);
        return View(category);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id) return BadRequest();
        ModelState.Remove("ParentCategory"); ModelState.Remove("SubCategories"); ModelState.Remove("Products");
        if (!ModelState.IsValid) { ViewBag.Parents = new SelectList(await _db.Categories.ToListAsync(), "Id", "Name"); return View(category); }
        _db.Categories.Update(category);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category != null) { category.IsDeleted = true; category.DeletedAt = DateTime.UtcNow; await _db.SaveChangesAsync(); }
        TempData["Success"] = "Category deleted.";
        return RedirectToAction(nameof(Index));
    }
}

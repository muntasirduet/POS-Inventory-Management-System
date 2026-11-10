using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Core.Entities;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class CustomerGroupController : Controller
{
    private readonly AppDbContext _db;
    public CustomerGroupController(AppDbContext db) => _db = db;

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Index()
        => View(await _db.CustomerGroups.Include(cg => cg.Customers).OrderBy(cg => cg.Name).ToListAsync());

    [PermissionAuthorize("pricing.manage")]
    public IActionResult Create() => View(new CustomerGroup());

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Create(CustomerGroup model)
    {
        ModelState.Remove("Customers"); ModelState.Remove("PriceRules");
        if (!ModelState.IsValid) return View(model);
        _db.CustomerGroups.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer group created.";
        return RedirectToAction(nameof(Index));
    }

    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Edit(int id)
    {
        var group = await _db.CustomerGroups.FindAsync(id);
        if (group == null) return NotFound();
        return View(group);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> Edit(int id, CustomerGroup model)
    {
        if (id != model.Id) return BadRequest();
        ModelState.Remove("Customers"); ModelState.Remove("PriceRules");
        if (!ModelState.IsValid) return View(model);
        _db.CustomerGroups.Update(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Customer group updated.";
        return RedirectToAction(nameof(Index));
    }

    // PriceRules for a group
    [PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> PriceRules(int id)
    {
        var group = await _db.CustomerGroups.Include(cg => cg.PriceRules)
            .ThenInclude(pr => pr.Product)
            .Include(cg => cg.PriceRules).ThenInclude(pr => pr.Category)
            .FirstOrDefaultAsync(cg => cg.Id == id);
        if (group == null) return NotFound();
        ViewBag.Products = await _db.Products.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(group);
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> AddPriceRule(int groupId, string name, int? productId, int? categoryId,
        string discountType, decimal value, int priority, DateTime? startDate, DateTime? endDate)
    {
        _db.PriceRules.Add(new PriceRule
        {
            Name = name, CustomerGroupId = groupId, ProductId = productId, CategoryId = categoryId,
            DiscountType = discountType, Value = value, Priority = priority,
            StartDate = startDate, EndDate = endDate, IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Price rule added.";
        return RedirectToAction(nameof(PriceRules), new { id = groupId });
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("pricing.manage")]
    public async Task<IActionResult> DeletePriceRule(int ruleId, int groupId)
    {
        var rule = await _db.PriceRules.FindAsync(ruleId);
        if (rule != null) { _db.PriceRules.Remove(rule); await _db.SaveChangesAsync(); }
        return RedirectToAction(nameof(PriceRules), new { id = groupId });
    }
}

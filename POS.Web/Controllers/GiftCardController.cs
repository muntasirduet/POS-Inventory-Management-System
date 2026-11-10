using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Web.Authorization;

namespace POS.Web.Controllers;

[Authorize]
public class GiftCardController : Controller
{
    private readonly GiftCardService _giftCardService;
    private readonly AppDbContext _db;

    public GiftCardController(GiftCardService giftCardService, AppDbContext db)
    {
        _giftCardService = giftCardService; _db = db;
    }

    [PermissionAuthorize("giftcards.manage")]
    public async Task<IActionResult> Index()
        => View(await _giftCardService.GetAllAsync());

    [PermissionAuthorize("giftcards.manage")]
    public async Task<IActionResult> Create()
    {
        ViewBag.Customers = await _db.Customers.OrderBy(c => c.Name).ToListAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken, PermissionAuthorize("giftcards.manage")]
    public async Task<IActionResult> Create(decimal value, int? customerId, DateTime? expiresAt)
    {
        var prefix = (await _db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == "GiftCardPrefix"))?.Value ?? "GC";
        var card = await _giftCardService.IssueAsync(value, expiresAt, customerId, prefix);
        TempData["Success"] = $"Gift card {card.Code} issued with value ${value:0.00}.";
        return RedirectToAction(nameof(Details), new { id = card.Id });
    }

    [PermissionAuthorize("giftcards.manage")]
    public async Task<IActionResult> Details(int id)
    {
        var card = await _db.GiftCards.Include(gc => gc.IssuedToCustomer).FirstOrDefaultAsync(gc => gc.Id == id);
        if (card == null) return NotFound();
        return View(card);
    }

    // JSON endpoint for POS terminal
    [HttpGet, PermissionAuthorize("sales.create")]
    public async Task<IActionResult> Validate(string code)
    {
        var card = await _giftCardService.ValidateAsync(code);
        if (card == null) return Json(new { valid = false, message = "Invalid or expired gift card." });
        return Json(new { valid = true, balance = card.Balance, code = card.Code });
    }
}

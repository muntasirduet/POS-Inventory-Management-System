using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Infrastructure.Data;
using POS.Infrastructure.Identity;

namespace POS.Web.Controllers;

[Authorize(Roles = "SuperAdmin,StoreOwner,StoreManager,Cashier")]
public class SaleController : Controller
{
    private readonly SaleService _saleService;
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SaleController(SaleService saleService, AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _saleService = saleService; _db = db; _userManager = userManager;
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await _db.Products.Include(p => p.TaxRate).Where(p => p.IsActive).ToListAsync();
        ViewBag.Customers = await _db.Customers.ToListAsync();
        ViewBag.BranchId = HttpContext.Session.GetInt32("BranchId");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Complete([FromBody] CompleteSaleRequest request)
    {
        try
        {
            var user = await _userManager.GetUserAsync(User);
            var sale = await _saleService.CreateSaleAsync(request.Items, request.Payments, user!.Id, request.CustomerId, request.BranchId, request.Discount, request.CouponCode);
            return Json(new { success = true, saleId = sale.Id });
        }
        catch (Exception ex) { return Json(new { success = false, message = ex.Message }); }
    }

    [HttpPost]
    public async Task<IActionResult> Hold([FromBody] HoldSaleRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        var sale = await _saleService.HoldSaleAsync(request.Items, user!.Id, request.CustomerId, request.BranchId);
        return Json(new { success = true, saleId = sale.Id });
    }

    [HttpGet]
    public async Task<IActionResult> HeldSales()
    {
        var user = await _userManager.GetUserAsync(User);
        var sales = await _saleService.GetOnHoldSalesAsync(user!.Id);
        return Json(sales);
    }

    public async Task<IActionResult> Receipt(int id)
    {
        var sale = await _saleService.GetSaleAsync(id);
        if (sale == null) return NotFound();
        var store = await _db.Stores.FirstOrDefaultAsync();
        var receipt = await _db.Receipts.FirstOrDefaultAsync(r => r.SaleId == id);
        ViewBag.StoreName = store?.Name ?? "POS Store";
        ViewBag.StorePhone = store?.Phone ?? "";
        ViewBag.ReceiptNumber = receipt?.ReceiptNumber ?? "";
        return View(sale);
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? cashierId)
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        var sales = await _saleService.GetSalesAsync(from, to, branchId, cashierId);
        ViewBag.From = from?.ToString("yyyy-MM-dd");
        ViewBag.To = to?.ToString("yyyy-MM-dd");
        return View(sales);
    }

    public async Task<IActionResult> Details(int id)
    {
        var sale = await _saleService.GetSaleAsync(id);
        if (sale == null) return NotFound();
        return View(sale);
    }

    [HttpGet]
    public async Task<IActionResult> ValidateCoupon(string code)
    {
        var coupon = await _db.Coupons.FirstOrDefaultAsync(c => c.Code == code && !c.IsUsed && (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow));
        if (coupon == null) return Json(new { valid = false });
        return Json(new { valid = true, type = coupon.Type.ToString(), value = coupon.Value });
    }
}

public class CompleteSaleRequest
{
    public List<CartItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }
    public decimal Discount { get; set; }
    public string? CouponCode { get; set; }
}

public class HoldSaleRequest
{
    public List<CartItemDto> Items { get; set; } = new();
    public int? CustomerId { get; set; }
    public int? BranchId { get; set; }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS.Application.Services;
using POS.Web.Models;
using System.Diagnostics;

namespace POS.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly DashboardService _dashboardService;
    private readonly ILogger<HomeController> _logger;

    public HomeController(DashboardService dashboardService, ILogger<HomeController> logger)
    {
        _dashboardService = dashboardService;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var branchId = HttpContext.Session.GetInt32("BranchId");
        var model = await _dashboardService.GetDashboardAsync(branchId);
        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

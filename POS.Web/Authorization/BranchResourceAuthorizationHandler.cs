using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using POS.Infrastructure.Identity;

namespace POS.Web.Authorization;

/// <summary>
/// Resource requirement that enforces branch-level scoping:
/// users whose role is StoreManager or Cashier may only access resources
/// belonging to their own assigned branch.
/// </summary>
public class BranchScopeRequirement : IAuthorizationRequirement { }

public class BranchResourceAuthorizationHandler : AuthorizationHandler<BranchScopeRequirement, int?>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public BranchResourceAuthorizationHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BranchScopeRequirement requirement,
        int? resourceBranchId)
    {
        if (context.User.Identity?.IsAuthenticated != true) return;

        // SuperAdmin and StoreOwner have no branch restriction
        if (context.User.IsInRole("SuperAdmin") || context.User.IsInRole("StoreOwner"))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return;

        // No branch restriction for this user if BranchId is not set
        if (user.BranchId == null)
        {
            context.Succeed(requirement);
            return;
        }

        // Restrict access to the user's own branch; null resource branchId means all-branches view
        if (resourceBranchId == null || resourceBranchId == user.BranchId)
            context.Succeed(requirement);
    }
}

using Microsoft.AspNetCore.Authorization;
using POS.Application.Services;

namespace POS.Web.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionAuthorizationHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return;

        // SuperAdmin always passes permission checks
        if (context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return;

        if (await _permissionService.UserHasPermissionAsync(userId, requirement.PermissionName))
            context.Succeed(requirement);
    }
}

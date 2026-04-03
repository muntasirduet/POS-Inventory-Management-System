using Microsoft.AspNetCore.Authentication;
using POS.Application.Services;
using System.Security.Claims;

namespace POS.Web.Authorization;

/// <summary>
/// Enriches the ClaimsPrincipal with one "permission" claim per granted permission so
/// that Razor views can use User.HasClaim("permission", "xyz") for conditional rendering.
/// Claims are injected once per authentication ticket; the handler is idempotent.
/// </summary>
public class PermissionClaimsTransformation : IClaimsTransformation
{
    private const string PermissionClaimType = "permission";
    private readonly IPermissionService _permissionService;

    public PermissionClaimsTransformation(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
            return principal;

        // Avoid adding duplicates on repeated calls within the same request pipeline
        if (principal.HasClaim(c => c.Type == PermissionClaimType))
            return principal;

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return principal;

        var permissions = await _permissionService.GetUserEffectivePermissionsAsync(userId);
        var granted = permissions.Where(p => p.IsGranted).Select(p => p.Name);

        var identity = new ClaimsIdentity();
        foreach (var name in granted)
            identity.AddClaim(new Claim(PermissionClaimType, name));

        principal.AddIdentity(identity);
        return principal;
    }
}

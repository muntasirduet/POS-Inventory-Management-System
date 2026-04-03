using Microsoft.AspNetCore.Authorization;

namespace POS.Web.Authorization;

/// <summary>
/// Convenience attribute: [PermissionAuthorize("products.delete")]
/// Creates an on-the-fly authorization policy named "Permission:products.delete"
/// that is handled by <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
public class PermissionAuthorizeAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "Permission:";

    public PermissionAuthorizeAttribute(string permissionName)
        : base(PolicyPrefix + permissionName)
    {
    }
}

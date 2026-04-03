namespace POS.Core.Entities;

public class UserPermissionOverride
{
    public string UserId { get; set; } = string.Empty;
    public int PermissionId { get; set; }

    /// <summary>When true the permission is explicitly granted; when false it is explicitly denied.</summary>
    public bool IsGranted { get; set; }

    public Permission Permission { get; set; } = null!;
}

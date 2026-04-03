using Microsoft.AspNetCore.Identity;
namespace POS.Infrastructure.Identity;
public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

using System.ComponentModel.DataAnnotations;
namespace POS.Web.Models;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Display(Name = "New password"), MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
    [DataType(DataType.Password), Display(Name = "Confirm new password"), Compare("NewPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CreateUserViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
    [Required]
    public string FullName { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = string.Empty;
    [Required]
    public string Role { get; set; } = string.Empty;
    public int? BranchId { get; set; }
}

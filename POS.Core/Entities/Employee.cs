namespace POS.Core.Entities;
public class Employee : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public DateTime HireDate { get; set; }
    public decimal CommissionPercent { get; set; }
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
}

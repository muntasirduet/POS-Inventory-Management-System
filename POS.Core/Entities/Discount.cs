using POS.Core.Enums;
namespace POS.Core.Entities;
public class Discount : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
}

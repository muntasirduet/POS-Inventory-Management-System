namespace POS.Core.Entities;
public class Reward : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int PointsRequired { get; set; }
    public decimal DiscountValue { get; set; }
    public bool IsActive { get; set; } = true;
}

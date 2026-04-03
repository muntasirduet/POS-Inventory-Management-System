namespace POS.Core.Entities;
public class SaleReturn : BaseEntity
{
    public int SaleId { get; set; }
    public Sale Sale { get; set; } = null!;
    public string RequestedById { get; set; } = string.Empty;
    public string? ApprovedById { get; set; }
    public decimal RefundAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
}

using POS.Core.Enums;
namespace POS.Core.Entities;
public class Sale : BaseEntity
{
    public string SaleNumber { get; set; } = string.Empty;
    public string CashierId { get; set; } = string.Empty;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public SaleStatus Status { get; set; } = SaleStatus.Active;
    public string? Notes { get; set; }
    public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public Receipt? Receipt { get; set; }
    public ICollection<SaleReturn> Returns { get; set; } = new List<SaleReturn>();
}

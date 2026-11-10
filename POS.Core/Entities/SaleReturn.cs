using POS.Core.Enums;
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
    /// <summary>How the refund is issued: Cash, Card, StoreCredit, Exchange</summary>
    public string RefundMethod { get; set; } = "Cash";
    public string? Notes { get; set; }
    public bool RestockItems { get; set; } = true;
    public ICollection<SaleReturnItem> Items { get; set; } = new List<SaleReturnItem>();
}

public class SaleReturnItem
{
    public int Id { get; set; }
    public int SaleReturnId { get; set; }
    public SaleReturn SaleReturn { get; set; } = null!;
    public int SaleItemId { get; set; }
    public SaleItem SaleItem { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int? VariantId { get; set; }
    public ProductVariant? Variant { get; set; }
    /// <summary>Quantity being returned (≤ original qty)</summary>
    public int ReturnQty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => UnitPrice * ReturnQty;
}

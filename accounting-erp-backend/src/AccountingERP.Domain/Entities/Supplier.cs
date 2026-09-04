namespace AccountingERP.Domain.Entities;

public sealed class Supplier
{
    public int SupplierId { get; set; }
    public string SupplierCode { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

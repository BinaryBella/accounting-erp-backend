namespace AccountingERP.Domain.Entities;

public sealed class Account
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = default!;
    public string AccountName { get; set; } = default!;
    public byte AccountTypeId { get; set; }
    public int? ParentAccountId { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Control accounts the posting engine depends on. Renamable, never deactivated or deleted.</summary>
    public bool IsSystem { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

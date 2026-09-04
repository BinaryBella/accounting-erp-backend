namespace AccountingERP.Domain.Entities;

/// <summary>Asset / Liability / Equity / Revenue / Expense. Seeded, never created via the API.</summary>
public sealed class AccountType
{
    public byte AccountTypeId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>"D" or "C" — the side on which this type carries a positive balance.</summary>
    public string NormalBalance { get; set; } = default!;

    /// <summary>1 = Asset/Liability/Equity, 0 = Revenue/Expense. Drives the Trial Balance and P&amp;L.</summary>
    public bool IsBalanceSheet { get; set; }
}

namespace AccountingERP.Domain.Constants;

/// <summary>
/// Logical account names the posting engine resolves through dbo.AccountMapping
/// (seeded in 02_seed.sql). No account <em>code</em> literal ("1100", "4000", …)
/// appears anywhere in C# — renumbering the chart of accounts is a row update, not a redeploy.
/// </summary>
public static class AccountMappingKeys
{
    public const string AccountsReceivable = "AccountsReceivable";
    public const string SalesRevenue = "SalesRevenue";
    public const string TaxPayableOutput = "TaxPayableOutput";
    public const string AccountsPayable = "AccountsPayable";
    public const string SupplierBillDefaultDebit = "SupplierBillDefaultDebit";
    public const string TaxReceivableInput = "TaxReceivableInput";
    public const string RetainedEarnings = "RetainedEarnings";
}

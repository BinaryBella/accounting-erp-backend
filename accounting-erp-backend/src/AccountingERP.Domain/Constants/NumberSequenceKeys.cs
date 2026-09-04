namespace AccountingERP.Domain.Constants;

/// <summary>Keys into dbo.NumberSequence. One allocation per posted document, inside the posting transaction.</summary>
public static class NumberSequenceKeys
{
    public const string Journal = "Journal";
    public const string SalesInvoice = "SalesInvoice";
    public const string SupplierBill = "SupplierBill";
    public const string Receipt = "Receipt";
    public const string Payment = "Payment";
}

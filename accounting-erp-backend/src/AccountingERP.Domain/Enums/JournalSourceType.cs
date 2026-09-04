namespace AccountingERP.Domain.Enums;

/// <summary>What produced a journal entry. Persisted as TINYINT in dbo.JournalEntry.SourceType.</summary>
public enum JournalSourceType : byte
{
    SalesInvoice = 1,
    CustomerReceipt = 2,
    SupplierBill = 3,
    SupplierPayment = 4,
    Manual = 5,
    Opening = 6
}

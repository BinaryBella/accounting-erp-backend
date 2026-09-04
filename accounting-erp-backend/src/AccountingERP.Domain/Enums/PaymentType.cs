namespace AccountingERP.Domain.Enums;

/// <summary>Discriminator on dbo.Payment — receipts and payments share one table (PLAN §2.8).</summary>
public enum PaymentType : byte
{
    CustomerReceipt = 1,
    SupplierPayment = 2
}

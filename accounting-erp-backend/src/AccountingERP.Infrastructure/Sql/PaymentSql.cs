namespace AccountingERP.Infrastructure.Sql;

/// <summary>Customer-receipt (PaymentType = 1) SQL. Supplier payments reuse dbo.Payment with PaymentType = 2.</summary>
internal static class PaymentSql
{
    public const string InsertCustomerReceipt = @"
INSERT INTO dbo.Payment
    (PaymentNumber, PaymentType, PaymentDate, CustomerId, SupplierId, PaymentMethodId, ReferenceNo, Amount, Status)
OUTPUT INSERTED.PaymentId
VALUES
    (@PaymentNumber, 1, @PaymentDate, @CustomerId, NULL, @PaymentMethodId, @ReferenceNo, @Amount, 1);";

    public const string InsertInvoiceAllocation = @"
INSERT INTO dbo.PaymentAllocation (PaymentId, SalesInvoiceId, SupplierBillId, AllocatedAmount)
VALUES (@PaymentId, @SalesInvoiceId, NULL, @AllocatedAmount);";

    public const string LockInvoice = @"
SELECT si.SalesInvoiceId, si.InvoiceNumber, si.Status, si.CustomerId, si.InvoiceDate,
       si.GrandTotal, si.AmountPaid
FROM   dbo.SalesInvoice si WITH (UPDLOCK, ROWLOCK)
WHERE  si.SalesInvoiceId = @SalesInvoiceId;";

    public const string AddInvoicePaidAmount = @"
UPDATE dbo.SalesInvoice
SET    AmountPaid = AmountPaid + @Delta,
       UpdatedAtUtc = SYSUTCDATETIME()
WHERE  SalesInvoiceId = @SalesInvoiceId;";

    public const string MarkPosted = @"
UPDATE dbo.Payment
SET    Status = 2, JournalEntryId = @JournalEntryId, PostedAtUtc = SYSUTCDATETIME()
WHERE  PaymentId = @PaymentId;";

    public const string MarkReversed = @"
UPDATE dbo.Payment SET Status = 3 WHERE PaymentId = @PaymentId;";

    public const string GetForReverse = @"
SELECT p.PaymentId, p.PaymentNumber, p.Status, p.JournalEntryId
FROM   dbo.Payment p
WHERE  p.PaymentId = @PaymentId AND p.PaymentType = 1;

SELECT pa.SalesInvoiceId, pa.AllocatedAmount
FROM   dbo.PaymentAllocation pa
WHERE  pa.PaymentId = @PaymentId AND pa.SalesInvoiceId IS NOT NULL
ORDER BY pa.PaymentAllocationId;";

    public const string GetCustomerReceiptById = @"
SELECT p.PaymentId, p.PaymentNumber, p.CustomerId, c.CustomerCode, c.Name AS CustomerName,
       p.PaymentDate, p.PaymentMethodId, pm.Name AS PaymentMethod, p.ReferenceNo,
       p.Amount, p.Status, p.JournalEntryId, p.PostedAtUtc, p.CreatedAtUtc
FROM   dbo.Payment p
JOIN   dbo.Customer c        ON c.CustomerId       = p.CustomerId
JOIN   dbo.PaymentMethod pm  ON pm.PaymentMethodId = p.PaymentMethodId
WHERE  p.PaymentId = @PaymentId AND p.PaymentType = 1;

SELECT pa.PaymentAllocationId, pa.SalesInvoiceId, si.InvoiceNumber, pa.AllocatedAmount,
       si.GrandTotal AS InvoiceGrandTotal, si.AmountPaid AS InvoiceAmountPaid
FROM   dbo.PaymentAllocation pa
JOIN   dbo.SalesInvoice si ON si.SalesInvoiceId = pa.SalesInvoiceId
WHERE  pa.PaymentId = @PaymentId
ORDER BY pa.PaymentAllocationId;";

    private const string ListFilter = @"
WHERE  p.PaymentType = 1
  AND  (@CustomerId IS NULL OR p.CustomerId  = @CustomerId)
  AND  (@FromDate   IS NULL OR p.PaymentDate >= @FromDate)
  AND  (@ToDate     IS NULL OR p.PaymentDate <= @ToDate)";

    public const string ListCustomerReceipts = $@"
SELECT p.PaymentId, p.PaymentNumber, p.CustomerId, c.Name AS CustomerName,
       p.PaymentDate, pm.Name AS PaymentMethod, p.Amount, p.Status
FROM   dbo.Payment p
JOIN   dbo.Customer c       ON c.CustomerId       = p.CustomerId
JOIN   dbo.PaymentMethod pm ON pm.PaymentMethodId = p.PaymentMethodId
{ListFilter}
ORDER BY p.PaymentDate DESC, p.PaymentId DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.Payment p
{ListFilter};";
}

namespace AccountingERP.Infrastructure.Sql;

internal static class SalesInvoiceSql
{
    public const string InsertHeader = @"
INSERT INTO dbo.SalesInvoice
    (InvoiceNumber, CustomerId, InvoiceDate, DueDate, Status,
     SubTotal, DiscountAmount, TaxAmount, GrandTotal, AmountPaid, Notes)
OUTPUT INSERTED.SalesInvoiceId
VALUES
    (@InvoiceNumber, @CustomerId, @InvoiceDate, @DueDate, 1,
     @SubTotal, @DiscountAmount, @TaxAmount, @GrandTotal, 0, @Notes);";

    public const string InsertLines = @"
INSERT INTO dbo.SalesInvoiceLine
    (SalesInvoiceId, LineNumber, Description, Quantity, UnitPrice, DiscountPercent, TaxRatePercent,
     LineSubTotal, LineDiscount, LineTax, LineTotal, RevenueAccountId)
SELECT @SalesInvoiceId, t.LineNumber, t.Description, t.Quantity, t.UnitPrice, t.DiscountPercent, t.TaxRatePercent,
       t.LineSubTotal, t.LineDiscount, t.LineTax, t.LineTotal, t.RevenueAccountId
FROM   @Lines AS t;";

    public const string DeleteLines = @"
DELETE FROM dbo.SalesInvoiceLine WHERE SalesInvoiceId = @SalesInvoiceId;";

    public const string UpdateHeader = @"
UPDATE dbo.SalesInvoice
SET    CustomerId     = @CustomerId,
       InvoiceDate    = @InvoiceDate,
       DueDate        = @DueDate,
       SubTotal       = @SubTotal,
       DiscountAmount = @DiscountAmount,
       TaxAmount      = @TaxAmount,
       GrandTotal     = @GrandTotal,
       Notes          = @Notes,
       UpdatedAtUtc   = SYSUTCDATETIME()
WHERE  SalesInvoiceId = @SalesInvoiceId;";

    public const string MarkPosted = @"
UPDATE dbo.SalesInvoice
SET    Status         = 2,
       JournalEntryId  = @JournalEntryId,
       PostedAtUtc     = SYSUTCDATETIME(),
       UpdatedAtUtc    = SYSUTCDATETIME()
WHERE  SalesInvoiceId = @SalesInvoiceId;";

    // Status 2 -> 3 only; no financial column changes, so TR_SalesInvoice_LockPosted does not fire.
    public const string MarkReversed = @"
UPDATE dbo.SalesInvoice
SET    Status = 3, UpdatedAtUtc = SYSUTCDATETIME()
WHERE  SalesInvoiceId = @SalesInvoiceId;";

    // FK_SalesInvoiceLine_Invoice has ON DELETE CASCADE, so the lines go with it.
    public const string Delete = @"
DELETE FROM dbo.SalesInvoice WHERE SalesInvoiceId = @SalesInvoiceId;";

    // header + lines + allocations, one round trip
    public const string GetById = @"
SELECT si.SalesInvoiceId, si.InvoiceNumber, si.CustomerId, c.CustomerCode, c.Name AS CustomerName,
       si.InvoiceDate, si.DueDate, si.Status,
       si.SubTotal, si.DiscountAmount, si.TaxAmount, si.GrandTotal, si.AmountPaid, si.Notes,
       si.JournalEntryId, si.PostedAtUtc, si.CreatedAtUtc, si.UpdatedAtUtc
FROM   dbo.SalesInvoice si
JOIN   dbo.Customer c ON c.CustomerId = si.CustomerId
WHERE  si.SalesInvoiceId = @SalesInvoiceId;

SELECT sil.SalesInvoiceLineId, sil.LineNumber, sil.Description, sil.Quantity, sil.UnitPrice,
       sil.DiscountPercent, sil.TaxRatePercent,
       sil.LineSubTotal, sil.LineDiscount, sil.LineTax, sil.LineTotal,
       sil.RevenueAccountId, a.AccountCode AS RevenueAccountCode, a.AccountName AS RevenueAccountName
FROM   dbo.SalesInvoiceLine sil
JOIN   dbo.Account a ON a.AccountId = sil.RevenueAccountId
WHERE  sil.SalesInvoiceId = @SalesInvoiceId
ORDER BY sil.LineNumber;

SELECT pa.PaymentAllocationId, pa.PaymentId, p.PaymentNumber, p.PaymentDate,
       pm.Name AS PaymentMethod, pa.AllocatedAmount
FROM   dbo.PaymentAllocation pa
JOIN   dbo.Payment p        ON p.PaymentId       = pa.PaymentId
JOIN   dbo.PaymentMethod pm ON pm.PaymentMethodId = p.PaymentMethodId
WHERE  pa.SalesInvoiceId = @SalesInvoiceId
ORDER BY p.PaymentDate, pa.PaymentAllocationId;";

    private const string ListFilter = @"
WHERE  (@CustomerId IS NULL OR si.CustomerId  = @CustomerId)
  AND  (@Status     IS NULL OR si.Status      = @Status)
  AND  (@FromDate   IS NULL OR si.InvoiceDate >= @FromDate)
  AND  (@ToDate     IS NULL OR si.InvoiceDate <= @ToDate)";

    public const string List = $@"
SELECT si.SalesInvoiceId, si.InvoiceNumber, si.CustomerId, c.Name AS CustomerName,
       si.InvoiceDate, si.DueDate, si.Status, si.GrandTotal, si.AmountPaid
FROM   dbo.SalesInvoice si
JOIN   dbo.Customer c ON c.CustomerId = si.CustomerId
{ListFilter}
ORDER BY si.InvoiceDate DESC, si.SalesInvoiceId DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.SalesInvoice si
{ListFilter};";

    public const string GetForPost = @"
SELECT si.SalesInvoiceId, si.InvoiceNumber, si.Status, si.CustomerId, c.Name AS CustomerName,
       si.InvoiceDate, si.TaxAmount, si.GrandTotal
FROM   dbo.SalesInvoice si WITH (UPDLOCK, ROWLOCK)
JOIN   dbo.Customer c ON c.CustomerId = si.CustomerId
WHERE  si.SalesInvoiceId = @SalesInvoiceId;

SELECT sil.RevenueAccountId, sil.LineSubTotal, sil.LineDiscount
FROM   dbo.SalesInvoiceLine sil
WHERE  sil.SalesInvoiceId = @SalesInvoiceId
ORDER BY sil.LineNumber;";

    public const string GetGuard = @"
SELECT si.SalesInvoiceId, si.Status, si.JournalEntryId, si.AmountPaid,
       CAST(CASE WHEN EXISTS (
                 SELECT 1
                 FROM   dbo.PaymentAllocation pa
                 JOIN   dbo.Payment p ON p.PaymentId = pa.PaymentId
                 WHERE  pa.SalesInvoiceId = si.SalesInvoiceId
                   AND  p.Status <> 3)   -- ignore allocations from reversed receipts
                 THEN 1 ELSE 0 END AS BIT) AS HasAllocations
FROM   dbo.SalesInvoice si
WHERE  si.SalesInvoiceId = @SalesInvoiceId;";
}

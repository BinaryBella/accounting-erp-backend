namespace AccountingERP.Infrastructure.Sql;

internal static class SupplierBillSql
{
    public const string InsertHeader = @"
INSERT INTO dbo.SupplierBill
    (BillNumber, SupplierId, BillDate, DueDate, Status,
     SubTotal, DiscountAmount, TaxAmount, GrandTotal, AmountPaid, Notes)
OUTPUT INSERTED.SupplierBillId
VALUES
    (@BillNumber, @SupplierId, @BillDate, @DueDate, 1,
     @SubTotal, @DiscountAmount, @TaxAmount, @GrandTotal, 0, @Notes);";

    public const string InsertLines = @"
INSERT INTO dbo.SupplierBillLine
    (SupplierBillId, LineNumber, Description, Quantity, UnitPrice, DiscountPercent, TaxRatePercent,
     LineSubTotal, LineDiscount, LineTax, LineTotal, DebitAccountId)
SELECT @SupplierBillId, t.LineNumber, t.Description, t.Quantity, t.UnitPrice, t.DiscountPercent, t.TaxRatePercent,
       t.LineSubTotal, t.LineDiscount, t.LineTax, t.LineTotal, t.DebitAccountId
FROM   @Lines AS t;";

    public const string DeleteLines = @"
DELETE FROM dbo.SupplierBillLine WHERE SupplierBillId = @SupplierBillId;";

    public const string UpdateHeader = @"
UPDATE dbo.SupplierBill
SET    SupplierId     = @SupplierId,
       BillDate       = @BillDate,
       DueDate        = @DueDate,
       SubTotal       = @SubTotal,
       DiscountAmount = @DiscountAmount,
       TaxAmount      = @TaxAmount,
       GrandTotal     = @GrandTotal,
       Notes          = @Notes,
       UpdatedAtUtc   = SYSUTCDATETIME()
WHERE  SupplierBillId = @SupplierBillId;";

    public const string MarkPosted = @"
UPDATE dbo.SupplierBill
SET    Status         = 2,
       JournalEntryId  = @JournalEntryId,
       PostedAtUtc     = SYSUTCDATETIME(),
       UpdatedAtUtc    = SYSUTCDATETIME()
WHERE  SupplierBillId = @SupplierBillId;";

    // Status 2 -> 3 only; no financial column changes, so TR_SupplierBill_LockPosted does not fire.
    public const string MarkReversed = @"
UPDATE dbo.SupplierBill
SET    Status = 3, UpdatedAtUtc = SYSUTCDATETIME()
WHERE  SupplierBillId = @SupplierBillId;";

    // FK_SupplierBillLine_Bill has ON DELETE CASCADE, so the lines go with it.
    public const string Delete = @"
DELETE FROM dbo.SupplierBill WHERE SupplierBillId = @SupplierBillId;";

    public const string GetById = @"
SELECT sb.SupplierBillId, sb.BillNumber, sb.SupplierId, s.SupplierCode, s.Name AS SupplierName,
       sb.BillDate, sb.DueDate, sb.Status,
       sb.SubTotal, sb.DiscountAmount, sb.TaxAmount, sb.GrandTotal, sb.AmountPaid, sb.Notes,
       sb.JournalEntryId, sb.PostedAtUtc, sb.CreatedAtUtc, sb.UpdatedAtUtc
FROM   dbo.SupplierBill sb
JOIN   dbo.Supplier s ON s.SupplierId = sb.SupplierId
WHERE  sb.SupplierBillId = @SupplierBillId;

SELECT sbl.SupplierBillLineId, sbl.LineNumber, sbl.Description, sbl.Quantity, sbl.UnitPrice,
       sbl.DiscountPercent, sbl.TaxRatePercent,
       sbl.LineSubTotal, sbl.LineDiscount, sbl.LineTax, sbl.LineTotal,
       sbl.DebitAccountId, a.AccountCode AS DebitAccountCode, a.AccountName AS DebitAccountName
FROM   dbo.SupplierBillLine sbl
JOIN   dbo.Account a ON a.AccountId = sbl.DebitAccountId
WHERE  sbl.SupplierBillId = @SupplierBillId
ORDER BY sbl.LineNumber;

SELECT pa.PaymentAllocationId, pa.PaymentId, p.PaymentNumber, p.PaymentDate,
       pm.Name AS PaymentMethod, pa.AllocatedAmount
FROM   dbo.PaymentAllocation pa
JOIN   dbo.Payment p        ON p.PaymentId       = pa.PaymentId
JOIN   dbo.PaymentMethod pm ON pm.PaymentMethodId = p.PaymentMethodId
WHERE  pa.SupplierBillId = @SupplierBillId
ORDER BY p.PaymentDate, pa.PaymentAllocationId;";

    private const string ListFilter = @"
WHERE  (@SupplierId IS NULL OR sb.SupplierId = @SupplierId)
  AND  (@Status     IS NULL OR sb.Status     = @Status)
  AND  (@FromDate   IS NULL OR sb.BillDate  >= @FromDate)
  AND  (@ToDate     IS NULL OR sb.BillDate  <= @ToDate)";

    public const string List = $@"
SELECT sb.SupplierBillId, sb.BillNumber, sb.SupplierId, s.Name AS SupplierName,
       sb.BillDate, sb.DueDate, sb.Status, sb.GrandTotal, sb.AmountPaid
FROM   dbo.SupplierBill sb
JOIN   dbo.Supplier s ON s.SupplierId = sb.SupplierId
{ListFilter}
ORDER BY sb.BillDate DESC, sb.SupplierBillId DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.SupplierBill sb
{ListFilter};";

    public const string GetForPost = @"
SELECT sb.SupplierBillId, sb.BillNumber, sb.Status, sb.SupplierId, s.Name AS SupplierName,
       sb.BillDate, sb.TaxAmount, sb.GrandTotal
FROM   dbo.SupplierBill sb WITH (UPDLOCK, ROWLOCK)
JOIN   dbo.Supplier s ON s.SupplierId = sb.SupplierId
WHERE  sb.SupplierBillId = @SupplierBillId;

SELECT sbl.DebitAccountId, sbl.LineSubTotal, sbl.LineDiscount
FROM   dbo.SupplierBillLine sbl
WHERE  sbl.SupplierBillId = @SupplierBillId
ORDER BY sbl.LineNumber;";

    public const string GetGuard = @"
SELECT sb.SupplierBillId, sb.Status, sb.JournalEntryId, sb.AmountPaid,
       CAST(CASE WHEN EXISTS (
                 SELECT 1
                 FROM   dbo.PaymentAllocation pa
                 JOIN   dbo.Payment p ON p.PaymentId = pa.PaymentId
                 WHERE  pa.SupplierBillId = sb.SupplierBillId
                   AND  p.Status <> 3)   -- ignore allocations from reversed payments
                 THEN 1 ELSE 0 END AS BIT) AS HasAllocations
FROM   dbo.SupplierBill sb
WHERE  sb.SupplierBillId = @SupplierBillId;";
}

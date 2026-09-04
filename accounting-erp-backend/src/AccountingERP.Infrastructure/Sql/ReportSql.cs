namespace AccountingERP.Infrastructure.Sql;

/// <summary>
/// The §5 reports, as parameterised SQL. Balances are derived from the journal and
/// from payment allocations — no report reads a stored balance column.
/// </summary>
internal static class ReportSql
{
    // ---- Trial Balance: one grouped pass, signed balance per account -------
    public const string TrialBalance = @"
WITH Movement AS (
    SELECT jl.AccountId,
           SUM(jl.Debit)  AS TotalDebit,
           SUM(jl.Credit) AS TotalCredit
    FROM   dbo.JournalEntryLine jl
    JOIN   dbo.JournalEntry     je ON je.JournalEntryId = jl.JournalEntryId
    WHERE  je.IsPosted = 1
      AND  je.EntryDate <= @AsOfDate
    GROUP BY jl.AccountId
)
SELECT a.AccountCode, a.AccountName, at.Name AS AccountType,
       m.TotalDebit, m.TotalCredit,
       CASE WHEN m.TotalDebit  - m.TotalCredit > 0 THEN m.TotalDebit  - m.TotalCredit ELSE 0 END AS DebitBalance,
       CASE WHEN m.TotalCredit - m.TotalDebit  > 0 THEN m.TotalCredit - m.TotalDebit  ELSE 0 END AS CreditBalance
FROM   Movement m
JOIN   dbo.Account     a  ON a.AccountId      = m.AccountId
JOIN   dbo.AccountType at ON at.AccountTypeId = a.AccountTypeId
WHERE  m.TotalDebit <> 0 OR m.TotalCredit <> 0
ORDER BY a.AccountCode;";

    // ---- Profit & Loss: grouped by AccountType, no account-code literal ----
    public const string ProfitAndLoss = @"
SELECT at.Name AS Section,
       a.AccountCode, a.AccountName,
       CASE WHEN at.NormalBalance = 'C'
            THEN SUM(jl.Credit - jl.Debit)
            ELSE SUM(jl.Debit  - jl.Credit) END AS Amount
FROM   dbo.JournalEntryLine jl
JOIN   dbo.JournalEntry     je ON je.JournalEntryId  = jl.JournalEntryId
JOIN   dbo.Account          a  ON a.AccountId        = jl.AccountId
JOIN   dbo.AccountType      at ON at.AccountTypeId   = a.AccountTypeId
WHERE  je.IsPosted = 1
  AND  at.IsBalanceSheet = 0
  AND  je.EntryDate BETWEEN @FromDate AND @ToDate
GROUP BY at.Name, at.NormalBalance, a.AccountCode, a.AccountName
HAVING SUM(jl.Debit) <> 0 OR SUM(jl.Credit) <> 0
ORDER BY at.Name DESC, a.AccountCode;";

    // ---- General Ledger: opening balance + window-function running balance -
    public const string GeneralLedger = @"
DECLARE @Opening DECIMAL(18,2) = ISNULL((
    SELECT SUM(jl.Debit - jl.Credit)
    FROM   dbo.JournalEntryLine jl
    JOIN   dbo.JournalEntry     je ON je.JournalEntryId = jl.JournalEntryId
    WHERE  jl.AccountId = @AccountId
      AND  je.IsPosted  = 1
      AND  je.EntryDate < @FromDate), 0);

SELECT a.AccountId, a.AccountCode, a.AccountName, at.Name AS AccountType, @Opening AS OpeningBalance
FROM   dbo.Account a
JOIN   dbo.AccountType at ON at.AccountTypeId = a.AccountTypeId
WHERE  a.AccountId = @AccountId;

SELECT je.EntryDate, je.EntryNumber, je.Description,
       jl.Debit, jl.Credit,
       @Opening + SUM(jl.Debit - jl.Credit) OVER (
           ORDER BY je.EntryDate, je.JournalEntryId, jl.JournalEntryLineId
           ROWS UNBOUNDED PRECEDING) AS RunningBalance
FROM   dbo.JournalEntryLine jl
JOIN   dbo.JournalEntry     je ON je.JournalEntryId = jl.JournalEntryId
WHERE  jl.AccountId = @AccountId
  AND  je.IsPosted  = 1
  AND  je.EntryDate BETWEEN @FromDate AND @ToDate
ORDER BY je.EntryDate, je.JournalEntryId, jl.JournalEntryLineId;";

    // ---- Customer Outstanding: balance from allocations, with ageing -------
    public const string CustomerOutstanding = @"
WITH InvoiceOutstanding AS (
    SELECT c.CustomerCode AS PartyCode, c.Name AS PartyName,
           i.InvoiceNumber AS DocumentNumber, i.InvoiceDate AS DocumentDate, i.DueDate,
           i.GrandTotal,
           ISNULL(p.Allocated, 0)                AS AmountPaid,
           i.GrandTotal - ISNULL(p.Allocated, 0) AS Outstanding,
           DATEDIFF(DAY, ISNULL(i.DueDate, i.InvoiceDate), @AsOfDate) AS DaysOverdue
    FROM   dbo.SalesInvoice i
    JOIN   dbo.Customer c ON c.CustomerId = i.CustomerId
    OUTER APPLY (
        SELECT SUM(pa.AllocatedAmount) AS Allocated
        FROM   dbo.PaymentAllocation pa
        JOIN   dbo.Payment pm ON pm.PaymentId = pa.PaymentId
        WHERE  pa.SalesInvoiceId = i.SalesInvoiceId
          AND  pm.PaymentType = 1 AND pm.Status = 2 AND pm.PaymentDate <= @AsOfDate
    ) p
    WHERE  i.Status = 2
      AND  i.InvoiceDate <= @AsOfDate
      AND  (@PartyId IS NULL OR i.CustomerId = @PartyId)
)
SELECT PartyCode, PartyName, DocumentNumber, DocumentDate, DueDate,
       GrandTotal, AmountPaid, Outstanding,
       CASE WHEN DaysOverdue <= 0  THEN 'Current'
            WHEN DaysOverdue <= 30 THEN '1-30'
            WHEN DaysOverdue <= 60 THEN '31-60'
            WHEN DaysOverdue <= 90 THEN '61-90'
            ELSE '90+' END AS AgeingBucket
FROM   InvoiceOutstanding
WHERE  Outstanding > 0
ORDER BY PartyCode, DocumentDate
OPTION (RECOMPILE);";

    // ---- Supplier Outstanding: same shape against SupplierBill ------------
    public const string SupplierOutstanding = @"
WITH BillOutstanding AS (
    SELECT s.SupplierCode AS PartyCode, s.Name AS PartyName,
           b.BillNumber AS DocumentNumber, b.BillDate AS DocumentDate, b.DueDate,
           b.GrandTotal,
           ISNULL(p.Allocated, 0)                AS AmountPaid,
           b.GrandTotal - ISNULL(p.Allocated, 0) AS Outstanding,
           DATEDIFF(DAY, ISNULL(b.DueDate, b.BillDate), @AsOfDate) AS DaysOverdue
    FROM   dbo.SupplierBill b
    JOIN   dbo.Supplier s ON s.SupplierId = b.SupplierId
    OUTER APPLY (
        SELECT SUM(pa.AllocatedAmount) AS Allocated
        FROM   dbo.PaymentAllocation pa
        JOIN   dbo.Payment pm ON pm.PaymentId = pa.PaymentId
        WHERE  pa.SupplierBillId = b.SupplierBillId
          AND  pm.PaymentType = 2 AND pm.Status = 2 AND pm.PaymentDate <= @AsOfDate
    ) p
    WHERE  b.Status = 2
      AND  b.BillDate <= @AsOfDate
      AND  (@PartyId IS NULL OR b.SupplierId = @PartyId)
)
SELECT PartyCode, PartyName, DocumentNumber, DocumentDate, DueDate,
       GrandTotal, AmountPaid, Outstanding,
       CASE WHEN DaysOverdue <= 0  THEN 'Current'
            WHEN DaysOverdue <= 30 THEN '1-30'
            WHEN DaysOverdue <= 60 THEN '31-60'
            WHEN DaysOverdue <= 90 THEN '61-90'
            ELSE '90+' END AS AgeingBucket
FROM   BillOutstanding
WHERE  Outstanding > 0
ORDER BY PartyCode, DocumentDate
OPTION (RECOMPILE);";
}

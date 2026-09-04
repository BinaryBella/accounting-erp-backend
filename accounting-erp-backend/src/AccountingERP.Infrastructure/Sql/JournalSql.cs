namespace AccountingERP.Infrastructure.Sql;

internal static class JournalSql
{
    private const string HeaderColumns = @"
    je.JournalEntryId, je.EntryNumber, je.EntryDate, je.Description,
    je.SourceType, je.SourceId, je.IsReversal, je.ReversesJournalEntryId,
    je.TotalDebit, je.TotalCredit, je.CreatedBy, je.CreatedAtUtc";

    public const string InsertHeader = @"
INSERT INTO dbo.JournalEntry
    (EntryNumber, EntryDate, Description, SourceType, SourceId,
     IsReversal, ReversesJournalEntryId, TotalDebit, TotalCredit, IsPosted)
OUTPUT INSERTED.JournalEntryId
VALUES
    (@EntryNumber, @EntryDate, @Description, @SourceType, @SourceId,
     @IsReversal, @ReversesJournalEntryId, @TotalDebit, @TotalCredit, 1);";

    public const string InsertLines = @"
INSERT INTO dbo.JournalEntryLine
    (JournalEntryId, LineNumber, AccountId, Debit, Credit, Description, CustomerId, SupplierId)
SELECT @JournalEntryId, t.LineNumber, t.AccountId, t.Debit, t.Credit, t.Description, t.CustomerId, t.SupplierId
FROM   @Lines AS t;";

    public const string ReconcileTotals = @"
UPDATE je
SET    je.TotalDebit  = s.SumDebit,
       je.TotalCredit = s.SumCredit
FROM   dbo.JournalEntry je
CROSS APPLY (
    SELECT SUM(jl.Debit) AS SumDebit, SUM(jl.Credit) AS SumCredit
    FROM   dbo.JournalEntryLine jl
    WHERE  jl.JournalEntryId = je.JournalEntryId
) s
WHERE  je.JournalEntryId = @JournalEntryId;";

    public const string GetById = $@"
SELECT {HeaderColumns}
FROM   dbo.JournalEntry je
WHERE  je.JournalEntryId = @JournalEntryId;

SELECT jl.LineNumber, jl.AccountId, a.AccountCode, a.AccountName,
       jl.Debit, jl.Credit, jl.Description, jl.CustomerId, jl.SupplierId
FROM   dbo.JournalEntryLine jl
JOIN   dbo.Account a ON a.AccountId = jl.AccountId
WHERE  jl.JournalEntryId = @JournalEntryId
ORDER BY jl.LineNumber;";

    private const string ListFilter = @"
WHERE  (@FromDate   IS NULL OR je.EntryDate  >= @FromDate)
  AND  (@ToDate     IS NULL OR je.EntryDate  <= @ToDate)
  AND  (@SourceType IS NULL OR je.SourceType  = @SourceType)
  AND  (@AccountId  IS NULL OR EXISTS (
            SELECT 1 FROM dbo.JournalEntryLine jl
            WHERE jl.JournalEntryId = je.JournalEntryId AND jl.AccountId = @AccountId))";

    public const string List = $@"
SELECT {HeaderColumns}
FROM   dbo.JournalEntry je
{ListFilter}
ORDER BY je.EntryDate DESC, je.JournalEntryId DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.JournalEntry je
{ListFilter};";

    // header basics + lines, for posting a mirrored (swapped) entry
    public const string GetForReverse = @"
SELECT je.JournalEntryId, je.EntryNumber, je.SourceType, je.SourceId, je.IsReversal
FROM   dbo.JournalEntry je
WHERE  je.JournalEntryId = @JournalEntryId;

SELECT jl.AccountId, jl.Debit, jl.Credit, jl.Description, jl.CustomerId, jl.SupplierId
FROM   dbo.JournalEntryLine jl
WHERE  jl.JournalEntryId = @JournalEntryId
ORDER BY jl.LineNumber;";

    public const string IsAlreadyReversed = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.JournalEntry WHERE ReversesJournalEntryId = @JournalEntryId)
            THEN 1 ELSE 0 END;";
}

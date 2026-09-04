namespace AccountingERP.Infrastructure.Sql;

internal static class NumberSequenceSql
{
    /// <summary>
    /// Single statement, no read-then-write race: increments and formats in one shot,
    /// gap-free under concurrency (PLAN §2.10). Runs in the caller's transaction.
    /// </summary>
    public const string AllocateNext = @"
UPDATE dbo.NumberSequence WITH (UPDLOCK, ROWLOCK)
SET    NextNumber = NextNumber + 1
OUTPUT deleted.Prefix
     + RIGHT(REPLICATE('0', deleted.PadLength) + CAST(deleted.NextNumber AS VARCHAR(20)), deleted.PadLength)
       AS DocumentNumber
WHERE  SequenceKey = @SequenceKey;";
}

/* =====================================================================
   SSIT Practical Assessment — ABC Trading (Pvt) Ltd
   OPTIONAL: Opening balance journal entry.

   This script is NOT required by the assessment brief — it's a documented
   assumption (see README §Assumptions) that gives Cash/Bank a starting
   balance so the demo trial balance doesn't show them as pure credits
   after the §10 walkthrough runs. Skip this script entirely and the
   schema/API behave identically without it.

   Run AFTER 02_seed.sql:
       sqlcmd -S localhost,1433 -U sa -P '<password>' -I -i 03_demo_data.sql

   Posts directly to the journal tables (there is no application running
   yet at this stage of setup) using the exact same number-allocation
   pattern (OUTPUT ... deleted.NextNumber) the application's
   NumberSequence allocator uses, so it mirrors what JournalService will
   do once it exists.
   ===================================================================== */

USE AccountingERPDb;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Guard and insert live in ONE batch: RETURN only exits the batch up to the next GO,
-- so a duplicate-guard split across a GO boundary would print "skipping" and then fall
-- through and insert anyway on a second run. Keeping it all in one IF block avoids that.
IF NOT EXISTS (SELECT 1 FROM dbo.JournalEntry WHERE SourceType = 6)
BEGIN
    BEGIN TRANSACTION;

    DECLARE @CashId  INT = (SELECT AccountId FROM dbo.Account WHERE AccountCode = '1010');
    DECLARE @BankId  INT = (SELECT AccountId FROM dbo.Account WHERE AccountCode = '1020');
    DECLARE @CapId   INT = (SELECT AccountId FROM dbo.Account WHERE AccountCode = '3000');

    IF @CashId IS NULL OR @BankId IS NULL OR @CapId IS NULL
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50020, 'Chart of accounts is missing 1010/1020/3000 — run 02_seed.sql first.', 1;
    END

    DECLARE @NumberOutput TABLE (DocumentNumber NVARCHAR(30));
    DECLARE @EntryNo NVARCHAR(30);

    UPDATE dbo.NumberSequence WITH (UPDLOCK, ROWLOCK)
    SET NextNumber = NextNumber + 1
    OUTPUT deleted.Prefix + RIGHT(REPLICATE('0', deleted.PadLength) + CAST(deleted.NextNumber AS VARCHAR(20)), deleted.PadLength)
        INTO @NumberOutput
    WHERE SequenceKey = 'Journal';

    SELECT @EntryNo = DocumentNumber FROM @NumberOutput;

    DECLARE @JeId INT;

    INSERT INTO dbo.JournalEntry (EntryNumber, EntryDate, Description, SourceType, SourceId, TotalDebit, TotalCredit, IsPosted)
    VALUES (@EntryNo, CAST(SYSUTCDATETIME() AS DATE), 'Opening balances - owner capital introduced', 6, NULL, 500000.00, 500000.00, 1);

    SET @JeId = SCOPE_IDENTITY();

    INSERT INTO dbo.JournalEntryLine (JournalEntryId, LineNumber, AccountId, Debit, Credit, Description) VALUES
        (@JeId, 1, @CashId, 100000.00, 0.00, 'Opening cash'),
        (@JeId, 2, @BankId, 400000.00, 0.00, 'Opening bank balance'),
        (@JeId, 3, @CapId,  0.00, 500000.00, N'Owner''s capital introduced');

    COMMIT TRANSACTION;

    PRINT 'Opening balance entry posted.';
END
ELSE
BEGIN
    PRINT 'Opening balance entry already exists — skipping.';
END
GO

-- Verification
SELECT je.EntryNumber, je.Description, je.TotalDebit, je.TotalCredit,
       CASE WHEN je.TotalDebit = je.TotalCredit THEN 'BALANCED' ELSE 'BROKEN' END AS Check_
FROM dbo.JournalEntry je
WHERE je.SourceType = 6;

SELECT a.AccountCode, a.AccountName, jl.Debit, jl.Credit
FROM dbo.JournalEntryLine jl
JOIN dbo.Account a ON a.AccountId = jl.AccountId
JOIN dbo.JournalEntry je ON je.JournalEntryId = jl.JournalEntryId
WHERE je.SourceType = 6
ORDER BY jl.LineNumber;
GO

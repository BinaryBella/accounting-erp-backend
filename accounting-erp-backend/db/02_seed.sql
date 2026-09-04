/* =====================================================================
   SSIT Practical Assessment — ABC Trading (Pvt) Ltd
   Seed Data: account types, chart of accounts, mappings, payment methods,
   document numbering sequences.

   Run AFTER 01_schema.sql:
       sqlcmd -S localhost,1433 -U sa -P '<password>' -I -i 02_seed.sql

   Idempotent: clears its own rows before inserting, so it can be re-run
   safely as long as 01_schema.sql has not also been re-run since (which
   already empties these tables).
   ===================================================================== */

USE AccountingERPDb;
GO

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

DELETE FROM dbo.NumberSequence;
DELETE FROM dbo.PaymentMethod;
DELETE FROM dbo.AccountMapping;
DELETE FROM dbo.Account;
DELETE FROM dbo.AccountType;
GO

/* ---------------------------------------------------------------------
   1. Account Types
   --------------------------------------------------------------------- */
INSERT INTO dbo.AccountType (AccountTypeId, Name, NormalBalance, IsBalanceSheet) VALUES
    (1, 'Asset',     'D', 1),
    (2, 'Liability', 'C', 1),
    (3, 'Equity',    'C', 1),
    (4, 'Revenue',   'C', 0),
    (5, 'Expense',   'D', 0);
GO

/* ---------------------------------------------------------------------
   2. Chart of Accounts (§3A: seed/demo accounts)
   --------------------------------------------------------------------- */
INSERT INTO dbo.Account (AccountCode, AccountName, AccountTypeId, IsSystem) VALUES
    ('1010', 'Cash in Hand',          1, 1),
    ('1020', 'Bank Account',          1, 1),
    ('1100', 'Accounts Receivable',   1, 1),
    ('1200', 'Inventory',             1, 1),
    ('1210', 'Input Tax Receivable',  1, 1),
    ('2000', 'Accounts Payable',      2, 1),
    ('2100', 'Tax Payable (Output)',  2, 1),
    ('3000', N'Owner''s Capital',     3, 1),
    ('3100', 'Retained Earnings',     3, 1),
    ('4000', 'Sales Revenue',         4, 1),
    ('4100', 'Other Income',          4, 0),
    ('5000', 'Purchases',             5, 1),
    ('5100', 'Operating Expenses',    5, 0),
    ('5200', 'Bank Charges',          5, 0);
GO

/* ---------------------------------------------------------------------
   3. Account Mappings — every logical account the posting engine needs
      is resolved through this table, never through a hard-coded code.
   --------------------------------------------------------------------- */
INSERT INTO dbo.AccountMapping (MappingKey, AccountId, Description)
SELECT 'AccountsReceivable', AccountId, 'Default AR control account for sales invoices'
FROM dbo.Account WHERE AccountCode = '1100'
UNION ALL
SELECT 'SalesRevenue', AccountId, 'Default revenue account for invoice lines'
FROM dbo.Account WHERE AccountCode = '4000'
UNION ALL
SELECT 'TaxPayableOutput', AccountId, 'Output tax collected on sales invoices'
FROM dbo.Account WHERE AccountCode = '2100'
UNION ALL
SELECT 'AccountsPayable', AccountId, 'Default AP control account for supplier bills'
FROM dbo.Account WHERE AccountCode = '2000'
UNION ALL
SELECT 'SupplierBillDefaultDebit', AccountId, 'Default debit account for supplier bill lines (Purchases)'
FROM dbo.Account WHERE AccountCode = '5000'
UNION ALL
SELECT 'TaxReceivableInput', AccountId, 'Input tax paid on supplier bills'
FROM dbo.Account WHERE AccountCode = '1210'
UNION ALL
SELECT 'RetainedEarnings', AccountId, 'Retained earnings / opening balance clearing'
FROM dbo.Account WHERE AccountCode = '3100';
GO

/* ---------------------------------------------------------------------
   4. Payment Methods (§3D: at least Cash and Bank)
   --------------------------------------------------------------------- */
INSERT INTO dbo.PaymentMethod (PaymentMethodId, Name, LedgerAccountId)
SELECT 1, 'Cash', AccountId FROM dbo.Account WHERE AccountCode = '1010'
UNION ALL
SELECT 2, 'Bank', AccountId FROM dbo.Account WHERE AccountCode = '1020';
GO

/* ---------------------------------------------------------------------
   5. Document Number Sequences
   --------------------------------------------------------------------- */
INSERT INTO dbo.NumberSequence (SequenceKey, Prefix, NextNumber, PadLength) VALUES
    ('SalesInvoice', 'INV-',  1, 6),
    ('SupplierBill', 'BILL-', 1, 6),
    ('Receipt',      'RCT-',  1, 6),
    ('Payment',      'PAY-',  1, 6),
    ('Journal',      'JV-',   1, 6);
GO

/* ---------------------------------------------------------------------
   6. Verification — run these by eye after the script finishes
   --------------------------------------------------------------------- */
SELECT 'AccountType' AS TableName, COUNT(*) AS [RowCount] FROM dbo.AccountType
UNION ALL SELECT 'Account',          COUNT(*) FROM dbo.Account
UNION ALL SELECT 'AccountMapping',   COUNT(*) FROM dbo.AccountMapping
UNION ALL SELECT 'PaymentMethod',    COUNT(*) FROM dbo.PaymentMethod
UNION ALL SELECT 'NumberSequence',   COUNT(*) FROM dbo.NumberSequence;
-- Expected: AccountType 5, Account 14, AccountMapping 7, PaymentMethod 2, NumberSequence 5

SELECT MappingKey, a.AccountCode, a.AccountName
FROM dbo.AccountMapping m JOIN dbo.Account a ON a.AccountId = m.AccountId
ORDER BY MappingKey;
-- Every mapping key should resolve to a real account with no NULLs

PRINT 'Seed data loaded successfully.';
GO

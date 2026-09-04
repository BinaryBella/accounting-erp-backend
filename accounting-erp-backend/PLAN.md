# SSIT Practical Assessment — Implementation Plan
**Software Engineer – Accounting & ERP Systems**
ASP.NET Core Web API + Dapper + SQL Server · 8-hour window · backend & database only

> Design principle for every decision below: **the brief is the spec.** Where the brief is
> explicit (double-entry table in §4, endpoint list in §6, Dapper rules in §8, demo in §10)
> the implementation follows it literally. Where the brief is silent, the brief itself grants
> permission — *"You may make reasonable accounting assumptions, but document them clearly in
> the README"* — so each gap is closed with the simplest defensible choice and recorded in
> the README's Assumptions section.

---

## 0. Scoring map — where the marks actually are

| Area | Weight | What must be visibly true in the repo |
|---|---|---|
| Accounting & double-entry | **35%** | Every posted document produces a balanced `JournalEntry`; Trial Balance foots; §11 answers written properly |
| ASP.NET Core API & code quality | **25%** | Controllers thin, services own rules, DTOs at the edge, DI, Swagger, correct status codes |
| SQL Server design & integrity | **20%** | PK/FK/CHECK/UNIQUE everywhere, `DECIMAL` money, transactions, timestamps |
| Dapper | **20%** | Parameterised only, one `IDbTransaction` per posting, clean mapping, SQL out of controllers |

Two things carry disproportionate weight for the effort they cost: **the §11 written answers**
and **the README assumptions list**. Neither requires code. Do not leave them to the last 20 minutes.

---

## 1. Stack and solution layout

```
.NET 8 (LTS)
Dapper                          2.1.x
Microsoft.Data.SqlClient        5.x        (NOT System.Data.SqlClient)
Swashbuckle.AspNetCore          6.x
FluentValidation.AspNetCore     11.x       (optional — DataAnnotations is acceptable)
SQL Server 2019/2022            (docker: mcr.microsoft.com/mssql/server:2022-latest)
```

```
SsitAccounting.sln
├─ src/
│  ├─ SsitAccounting.Api/                  ASP.NET Core Web API
│  │   ├─ Controllers/                     thin — model-bind, call service, map status code
│  │   ├─ Middleware/ExceptionHandlingMiddleware.cs
│  │   ├─ Program.cs  appsettings.json
│  ├─ SsitAccounting.Application/          business logic — no SQL, no HTTP
│  │   ├─ Dtos/                            request + response contracts
│  │   ├─ Services/                        SalesInvoiceService, PaymentService, JournalService…
│  │   ├─ Abstractions/                    IUnitOfWork, I*Repository, I*Service
│  │   ├─ Domain/                          entities, enums, AccountMappingKeys
│  │   └─ Validation/                      FluentValidation validators
│  └─ SsitAccounting.Infrastructure/       Dapper only
│      ├─ SqlConnectionFactory.cs
│      ├─ UnitOfWork.cs                    IDbConnection + IDbTransaction
│      ├─ Repositories/
│      └─ Sql/                             static SQL text, one class per aggregate
├─ db/
│  ├─ 01_schema.sql                        tables, constraints, indexes, TVP types, triggers
│  ├─ 02_seed.sql                          account types, chart of accounts, mappings, sequences
│  └─ 03_demo_data.sql                     optional: C001, S001, opening balances
├─ docs/
│  ├─ accounting-answers.md                §11 — Q1..Q5
│  └─ screenshots/
├─ requests/demo.http                      the §10 nine-step scenario, runnable in VS Code / Rider
└─ README.md
```

Three projects, not one — the brief asks for *"clear separation of concerns (controllers,
services, data-access/repositories)"* and a project boundary proves it better than a folder does.
`Application` has no reference to `Microsoft.Data.SqlClient`; `Api` has no reference to
`Infrastructure` types except in `Program.cs` DI registration.

**If time runs short**, collapsing to one project with `Controllers/`, `Services/`,
`Repositories/` folders costs almost no marks. Collapsing the *layering* does.

---

## 2. Database design

### 2.1 Money and type conventions

| Concept | Type | Reason |
|---|---|---|
| Monetary amount | `DECIMAL(18,2)` | §7: no floating point for money |
| Unit price, quantity | `DECIMAL(18,4)` | allows fractional pricing/qty; rounded to 2dp at line total |
| Percentage (discount, tax rate) | `DECIMAL(9,4)` | 18.0000% |
| Dates on documents | `DATE` | accounting date has no time component |
| Audit timestamps | `DATETIME2(3)` UTC | `SYSUTCDATETIME()` default |
| Codes | `NVARCHAR(20)` | `AccountCode`, `CustomerCode` |

**Rounding rule (assumption to document):** each line is rounded to 2dp with
`MidpointRounding.AwayFromZero`; header totals are the **sum of already-rounded line values**,
so the header can never disagree with its lines by a cent.

### 2.2 Reference tables

```sql
CREATE TABLE dbo.AccountType (
    AccountTypeId   TINYINT       NOT NULL PRIMARY KEY,
    Name            NVARCHAR(20)  NOT NULL UNIQUE,   -- Asset, Liability, Equity, Revenue, Expense
    NormalBalance   CHAR(1)       NOT NULL CONSTRAINT CK_AccountType_NormalBalance CHECK (NormalBalance IN ('D','C')),
    IsBalanceSheet  BIT           NOT NULL           -- 1 = Asset/Liability/Equity, 0 = Revenue/Expense
);
```

`NormalBalance` and `IsBalanceSheet` are what let the Trial Balance present a signed balance
and let the P&L pick its accounts **without a single hard-coded account code in C#** — which is
exactly what §9's *"No unexplained hard-coded accounting calculations"* is testing.

```sql
CREATE TABLE dbo.Account (
    AccountId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Account PRIMARY KEY,
    AccountCode      NVARCHAR(20)  NOT NULL,
    AccountName      NVARCHAR(150) NOT NULL,
    AccountTypeId    TINYINT       NOT NULL CONSTRAINT FK_Account_AccountType REFERENCES dbo.AccountType(AccountTypeId),
    ParentAccountId  INT           NULL     CONSTRAINT FK_Account_Parent      REFERENCES dbo.Account(AccountId),
    IsActive         BIT           NOT NULL CONSTRAINT DF_Account_IsActive    DEFAULT (1),
    IsSystem         BIT           NOT NULL CONSTRAINT DF_Account_IsSystem    DEFAULT (0),
    CreatedAtUtc     DATETIME2(3)  NOT NULL CONSTRAINT DF_Account_Created     DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc     DATETIME2(3)  NULL,
    CONSTRAINT UQ_Account_AccountCode UNIQUE (AccountCode)          -- §3A: prevent duplicate codes
);
```

`IsSystem = 1` marks the control accounts (AR, AP, Tax Payable, Sales, Cash, Bank) that the
posting engine depends on — they can be renamed but never deactivated or deleted.

### 2.3 The account mapping table — the anti-hard-coding device

```sql
CREATE TABLE dbo.AccountMapping (
    MappingKey   NVARCHAR(50) NOT NULL CONSTRAINT PK_AccountMapping PRIMARY KEY,
    AccountId    INT          NOT NULL CONSTRAINT FK_AccountMapping_Account REFERENCES dbo.Account(AccountId),
    Description  NVARCHAR(200) NOT NULL
);
```

Seeded keys: `AccountsReceivable`, `SalesRevenue`, `TaxPayableOutput`, `AccountsPayable`,
`SupplierBillDefaultDebit`, `TaxReceivableInput`, `RetainedEarnings`.

`JournalService` resolves accounts through this table (cached at startup, invalidated on write).
Nowhere in C# does the string `"1100"` appear. When the interviewer asks *"what if ABC Trading
renumbers their chart of accounts?"* the answer is a row update, not a redeploy.

### 2.4 Payment methods (§3D: "at least Cash and Bank")

```sql
CREATE TABLE dbo.PaymentMethod (
    PaymentMethodId  TINYINT      NOT NULL PRIMARY KEY,   -- 1 = Cash, 2 = Bank
    Name             NVARCHAR(30) NOT NULL UNIQUE,
    LedgerAccountId  INT          NOT NULL CONSTRAINT FK_PaymentMethod_Account REFERENCES dbo.Account(AccountId),
    IsActive         BIT          NOT NULL DEFAULT (1)
);
```

The Cash/Bank leg of every receipt and payment comes from `PaymentMethod.LedgerAccountId`.
Adding "Cheque" or a second bank account later is a seed row, not a code change.

### 2.5 Parties

```sql
CREATE TABLE dbo.Customer (
    CustomerId     INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    CustomerCode   NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Customer_Code UNIQUE,
    Name           NVARCHAR(150) NOT NULL,
    ContactPerson  NVARCHAR(100) NULL,
    Email          NVARCHAR(150) NULL,
    Phone          NVARCHAR(30)  NULL,
    Address        NVARCHAR(300) NULL,
    IsActive       BIT           NOT NULL DEFAULT (1),
    CreatedAtUtc   DATETIME2(3)  NOT NULL DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc   DATETIME2(3)  NULL
);
-- dbo.Supplier is structurally identical (SupplierCode, …)
```

§3B says *"Customer/supplier balances must be traceable from transactions."* Note what is
**deliberately absent**: there is no `Customer.Balance` column. A stored party balance is the
classic ERP data-integrity bug — it drifts from the ledger and no one notices until year-end.
Balances are derived in the Outstanding reports from invoices and their allocations. Say this
out loud in the README; it is a genuine design opinion and it is the correct one.

### 2.6 Sales invoice

```sql
CREATE TABLE dbo.SalesInvoice (
    SalesInvoiceId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    InvoiceNumber   NVARCHAR(30) NOT NULL CONSTRAINT UQ_SalesInvoice_Number UNIQUE,
    CustomerId      INT          NOT NULL CONSTRAINT FK_SalesInvoice_Customer REFERENCES dbo.Customer(CustomerId),
    InvoiceDate     DATE         NOT NULL,
    DueDate         DATE         NULL,
    Status          TINYINT      NOT NULL CONSTRAINT DF_SalesInvoice_Status DEFAULT (1),
    SubTotal        DECIMAL(18,2) NOT NULL CONSTRAINT CK_SI_SubTotal CHECK (SubTotal        >= 0),
    DiscountAmount  DECIMAL(18,2) NOT NULL CONSTRAINT CK_SI_Discount CHECK (DiscountAmount  >= 0),
    TaxAmount       DECIMAL(18,2) NOT NULL CONSTRAINT CK_SI_Tax      CHECK (TaxAmount       >= 0),
    GrandTotal      DECIMAL(18,2) NOT NULL CONSTRAINT CK_SI_Grand    CHECK (GrandTotal      >= 0),
    AmountPaid      DECIMAL(18,2) NOT NULL CONSTRAINT DF_SI_Paid DEFAULT (0),
    Notes           NVARCHAR(500) NULL,
    JournalEntryId  INT          NULL CONSTRAINT FK_SalesInvoice_JournalEntry REFERENCES dbo.JournalEntry(JournalEntryId),
    PostedAtUtc     DATETIME2(3) NULL,
    CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc    DATETIME2(3) NULL,
    CONSTRAINT CK_SalesInvoice_Status   CHECK (Status IN (1,2,3)),                 -- 1 Draft, 2 Posted, 3 Reversed
    CONSTRAINT CK_SalesInvoice_Paid     CHECK (AmountPaid >= 0 AND AmountPaid <= GrandTotal),
    CONSTRAINT CK_SalesInvoice_Posted   CHECK (Status <> 2 OR (JournalEntryId IS NOT NULL AND PostedAtUtc IS NOT NULL))
);
```

`CK_SalesInvoice_Posted` is worth pointing at in the interview: **the database itself refuses to
hold a posted invoice that has no journal entry.** Financial integrity is not left to the C#.

On `AmountPaid`: allocations remain the single source of truth; `AmountPaid` is a maintained
cache written inside the same transaction as the allocation, and `CK_SalesInvoice_Paid` makes
over-application impossible at the storage layer. The Outstanding report computes from
allocations, so any drift would surface immediately.

```sql
CREATE TABLE dbo.SalesInvoiceLine (
    SalesInvoiceLineId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    SalesInvoiceId  INT NOT NULL CONSTRAINT FK_SIL_Invoice REFERENCES dbo.SalesInvoice(SalesInvoiceId) ON DELETE CASCADE,
    LineNumber      INT           NOT NULL,
    Description     NVARCHAR(250) NOT NULL,
    Quantity        DECIMAL(18,4) NOT NULL CONSTRAINT CK_SIL_Qty      CHECK (Quantity  > 0),
    UnitPrice       DECIMAL(18,4) NOT NULL CONSTRAINT CK_SIL_Price    CHECK (UnitPrice >= 0),
    DiscountPercent DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SIL_DiscPct DEFAULT (0)
                                  CONSTRAINT CK_SIL_DiscPct CHECK (DiscountPercent BETWEEN 0 AND 100),
    TaxRatePercent  DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SIL_TaxPct  DEFAULT (0)
                                  CONSTRAINT CK_SIL_TaxPct  CHECK (TaxRatePercent  BETWEEN 0 AND 100),
    LineSubTotal    DECIMAL(18,2) NOT NULL,   -- Quantity * UnitPrice
    LineDiscount    DECIMAL(18,2) NOT NULL,   -- LineSubTotal * DiscountPercent / 100
    LineTax         DECIMAL(18,2) NOT NULL,   -- (LineSubTotal - LineDiscount) * TaxRatePercent / 100
    LineTotal       DECIMAL(18,2) NOT NULL,   -- LineSubTotal - LineDiscount + LineTax
    RevenueAccountId INT NOT NULL CONSTRAINT FK_SIL_Revenue REFERENCES dbo.Account(AccountId),
    CONSTRAINT UQ_SIL_LineNumber UNIQUE (SalesInvoiceId, LineNumber)
);
```

Line amounts are **computed server-side and persisted**, never trusted from the request body.
The client sends qty / price / discount% / tax%; the service computes the rest. A client that
posts `lineTotal: 1` is ignored.

`RevenueAccountId` per line defaults from `AccountMapping['SalesRevenue']` — so a single invoice
can split across revenue accounts, which is what a real system does.

### 2.7 Supplier bill

`dbo.SupplierBill` / `dbo.SupplierBillLine` mirror the above with `BillNumber`, `SupplierId`,
`BillDate`, and one deliberate difference on the line:

```sql
    DebitAccountId  INT NOT NULL CONSTRAINT FK_SBL_Debit REFERENCES dbo.Account(AccountId),
```

§4's table specifies the supplier-bill debit as **"Inventory / Purchase Expense"** — the brief
offers both. So the line carries the account rather than the code choosing one: it defaults from
`AccountMapping['SupplierBillDefaultDebit']` and the caller may override per line. Seed the
default to `5000 Purchases (Expense)` so the demo's Profit & Loss has something in it, and note
in the README that posting to `1200 Inventory` is equally supported and that periodic (not
perpetual) inventory is assumed — COGS recognition and stock valuation are out of scope.

### 2.8 Payments — one table, two directions

```sql
CREATE TABLE dbo.Payment (
    PaymentId       INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PaymentNumber   NVARCHAR(30) NOT NULL CONSTRAINT UQ_Payment_Number UNIQUE,
    PaymentType     TINYINT      NOT NULL,   -- 1 = Customer Receipt, 2 = Supplier Payment
    PaymentDate     DATE         NOT NULL,
    CustomerId      INT          NULL CONSTRAINT FK_Payment_Customer REFERENCES dbo.Customer(CustomerId),
    SupplierId      INT          NULL CONSTRAINT FK_Payment_Supplier REFERENCES dbo.Supplier(SupplierId),
    PaymentMethodId TINYINT      NOT NULL CONSTRAINT FK_Payment_Method REFERENCES dbo.PaymentMethod(PaymentMethodId),
    ReferenceNo     NVARCHAR(50) NULL,       -- cheque no / bank slip
    Amount          DECIMAL(18,2) NOT NULL CONSTRAINT CK_Payment_Amount CHECK (Amount > 0),
    Status          TINYINT      NOT NULL DEFAULT (1),
    JournalEntryId  INT          NULL CONSTRAINT FK_Payment_JournalEntry REFERENCES dbo.JournalEntry(JournalEntryId),
    PostedAtUtc     DATETIME2(3) NULL,
    CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Payment_Type  CHECK (PaymentType IN (1,2)),
    CONSTRAINT CK_Payment_Party CHECK (
        (PaymentType = 1 AND CustomerId IS NOT NULL AND SupplierId IS NULL) OR
        (PaymentType = 2 AND SupplierId IS NOT NULL AND CustomerId IS NULL))
);

CREATE TABLE dbo.PaymentAllocation (
    PaymentAllocationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    PaymentId       INT NOT NULL CONSTRAINT FK_PA_Payment REFERENCES dbo.Payment(PaymentId) ON DELETE CASCADE,
    SalesInvoiceId  INT NULL CONSTRAINT FK_PA_Invoice REFERENCES dbo.SalesInvoice(SalesInvoiceId),
    SupplierBillId  INT NULL CONSTRAINT FK_PA_Bill    REFERENCES dbo.SupplierBill(SupplierBillId),
    AllocatedAmount DECIMAL(18,2) NOT NULL CONSTRAINT CK_PA_Amount CHECK (AllocatedAmount > 0),
    CONSTRAINT CK_PA_Target CHECK (
        (SalesInvoiceId IS NOT NULL AND SupplierBillId IS NULL) OR
        (SalesInvoiceId IS NULL AND SupplierBillId IS NOT NULL))
);
CREATE UNIQUE INDEX UX_PA_Payment_Invoice ON dbo.PaymentAllocation(PaymentId, SalesInvoiceId) WHERE SalesInvoiceId IS NOT NULL;
CREATE UNIQUE INDEX UX_PA_Payment_Bill    ON dbo.PaymentAllocation(PaymentId, SupplierBillId) WHERE SupplierBillId IS NOT NULL;
```

The allocation table is what makes §3D's *"Update outstanding invoice balance"* honest: one
receipt can settle three invoices, and a part-payment leaves a traceable remainder. A
`SalesInvoice.PaidAmount` column alone could not express that.

Justify the single-table choice in the README: receipts and payments have identical structure and
an identical posting shape with the debit and credit swapped, so one table plus a discriminator
avoids duplicating the allocation logic twice. The two `CHECK` constraints keep it type-safe.
Two separate tables is also defensible — have the reasoning ready either way.

### 2.9 The journal — the 35% of the marks

> **Script ordering:** `SalesInvoice`, `SupplierBill` and `Payment` all carry an FK to
> `JournalEntry`, so `01_schema.sql` must create the journal tables **first** — or create every
> table without those FKs and add them in a trailing `ALTER TABLE … ADD CONSTRAINT` block. The
> second option keeps the script re-runnable and easier to read; either is fine as long as it
> executes top-to-bottom on an empty database without errors.

```sql
CREATE TABLE dbo.JournalEntry (
    JournalEntryId  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EntryNumber     NVARCHAR(30) NOT NULL CONSTRAINT UQ_JE_Number UNIQUE,
    EntryDate       DATE         NOT NULL,
    Description     NVARCHAR(300) NOT NULL,
    SourceType      TINYINT      NOT NULL,   -- 1 SalesInvoice 2 CustomerReceipt 3 SupplierBill 4 SupplierPayment 5 Manual 6 Opening
    SourceId        INT          NULL,
    IsReversal      BIT          NOT NULL DEFAULT (0),
    ReversesJournalEntryId INT   NULL CONSTRAINT FK_JE_Reverses REFERENCES dbo.JournalEntry(JournalEntryId),
    TotalDebit      DECIMAL(18,2) NOT NULL,
    TotalCredit     DECIMAL(18,2) NOT NULL,
    IsPosted        BIT          NOT NULL DEFAULT (1),
    CreatedBy       NVARCHAR(100) NOT NULL DEFAULT (SUSER_SNAME()),
    CreatedAtUtc    DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_JE_Balanced CHECK (TotalDebit = TotalCredit AND TotalDebit > 0),
    CONSTRAINT CK_JE_Source   CHECK (SourceType BETWEEN 1 AND 6)
);
CREATE UNIQUE INDEX UX_JE_Source ON dbo.JournalEntry(SourceType, SourceId)
    WHERE SourceId IS NOT NULL AND IsReversal = 0;   -- one posting entry per document, ever

CREATE TABLE dbo.JournalEntryLine (
    JournalEntryLineId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    JournalEntryId  INT NOT NULL CONSTRAINT FK_JEL_Entry REFERENCES dbo.JournalEntry(JournalEntryId),
    LineNumber      INT NOT NULL,
    AccountId       INT NOT NULL CONSTRAINT FK_JEL_Account REFERENCES dbo.Account(AccountId),
    Debit           DECIMAL(18,2) NOT NULL DEFAULT (0),
    Credit          DECIMAL(18,2) NOT NULL DEFAULT (0),
    Description     NVARCHAR(300) NULL,
    CustomerId      INT NULL CONSTRAINT FK_JEL_Customer REFERENCES dbo.Customer(CustomerId),
    SupplierId      INT NULL CONSTRAINT FK_JEL_Supplier REFERENCES dbo.Supplier(SupplierId),
    CONSTRAINT CK_JEL_OneSided CHECK (
        (Debit > 0 AND Credit = 0) OR (Credit > 0 AND Debit = 0)),
    CONSTRAINT CK_JEL_NonNegative CHECK (Debit >= 0 AND Credit >= 0)
);
CREATE INDEX IX_JEL_Account ON dbo.JournalEntryLine(AccountId) INCLUDE (JournalEntryId, Debit, Credit);
```

Three layers of defence on *Total Debits = Total Credits*, and it is worth being able to name all three:

1. **`JournalService`** builds the entry in memory and throws `UnbalancedJournalException`
   before anything touches the database.
2. **`CK_JE_Balanced`** on the header. The service inserts the header, inserts the lines, then
   `UPDATE JournalEntry SET TotalDebit = (SELECT SUM(Debit)…), TotalCredit = (SELECT SUM(Credit)…)`
   from the lines it just wrote — so the constraint is evaluated against reality, not against a
   number the application asserted. An unbalanced entry cannot be committed.
3. **`CK_JEL_OneSided`** — no line can be both a debit and a credit, which kills a whole class
   of sign errors.

Plus an append-only guard:

```sql
CREATE TRIGGER dbo.TR_JournalEntryLine_NoMutation ON dbo.JournalEntryLine
INSTEAD OF UPDATE, DELETE AS
BEGIN
    THROW 50010, 'Journal lines are append-only. Correct via a reversing entry.', 1;
END;
```

> **Gotcha:** SQL Server refuses an `INSTEAD OF DELETE` trigger on a table that has a cascading
> foreign key, so `FK_JEL_Entry` above deliberately has **no** `ON DELETE CASCADE`. That is the
> right call regardless — journal entries are never deleted. `SalesInvoiceLine` keeps its cascade
> because draft invoices *are* deletable.

### 2.10 Document numbering (concurrency-safe)

```sql
CREATE TABLE dbo.NumberSequence (
    SequenceKey NVARCHAR(30) NOT NULL PRIMARY KEY,   -- SalesInvoice, SupplierBill, Receipt, Payment, Journal
    Prefix      NVARCHAR(10) NOT NULL,
    NextNumber  INT          NOT NULL,
    PadLength   TINYINT      NOT NULL
);
```

Allocated inside the posting transaction, atomically:

```sql
UPDATE dbo.NumberSequence WITH (UPDLOCK, ROWLOCK)
SET NextNumber = NextNumber + 1
OUTPUT deleted.Prefix + RIGHT(REPLICATE('0', inserted.PadLength)
     + CAST(deleted.NextNumber AS VARCHAR(20)), inserted.PadLength) AS DocumentNumber
WHERE SequenceKey = @SequenceKey;
```

Single statement, no read-then-write race, gap-free under concurrency. Mentioning *why*
`MAX(InvoiceNumber) + 1` is wrong is a cheap way to show production instinct.

### 2.11 Audit log (backs the §11 Q3 answer)

```sql
CREATE TABLE dbo.AuditLog (
    AuditLogId   BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EntityName   NVARCHAR(50)  NOT NULL,
    EntityId     INT           NOT NULL,
    Action       NVARCHAR(30)  NOT NULL,   -- Created, Updated, Posted, Reversed
    PerformedBy  NVARCHAR(100) NOT NULL,
    PerformedAtUtc DATETIME2(3) NOT NULL DEFAULT (SYSUTCDATETIME()),
    DetailJson   NVARCHAR(MAX) NULL
);
```

Written in the same transaction as the action it records.

### 2.12 Table-valued parameters (for clean Dapper bulk inserts)

```sql
CREATE TYPE dbo.JournalEntryLineTvp AS TABLE (
    LineNumber  INT           NOT NULL,
    AccountId   INT           NOT NULL,
    Debit       DECIMAL(18,2) NOT NULL,
    Credit      DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(300) NULL,
    CustomerId  INT NULL,
    SupplierId  INT NULL
);
-- dbo.SalesInvoiceLineTvp and dbo.SupplierBillLineTvp likewise
```

One round trip per line collection instead of N. Directly demonstrates Dapper competence
beyond the basics.

---

## 3. Seed data (`02_seed.sql`)

**Account types**

| Id | Name | NormalBalance | IsBalanceSheet |
|---|---|---|---|
| 1 | Asset | D | 1 |
| 2 | Liability | C | 1 |
| 3 | Equity | C | 1 |
| 4 | Revenue | C | 0 |
| 5 | Expense | D | 0 |

**Chart of accounts** (§3A: *"Provide seed/demo accounts"*)

| Code | Name | Type | System |
|---|---|---|---|
| 1010 | Cash in Hand | Asset | ✓ |
| 1020 | Bank Account | Asset | ✓ |
| 1100 | Accounts Receivable | Asset | ✓ |
| 1200 | Inventory | Asset | ✓ |
| 1210 | Input Tax Receivable | Asset | ✓ |
| 2000 | Accounts Payable | Liability | ✓ |
| 2100 | Tax Payable (Output) | Liability | ✓ |
| 3000 | Owner's Capital | Equity | ✓ |
| 3100 | Retained Earnings | Equity | ✓ |
| 4000 | Sales Revenue | Revenue | ✓ |
| 4100 | Other Income | Revenue | |
| 5000 | Purchases | Expense | ✓ |
| 5100 | Operating Expenses | Expense | |
| 5200 | Bank Charges | Expense | |

**Mappings** — `AccountsReceivable→1100`, `SalesRevenue→4000`, `TaxPayableOutput→2100`,
`AccountsPayable→2000`, `SupplierBillDefaultDebit→5000`, `TaxReceivableInput→1210`,
`RetainedEarnings→3100`.

**Payment methods** — `1 Cash→1010`, `2 Bank→1020`.

**Sequences** — `SalesInvoice/INV-/1/6`, `SupplierBill/BILL-/1/6`, `Receipt/RCT-/1/6`,
`Payment/PAY-/1/6`, `Journal/JV-/1/6`.

**Optional opening balance entry** (`03_demo_data.sql`, clearly flagged as an assumption):
`DR 1010 Cash 100,000 · DR 1020 Bank 400,000 · CR 3000 Owner's Capital 500,000`.
The brief doesn't ask for it, but without opening funds the demo leaves Cash with a credit
balance — arithmetically balanced, but it reads like a bug to an accountant reviewing the output.
Keep the file separate so the reviewer can skip it.

---

## 4. Posting engine and journal templates

`IJournalService.PostAsync(JournalDraft draft, IDbTransaction tx)` is the **only** code path in
the solution that writes to `JournalEntry`. Every document type builds a `JournalDraft`
(a source type, a date, a description, and a list of `(AccountId, Debit, Credit, party tag)`)
and hands it over. One place to get double-entry right, one place to test.

Accounts are resolved by mapping key or from the document's own line accounts — never a literal.

### The four templates (§4, verbatim)

**A. Post sales invoice** — `SourceType 1`

| Account | Debit | Credit |
|---|---|---|
| `AccountsReceivable` (1100), tagged with CustomerId | GrandTotal | |
| each line's `RevenueAccountId` (4000) | | LineSubTotal − LineDiscount |
| `TaxPayableOutput` (2100), only if TaxAmount > 0 | | TaxAmount |

Discount is netted against revenue rather than posted to a contra account. The brief asks only
that the invoice *carry* a discount and that subtotal/discount/tax/grand total be calculated;
it does not prescribe a discount account. Net revenue is the standard treatment for a trade
discount. Document the choice and note that a `4100 Discounts Allowed` contra-revenue account
would be the alternative for settlement discounts.

**B. Customer receipt** — `SourceType 2`

| Account | Debit | Credit |
|---|---|---|
| `PaymentMethod.LedgerAccountId` (1010 Cash / 1020 Bank) | Amount | |
| `AccountsReceivable` (1100), tagged with CustomerId | | Amount |

**C. Post supplier bill** — `SourceType 3`

| Account | Debit | Credit |
|---|---|---|
| each line's `DebitAccountId` (5000 Purchases *or* 1200 Inventory) | LineSubTotal − LineDiscount | |
| `TaxReceivableInput` (1210), only if TaxAmount > 0 | TaxAmount | |
| `AccountsPayable` (2000), tagged with SupplierId | | GrandTotal |

**D. Supplier payment** — `SourceType 4`

| Account | Debit | Credit |
|---|---|---|
| `AccountsPayable` (2000), tagged with SupplierId | Amount | |
| `PaymentMethod.LedgerAccountId` | | Amount |

### Posting transaction — the exact sequence

```
BEGIN TRANSACTION (ReadCommitted)
  1. SELECT the document WITH (UPDLOCK) — re-read status inside the lock
  2. Guard: status must be Draft, must have >= 1 line, date must be valid
  3. Allocate document number from NumberSequence (single UPDATE…OUTPUT)
  4. Build the JournalDraft; assert sum(debits) == sum(credits) in memory
  5. INSERT JournalEntry header with the draft's computed totals (equal and > 0, or
     CK_JE_Balanced rejects the insert immediately)
  6. INSERT JournalEntryLine rows via TVP (one round trip)
  7. UPDATE JournalEntry SET TotalDebit/TotalCredit = SUM(...) FROM the lines just written
     → reconciles the header against what actually landed; CK_JE_Balanced fires here if the
       persisted lines do not balance, even if the in-memory draft claimed they did
  8. UPDATE document SET Status = Posted, JournalEntryId, PostedAtUtc
  9. INSERT AuditLog 'Posted'
COMMIT   (any exception → Rollback, nothing partial survives — §7 and §8 both require this)
```

Payment posting inserts allocations between 6 and 8, taking `UPDLOCK` on each target invoice/bill
and re-checking `GrandTotal − AmountPaid >= AllocatedAmount` **inside the lock** — otherwise two
concurrent receipts can each pass a check and jointly over-apply an invoice.

---

## 5. Immutability and reversal (§3C: *"Posted transactions must not be freely editable"*)

The design, stated plainly for the README and for Q3:

- **Draft** is freely editable and deletable.
- **Posted** is immutable. `PUT` and `DELETE` on a posted document return **409 Conflict** with a
  message naming the reversal endpoint.
- Journal lines are append-only, enforced by `TR_JournalEntryLine_NoMutation` — not merely by
  application code, so a direct SQL `UPDATE` fails too.
- A document-level trigger blocks financial-column changes on posted rows while still permitting
  `AmountPaid`, `Status`, and `UpdatedAtUtc`:

```sql
CREATE TRIGGER dbo.TR_SalesInvoice_LockPosted ON dbo.SalesInvoice
AFTER UPDATE AS
BEGIN
    IF EXISTS (
        SELECT 1 FROM deleted d JOIN inserted i ON d.SalesInvoiceId = i.SalesInvoiceId
        WHERE d.Status = 2
          AND (d.GrandTotal <> i.GrandTotal OR d.SubTotal   <> i.SubTotal
            OR d.TaxAmount  <> i.TaxAmount  OR d.CustomerId <> i.CustomerId
            OR d.InvoiceDate <> i.InvoiceDate OR d.DiscountAmount <> i.DiscountAmount))
    BEGIN
        THROW 50011, 'A posted invoice cannot be modified. Use the reversal endpoint.', 1;
    END
END;
```

**Correction workflow:** `POST /{id}/reverse` with a reversal date and a mandatory reason →
creates a new `JournalEntry` with `IsReversal = 1`, `ReversesJournalEntryId` set, and every
debit and credit swapped; sets the document `Status = Reversed`; writes an `AuditLog` row.
The original entry stays in the ledger forever. The user then raises a fresh corrected document.
Reversal is blocked if the invoice has allocated payments — those must be reversed first.

This is exactly how Sage and QuickBooks behave, and saying so answers Q3 and Q4 together.

---

## 6. Dapper patterns (§8 — 20% of the marks, so make each rule visibly satisfied)

**Connection and transaction**

```csharp
public interface ISqlConnectionFactory { IDbConnection Create(); }

public sealed class SqlConnectionFactory(IConfiguration cfg) : ISqlConnectionFactory
{
    private readonly string _cs = cfg.GetConnectionString("AccountingDb")!;
    public IDbConnection Create() => new SqlConnection(_cs);
}

public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
    void Begin(IsolationLevel level = IsolationLevel.ReadCommitted);
    void Commit();
    void Rollback();
}
```

Registered `services.AddScoped<IUnitOfWork, UnitOfWork>()` — one connection per request, opened
lazily, disposed by the container. Every repository takes `IUnitOfWork` and passes
`transaction: _uow.Transaction` on **every** Dapper call, so a posting's header, lines, journal
and audit row all enlist in one `IDbTransaction`. That is §8's third bullet, satisfied literally.

**Parameterisation — never string concatenation**

```csharp
const string Sql = @"
SELECT SalesInvoiceId, InvoiceNumber, GrandTotal
FROM   dbo.SalesInvoice
WHERE  (@CustomerId IS NULL OR CustomerId = @CustomerId)
  AND  (@Status     IS NULL OR Status     = @Status)
  AND  (@FromDate   IS NULL OR InvoiceDate >= @FromDate)
  AND  (@ToDate     IS NULL OR InvoiceDate <= @ToDate)
ORDER BY InvoiceDate DESC, SalesInvoiceId DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
OPTION (RECOMPILE);";
```

The `(@P IS NULL OR col = @P)` optional-filter pattern keeps report filters dynamic with zero
concatenation; `OPTION (RECOMPILE)` avoids the parameter-sniffing penalty it would otherwise
cause. Explaining that trade-off is a strong interview moment.

**`QueryMultiple` for an aggregate in one round trip**

```csharp
using var grid = await conn.QueryMultipleAsync(SalesInvoiceSql.GetById,
    new { SalesInvoiceId = id }, _uow.Transaction);

var invoice     = await grid.ReadSingleOrDefaultAsync<SalesInvoiceDto>();
if (invoice is null) return null;
invoice.Lines       = (await grid.ReadAsync<SalesInvoiceLineDto>()).ToList();
invoice.Allocations = (await grid.ReadAsync<PaymentAllocationDto>()).ToList();
return invoice;
```

**Multi-mapping for the General Ledger**

```csharp
var rows = await conn.QueryAsync<GeneralLedgerRowDto, AccountSummaryDto, GeneralLedgerRowDto>(
    ReportSql.GeneralLedger,
    (row, account) => { row.Account = account; return row; },
    new { AccountId = accountId, FromDate = from, ToDate = to },
    _uow.Transaction,
    splitOn: "AccountId");
```

**TVP for line collections**

```csharp
var table = new DataTable();
table.Columns.Add("LineNumber",  typeof(int));
table.Columns.Add("AccountId",   typeof(int));
table.Columns.Add("Debit",       typeof(decimal));
table.Columns.Add("Credit",      typeof(decimal));
// …
foreach (var l in lines) table.Rows.Add(l.LineNumber, l.AccountId, l.Debit, l.Credit /*…*/);

await conn.ExecuteAsync(JournalSql.InsertLines,
    new { JournalEntryId = id, Lines = table.AsTableValuedParameter("dbo.JournalEntryLineTvp") },
    _uow.Transaction);
```

**SQL organisation** — every query lives in `Infrastructure/Sql/*.cs` as a `const string` on a
`static class` (`SalesInvoiceSql`, `JournalSql`, `ReportSql`). Controllers contain zero SQL;
services contain zero SQL. §8's last bullet, satisfied structurally.

**Two gotchas worth pre-empting**

- `DateOnly` does not map cleanly through `Microsoft.Data.SqlClient` — register a
  `DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>` at startup, or use `DateTime` with
  `DbType.Date`. Silent failure otherwise.
- Set `SqlMapper.Settings.CommandTimeout = 30` and use `DbType.Decimal` with explicit
  precision in `DynamicParameters` for money, so nothing silently truncates.

---

## 7. API surface (§6)

Base: `/api` · JSON · `ProblemDetails` (RFC 7807) on every error · Swagger at `/swagger`.

### Chart of accounts
| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/accounts` | filters: `accountType`, `search`, `isActive`, `page`, `pageSize` |
| `GET` | `/api/accounts/{id}` | 404 if missing |
| `POST` | `/api/accounts` | 201 + `Location`; **409** on duplicate `accountCode` |
| `PUT` | `/api/accounts/{id}` | 409 if the new code collides |
| `DELETE` | `/api/accounts/{id}` | soft delete (`IsActive = 0`); **409** if `IsSystem` or the account has journal lines |
| `GET` | `/api/account-types` | the five types + normal balance |

### Customers / Suppliers
| Method | Route |
|---|---|
| `GET` | `/api/customers` · `/api/suppliers` (`search`, `isActive`, paging) |
| `GET` | `/api/customers/{id}` · `/api/suppliers/{id}` |
| `POST` | `/api/customers` · `/api/suppliers` — 409 on duplicate code |
| `PUT` | `/api/customers/{id}` · `/api/suppliers/{id}` |
| `DELETE` | `/api/customers/{id}` · `/api/suppliers/{id}` — soft delete, 409 if transactions exist |

### Sales invoices
| Method | Route | Notes |
|---|---|---|
| `POST` | `/api/sales-invoices` | creates **Draft**; server computes all amounts; 201 |
| `GET` | `/api/sales-invoices` | `customerId`, `status`, `fromDate`, `toDate`, paging |
| `GET` | `/api/sales-invoices/{id}` | header + lines + allocations (`QueryMultiple`) |
| `PUT` | `/api/sales-invoices/{id}` | Draft only → **409** if Posted |
| `DELETE` | `/api/sales-invoices/{id}` | Draft only → 409 if Posted |
| `POST` | `/api/sales-invoices/{id}/post` | creates the journal entry; returns invoice **+ journal entry** |
| `POST` | `/api/sales-invoices/{id}/reverse` | body: `reversalDate`, `reason`; 409 if payments allocated |
| `GET` | `/api/sales-invoices/{id}/journal-entry` | the entry created by posting |

### Customer payments / receipts
| Method | Route | Notes |
|---|---|---|
| `POST` | `/api/customer-payments` | `customerId`, `paymentDate`, `paymentMethodId`, `amount`, `allocations[]`; posts atomically |
| `GET` | `/api/customer-payments` | `customerId`, date range |
| `GET` | `/api/customer-payments/{id}` | with allocations |
| `POST` | `/api/customer-payments/{id}/reverse` | reverses the journal and releases the allocations |

### Supplier bills
| Method | Route |
|---|---|
| `POST` `GET` | `/api/supplier-bills` |
| `GET` `PUT` `DELETE` | `/api/supplier-bills/{id}` |
| `POST` | `/api/supplier-bills/{id}/post` |
| `POST` | `/api/supplier-bills/{id}/reverse` |
| `GET` | `/api/supplier-bills/{id}/journal-entry` |

### Supplier payments
| Method | Route |
|---|---|
| `POST` `GET` | `/api/supplier-payments` |
| `GET` | `/api/supplier-payments/{id}` |
| `POST` | `/api/supplier-payments/{id}/reverse` |

### Journal
| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/journal-entries` | `fromDate`, `toDate`, `sourceType`, `accountId` |
| `GET` | `/api/journal-entries/{id}` | header + lines |
| `POST` | `/api/journal-entries` | manual entry — ≥2 lines, must balance (**bonus, cut first**) |
| `POST` | `/api/journal-entries/{id}/reverse` | generic reversal |

### Reports (§5)
| Method | Route | Query |
|---|---|---|
| `GET` | `/api/reports/general-ledger` | `accountId` *(required)*, `fromDate`, `toDate` |
| `GET` | `/api/reports/trial-balance` | `asOfDate` |
| `GET` | `/api/reports/profit-and-loss` | `fromDate`, `toDate` |
| `GET` | `/api/reports/customer-outstanding` | `customerId` *(optional)*, `asOfDate` |
| `GET` | `/api/reports/supplier-outstanding` | `supplierId` *(optional)*, `asOfDate` |
| `GET` | `/api/reports/balance-sheet` | `asOfDate` — **bonus**, trivial once TB exists |

### Infrastructure
`GET /health` · `GET /swagger`

### Status code contract
| Code | When |
|---|---|
| 200 | successful read / update |
| 201 | created — always with `Location` |
| 204 | successful delete |
| 400 | validation failure — `ProblemDetails` with an `errors` dictionary |
| 404 | entity not found |
| 409 | duplicate code · posting an already-posted document · editing a posted document · payment exceeds outstanding · deleting an account that has journal lines |
| 422 | *(optional)* accounting rule violation, e.g. an unbalanced manual journal |
| 500 | unhandled — generic `ProblemDetails`, real detail to the log only |

Mapped centrally in `ExceptionHandlingMiddleware`:
`NotFoundException → 404`, `ValidationException → 400`, `ConflictException → 409`,
`UnbalancedJournalException → 422`, everything else → 500.

### Sample payloads

**`POST /api/sales-invoices`**
```json
{
  "customerId": 1,
  "invoiceDate": "2026-09-04",
  "dueDate": "2026-10-04",
  "notes": "Demo invoice",
  "lines": [
    { "description": "Trading goods", "quantity": 1, "unitPrice": 100000.00,
      "discountPercent": 0, "taxRatePercent": 18, "revenueAccountId": null }
  ]
}
```

**`POST /api/sales-invoices/1/post` → 200**
```json
{
  "invoice": {
    "salesInvoiceId": 1, "invoiceNumber": "INV-000001", "status": "Posted",
    "subTotal": 100000.00, "discountAmount": 0.00, "taxAmount": 18000.00,
    "grandTotal": 118000.00, "amountPaid": 0.00, "outstandingAmount": 118000.00
  },
  "journalEntry": {
    "journalEntryId": 2, "entryNumber": "JV-000002", "entryDate": "2026-09-04",
    "description": "Sales Invoice INV-000001 - XYZ Retail",
    "totalDebit": 118000.00, "totalCredit": 118000.00, "isBalanced": true,
    "lines": [
      { "accountCode": "1100", "accountName": "Accounts Receivable", "debit": 118000.00, "credit": 0.00 },
      { "accountCode": "4000", "accountName": "Sales Revenue",       "debit": 0.00, "credit": 100000.00 },
      { "accountCode": "2100", "accountName": "Tax Payable (Output)","debit": 0.00, "credit": 18000.00 }
    ]
  }
}
```

**`POST /api/customer-payments`**
```json
{
  "customerId": 1,
  "paymentDate": "2026-09-04",
  "paymentMethodId": 2,
  "referenceNo": "CHQ-77120",
  "amount": 50000.00,
  "allocations": [ { "salesInvoiceId": 1, "allocatedAmount": 50000.00 } ]
}
```

Always echo the journal entry in the post/payment response. The reviewer's §10 step 4 and step 5
are literally *"show the journal entry"* — make it impossible to miss.

---

## 8. Report SQL (§5 — *"efficient parameterised SQL queries executed through Dapper"*)

**Trial Balance** — one pass, grouped, with a signed balance derived from `NormalBalance`:

```sql
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
JOIN   dbo.Account     a  ON a.AccountId     = m.AccountId
JOIN   dbo.AccountType at ON at.AccountTypeId = a.AccountTypeId
WHERE  m.TotalDebit <> 0 OR m.TotalCredit <> 0
ORDER BY a.AccountCode;
```

The DTO adds `TotalDebitBalance`, `TotalCreditBalance` and a computed
`IsBalanced = TotalDebitBalance == TotalCreditBalance` — that boolean **is** §10 step 9. Put it
in the response body, not just in a screenshot.

**General Ledger with opening balance and running balance:**

```sql
DECLARE @Opening DECIMAL(18,2) = (
    SELECT ISNULL(SUM(jl.Debit - jl.Credit), 0)
    FROM   dbo.JournalEntryLine jl
    JOIN   dbo.JournalEntry     je ON je.JournalEntryId = jl.JournalEntryId
    WHERE  jl.AccountId = @AccountId AND je.IsPosted = 1 AND je.EntryDate < @FromDate);

SELECT je.EntryDate, je.EntryNumber, je.Description,
       jl.Debit, jl.Credit,
       @Opening + SUM(jl.Debit - jl.Credit) OVER (
           ORDER BY je.EntryDate, je.JournalEntryId, jl.JournalEntryLineId
           ROWS UNBOUNDED PRECEDING) AS RunningBalance,
       a.AccountId, a.AccountCode, a.AccountName          -- splitOn: AccountId
FROM   dbo.JournalEntryLine jl
JOIN   dbo.JournalEntry     je ON je.JournalEntryId = jl.JournalEntryId
JOIN   dbo.Account          a  ON a.AccountId       = jl.AccountId
WHERE  jl.AccountId = @AccountId
  AND  je.IsPosted  = 1
  AND  je.EntryDate BETWEEN @FromDate AND @ToDate
ORDER BY je.EntryDate, je.JournalEntryId, jl.JournalEntryLineId;
```

A window-function running balance rather than a client-side loop is a deliberate SQL-competence
signal. `@Opening` is returned as a header row so the ledger reconciles for any date range.

**Profit & Loss** — driven by `AccountType`, not by account codes:

```sql
SELECT at.Name AS Section,          -- Revenue | Expense
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
ORDER BY at.Name DESC, a.AccountCode;
```

Service shapes it into `{ revenue[], totalRevenue, expenses[], totalExpenses, netProfit }`.

**Customer Outstanding** — with ageing buckets, which costs ten extra lines and reads as real
ERP experience:

```sql
SELECT c.CustomerCode, c.Name,
       i.InvoiceNumber, i.InvoiceDate, i.DueDate, i.GrandTotal,
       ISNULL(p.Allocated, 0)                    AS AmountPaid,
       i.GrandTotal - ISNULL(p.Allocated, 0)     AS Outstanding,
       CASE WHEN DATEDIFF(DAY, ISNULL(i.DueDate, i.InvoiceDate), @AsOfDate) <= 0  THEN 'Current'
            WHEN DATEDIFF(DAY, ISNULL(i.DueDate, i.InvoiceDate), @AsOfDate) <= 30 THEN '1-30'
            WHEN DATEDIFF(DAY, ISNULL(i.DueDate, i.InvoiceDate), @AsOfDate) <= 60 THEN '31-60'
            WHEN DATEDIFF(DAY, ISNULL(i.DueDate, i.InvoiceDate), @AsOfDate) <= 90 THEN '61-90'
            ELSE '90+' END                       AS AgeingBucket
FROM   dbo.SalesInvoice i
JOIN   dbo.Customer     c ON c.CustomerId = i.CustomerId
OUTER APPLY (
    SELECT SUM(pa.AllocatedAmount) AS Allocated
    FROM   dbo.PaymentAllocation pa
    JOIN   dbo.Payment           pm ON pm.PaymentId = pa.PaymentId
    WHERE  pa.SalesInvoiceId = i.SalesInvoiceId
      AND  pm.Status = 2 AND pm.PaymentDate <= @AsOfDate
) p
WHERE  i.Status = 2
  AND  i.InvoiceDate <= @AsOfDate
  AND  (@CustomerId IS NULL OR i.CustomerId = @CustomerId)
ORDER BY c.CustomerCode, i.InvoiceDate
OPTION (RECOMPILE);
```

Supplier Outstanding is the same query against `SupplierBill` / `Supplier` with
`SupplierBillId`. Note the balance comes from **allocations**, not from a stored column — this
is §3B's *"traceable from transactions"* demonstrated rather than claimed.

---

## 9. Validation rules (§9)

| Rule | Response |
|---|---|
| `accountCode` unique, 1–20 chars | 409 |
| `accountTypeId` ∈ 1..5 | 400 |
| Invoice must have ≥ 1 line | 400 |
| `quantity > 0`, `unitPrice ≥ 0` | 400 |
| `discountPercent` and `taxRatePercent` ∈ 0..100 | 400 |
| Customer/supplier must exist and be active | 400 / 404 |
| Post a Draft only | 409 |
| Edit/delete a Posted document | 409 |
| `sum(allocations) ≤ payment.amount` | 400 |
| Each allocation ≤ that invoice's outstanding (checked under `UPDLOCK`) | 409 |
| Payment date ≥ invoice date | 400 |
| Manual journal must balance and have ≥ 2 lines | 422 |
| Reverse a Posted document only, and only if unallocated | 409 |

Any unallocated remainder on a receipt is an **advance/credit balance** — either reject it in v1
(simplest, and say so) or post it to a `2200 Customer Advances` liability. Pick one, write it in
the README, don't leave it silently undefined.

---

## 10. Demo scenario (§10) — exact expected numbers

`requests/demo.http` runs these nine steps in order. Rehearse it once before submitting.

| # | Action | Journal |
|---|---|---|
| 1 | `POST /api/customers` → C001 XYZ Retail | — |
| 2 | `POST /api/suppliers` → S001 ABC Suppliers | — |
| 3 | `POST /api/sales-invoices` — 1 × 100,000 @ 18% tax → GrandTotal 118,000, Draft | — |
| 4 | `POST /api/sales-invoices/1/post` | DR 1100 **118,000** · CR 4000 **100,000** · CR 2100 **18,000** |
| 5 | `POST /api/customer-payments` — 50,000 by Bank | DR 1020 **50,000** · CR 1100 **50,000** |
| 6 | `POST /api/supplier-bills` + `/post` — 60,000, no tax | DR 5000 **60,000** · CR 2000 **60,000** |
| 7 | `POST /api/supplier-payments` — 30,000 by Cash | DR 2000 **30,000** · CR 1010 **30,000** |
| 8 | `GET /api/reports/trial-balance` and `/profit-and-loss` | — |
| 9 | `isBalanced: true` in the trial balance response | — |

**Trial balance, without the optional opening entry:**

| Account | Debit | Credit |
|---|---|---|
| 1010 Cash in Hand | | 30,000 |
| 1020 Bank Account | 50,000 | |
| 1100 Accounts Receivable | 68,000 | |
| 2000 Accounts Payable | | 30,000 |
| 2100 Tax Payable (Output) | | 18,000 |
| 4000 Sales Revenue | | 100,000 |
| 5000 Purchases | 60,000 | |
| **Total** | **178,000** | **178,000** |

**With the optional opening entry** (DR Cash 100,000 · DR Bank 400,000 · CR Capital 500,000):

| Account | Debit | Credit |
|---|---|---|
| 1010 Cash in Hand | 70,000 | |
| 1020 Bank Account | 450,000 | |
| 1100 Accounts Receivable | 68,000 | |
| 2000 Accounts Payable | | 30,000 |
| 2100 Tax Payable (Output) | | 18,000 |
| 3000 Owner's Capital | | 500,000 |
| 4000 Sales Revenue | | 100,000 |
| 5000 Purchases | 60,000 | |
| **Total** | **648,000** | **648,000** |

**Profit & Loss, either way:** Revenue 100,000 − Expenses 60,000 = **Net Profit 40,000**.

The tax is *not* revenue (it sits in 2100, a liability owed to the authority) and the receipt
does *not* touch the P&L (it moves one asset to another). If the reviewer probes anything in the
demo, it will be one of those two. Have the sentence ready.

**Highest-value optional extra:** one xUnit integration test that runs all nine steps against a
test database and asserts `totalDebit == totalCredit`. It converts §10 step 9 from a screenshot
into a proof, and it takes about twenty minutes.

---

## 11. Written answers — the 35% that needs no code

`docs/accounting-answers.md`, 1–2 pages. Skeleton so you can write it fast:

- **Q1 — Invoice vs Receipt vs Journal Entry.** An invoice is a *source document* recognising
  revenue and creating a receivable at the point of sale (accrual basis). A receipt records the
  *settlement* of that receivable in cash — it changes the composition of assets, never revenue.
  A journal entry is the *underlying double-entry record*; invoices and receipts each generate
  one automatically, while a manual journal handles what has no source document (depreciation,
  accruals, corrections).
- **Q2 — How a sales invoice flows.** Debit AR (asset ↑, customer owes us), credit Sales Revenue
  (income ↑) and credit Tax Payable (liability ↑) for the collected tax. AR is a *control
  account*: its GL balance must always equal the sum of the customer sub-ledger. The GL is the
  aggregation from which the Trial Balance, then the P&L and Balance Sheet, are drawn.
- **Q3 — Preventing modification of a posted transaction.** Immutable-once-posted plus a
  reversing entry for corrections; append-only journal lines enforced by a trigger; status
  transitions Draft → Posted → Reversed only; a full audit log of who posted and reversed what
  and when; reversal linked to the original via `ReversesJournalEntryId` so both sides of the
  correction are visible forever. Editing history destroys auditability and breaks any period
  already reported on — the point of an audit trail is that it records what happened, including
  the mistakes.
- **Q4 — An ERP workflow.** Walk the order-to-cash cycle you have actually built: customer
  master → sales invoice (draft) → post (AR/Revenue/Tax) → receipt with allocation → AR ageing
  → period-end Trial Balance and P&L. Answer honestly about Sage/QuickBooks exposure; describing
  the workflow you implemented and *why* each step exists scores better than a vague claim.
- **Q5 — Trial Balance.** A period-end listing of every account's debit and credit balances.
  Debits must equal credits because every transaction records equal and opposite entries under
  the accounting equation (Assets = Liabilities + Equity). It is a necessary but *not sufficient*
  check — it catches one-sided postings and arithmetic errors, but not a correct-amount entry
  posted to the wrong account, a wholly omitted transaction, or a compensating error. Making
  that last point is what separates a real answer from a textbook one.

---

## 12. README checklist (§12)

- [ ] Prerequisites, `docker run` line for SQL Server 2022
- [ ] Run order: `01_schema.sql` → `02_seed.sql` → `03_demo_data.sql` *(optional)*
- [ ] Connection string in `appsettings.Development.json` + `dotnet user-secrets` note
- [ ] `dotnet run --project src/SsitAccounting.Api` → Swagger URL
- [ ] **Assumptions** — the section the brief explicitly asks for:
      single company · LKR only, no multi-currency · single simplified tax rate per line,
      no tax authority filing · discount netted against revenue (not a contra account) ·
      periodic inventory, supplier bills default to Purchases expense, no COGS or stock
      valuation · no authentication (out of scope, would be JWT) · no period-close/lock ·
      rounding to 2dp away from zero · optional opening-balance entry and why
- [ ] Endpoint table (lift §7 above)
- [ ] Double-entry rules table (lift §4 above)
- [ ] Demo walkthrough with the expected trial balance
- [ ] Screenshots: Swagger, a posted journal entry, the trial balance showing equal totals
- [ ] **Known gaps / incomplete functionality** — §13 requires you to *"clearly identify
      incomplete functionality."* An honest gap list scores better than a silent omission.

---

## 13. Eight-hour timeline

| Time | Work | Why here |
|---|---|---|
| 0:00–0:50 | `01_schema.sql` + `02_seed.sql`, run against a real DB | Everything downstream depends on it; a schema change at hour 5 is expensive |
| 0:50–1:20 | Solution scaffold, DI, `SqlConnectionFactory`, `UnitOfWork`, exception middleware, Swagger, `/health` | Establishes the pattern every later feature copies |
| 1:20–2:05 | Accounts + Customers + Suppliers CRUD | Proves the controller → service → repository stack end to end |
| 2:05–3:20 | **`JournalService`** + sales invoice create/get/post | The core 35%. Do not start this tired |
| 3:20–4:05 | Customer payment + allocations | |
| 4:05–4:45 | Supplier bill + supplier payment | Largely mirrors what already exists |
| 4:45–5:40 | Five reports | Pure SQL, no new plumbing |
| 5:40–6:10 | Reversal endpoints + immutability triggers | |
| 6:10–7:05 | README + `docs/accounting-answers.md` + `demo.http` | |
| 7:05–7:40 | Run the nine-step demo end to end, screenshots, fix what breaks | Something always breaks |
| 7:40–8:00 | Final commit, push, verify a clean clone builds, reply to the email | Buffer |

**If you fall behind, drop these in order:** balance sheet → manual journal entry → ageing
buckets → pagination → account hierarchy → soft-delete niceties → integration test.

**Never drop these:** balanced posting inside one transaction · parameterised Dapper ·
Trial Balance and P&L · the README assumptions list · the §11 answers.

Write the §11 answers *before* the last hour. They are worth more per minute than any remaining
code, and they are the one deliverable that cannot be rescued by a working API.

---

## 14. Two-minute pre-submission check

- [ ] Fresh clone → `01`+`02` scripts → `dotnet run` → Swagger loads
- [ ] Nine-step demo runs clean; trial balance returns `isBalanced: true`
- [ ] `grep` the solution for string-concatenated SQL — there must be none
- [ ] Every posting method passes `transaction:` on every Dapper call
- [ ] No `EntityFrameworkCore` package reference anywhere (§8 disqualifier)
- [ ] No `FLOAT` / `REAL` / `MONEY` column in the schema (§7 disqualifier)
- [ ] `appsettings.json` contains no real credentials
- [ ] README lists assumptions and known gaps
- [ ] `docs/accounting-answers.md` is committed
- [ ] Repository is **public**, and the reply email links to it

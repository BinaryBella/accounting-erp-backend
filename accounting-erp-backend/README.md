# AccountingERP — SSIT Practical Assessment

**Software Engineer – Accounting & ERP Systems.** ASP.NET Core Web API · Dapper · SQL Server. Backend & database only.

A lightweight accounting module for **ABC Trading (Pvt) Ltd**: chart of accounts, customers, suppliers,
sales invoices, supplier bills, customer receipts and supplier payments — every posted document
automatically generates a **balanced double-entry `JournalEntry`**, and the Trial Balance foots.

---

## Solution layout

```
AccountingERP.sln
├─ src/
│  ├─ AccountingERP.Api/            ASP.NET Core Web API — thin controllers, exception middleware, Program.cs
│  │   ├─ Controllers/             model-bind, call a service, map the status code
│  │   └─ Middleware/              ExceptionHandlingMiddleware → RFC 7807 ProblemDetails
│  ├─ AccountingERP.Application/    business logic — no SQL, no HTTP
│  │   ├─ Dtos/  Services/  Abstractions/  Validation/  Exceptions/
│  ├─ AccountingERP.Domain/         entities, enums, AccountMappingKeys / NumberSequenceKeys
│  └─ AccountingERP.Infrastructure/ Dapper only — SqlConnectionFactory, UnitOfWork, repositories
│      ├─ Repositories/
│      └─ Sql/                      parameterised SQL text as `const string`, one class per aggregate
├─ tests/  (xUnit scaffolding)
├─ db/
│  ├─ 01_schema.sql                 tables, PK/FK/CHECK/UNIQUE, indexes, TVP types, triggers
│  ├─ 02_seed.sql                   account types, chart of accounts, account mappings, payment methods, sequences
│  └─ 03_demo_data.sql              OPTIONAL opening-balance journal entry (see Assumptions)
├─ docs/
│  ├─ accounting-answers.md         §11 written answers  (pending — see Known gaps)
│  └─ screenshots/
├─ requests/demo.http               the §10 nine-step scenario, runnable in VS Code / Rider
└─ README.md
```

Dependency direction: `Api → Application → Domain`, `Infrastructure → Application, Domain`.
`Application` has **no** reference to `Microsoft.Data.SqlClient`; `Api` touches `Infrastructure`
only in `Program.cs` DI registration.

## Stack

| Component | Version | Notes |
|---|---|---|
| .NET | 8.0 (LTS) | |
| Dapper | 2.1.79 | the only data-access library — **no Entity Framework Core anywhere** |
| Microsoft.Data.SqlClient | 7.0.2 | not `System.Data.SqlClient` |
| Swashbuckle.AspNetCore | 10.x | Swagger / OpenAPI |
| FluentValidation | 12.x | request validation |
| SQL Server | 2019 / 2022 | |

---

## Prerequisites

- .NET 8 SDK
- SQL Server 2019/2022. With Docker:
  ```bash
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Str0ng!Passw0rd" \
    -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
  ```

## Database setup

Run the three scripts **in order** against a fresh database. Pass `sqlcmd -I` — the schema
has filtered indexes, which need `QUOTED_IDENTIFIER ON` for any DML; the scripts also set it
themselves, and SSMS / the application connection have it on by default.

```bash
sqlcmd -S localhost,1433 -U sa -P 'Str0ng!Passw0rd' -I -Q "CREATE DATABASE SsitAccountingDb"
sqlcmd -S localhost,1433 -U sa -P 'Str0ng!Passw0rd' -I -d SsitAccountingDb -i db/01_schema.sql
sqlcmd -S localhost,1433 -U sa -P 'Str0ng!Passw0rd' -I -d SsitAccountingDb -i db/02_seed.sql
sqlcmd -S localhost,1433 -U sa -P 'Str0ng!Passw0rd' -I -d SsitAccountingDb -i db/03_demo_data.sql   # optional
```

`01_schema.sql` and `02_seed.sql` are idempotent (each drops/clears its own objects first),
so they are safe to re-run while iterating. `03_demo_data.sql` guards against double-insert.

## Configuration & run

The API reads the connection string `ConnectionStrings:AccountingDb`.

```bash
# preferred: user-secrets (nothing sensitive committed)
dotnet user-secrets --project src/AccountingERP.Api \
  set "ConnectionStrings:AccountingDb" "Server=localhost,1433;Database=SsitAccountingDb;User Id=sa;Password=Str0ng!Passw0rd;TrustServerCertificate=True"

dotnet run --project src/AccountingERP.Api
```

`appsettings.Development.json` carries a working local default (Windows-auth /
`Trusted_Connection`), so on a machine with a local SQL instance you can just `dotnet run`.
`appsettings.json` contains **no credentials**.

- Swagger UI: `http://localhost:5006/swagger` (or `https://localhost:7165/swagger`)
- Health: `GET /health`

## Run the demo

Open **`requests/demo.http`** in VS Code (REST Client) or Rider and send the requests top to
bottom. It performs the brief's §10 scenario and ends with the Trial Balance and P&L.

---

## Double-entry rules (§4)

Every posted journal transaction satisfies **Total Debits = Total Credits**. Accounts are
resolved through `dbo.AccountMapping` by key — **no account-code literal appears in C#**.

| Document | Debit | Credit |
|---|---|---|
| **Sales Invoice** (`SourceType 1`) | Accounts Receivable = GrandTotal *(tagged CustomerId)* | each line's revenue account = `LineSubTotal − LineDiscount`; Tax Payable (Output) = TaxAmount *(if > 0)* |
| **Customer Receipt** (`SourceType 2`) | Cash / Bank (from `PaymentMethod.LedgerAccountId`) = Amount | Accounts Receivable = Amount *(tagged CustomerId)* |
| **Supplier Bill** (`SourceType 3`) | each line's debit account (Purchases / Inventory) = `LineSubTotal − LineDiscount`; Input Tax Receivable = TaxAmount *(if > 0)* | Accounts Payable = GrandTotal *(tagged SupplierId)* |
| **Supplier Payment** (`SourceType 4`) | Accounts Payable = Amount *(tagged SupplierId)* | Cash / Bank = Amount |

**Three lines of defence on `Total Debits = Total Credits`:**
1. `JournalService.PostAsync` sums the draft in memory and throws `UnbalancedJournalException` (→ **422**) before any write.
2. `CK_JournalEntry_Balanced` on the header (`TotalDebit = TotalCredit AND TotalDebit > 0`).
3. After inserting the lines, the header totals are re-derived with `UPDATE … SET TotalDebit = (SELECT SUM(...))` from the persisted rows, so constraint #2 is checked against reality, not an asserted number.

Plus `CK_JournalEntryLine_OneSided` (a line is a debit **or** a credit, never both) and the
append-only trigger `TR_JournalEntryLine_NoMutation` (journal lines cannot be UPDATEd or DELETEd —
`THROW 50010`).

## Immutability & correction (§3C)

- **Draft** documents are freely editable and deletable.
- **Posted** documents are immutable: `PUT` / `DELETE` return **409**. Enforced in the service
  *and* by `TR_SalesInvoice_LockPosted` / `TR_SupplierBill_LockPosted` (block financial-column
  changes on a posted row).
- **Correction** = `POST /{id}/reverse` with `{ reversalDate, reason }`. This posts a new
  `JournalEntry` with `IsReversal = 1`, `ReversesJournalEntryId` set and every debit/credit
  swapped; the document moves to `Reversed`. The original entry stays in the ledger forever —
  both sides of the correction remain visible. Reversing an invoice/bill is blocked (409) while
  a non-reversed payment is allocated to it; reverse the payment first (which restores the
  document's outstanding balance).
- Every create / post / reverse writes an `AuditLog` row inside the same transaction.

## API surface

Base `/api` · JSON · `ProblemDetails` on every error · Swagger at `/swagger`.

| Area | Endpoints |
|---|---|
| Chart of accounts | `GET/POST /accounts` · `GET/PUT/DELETE /accounts/{id}` · `GET /account-types` |
| Customers | `GET/POST /customers` · `GET/PUT/DELETE /customers/{id}` |
| Suppliers | `GET/POST /suppliers` · `GET/PUT/DELETE /suppliers/{id}` |
| Sales invoices | `GET/POST /sales-invoices` · `GET/PUT/DELETE /sales-invoices/{id}` · `POST /sales-invoices/{id}/post` · `POST /sales-invoices/{id}/reverse` · `GET /sales-invoices/{id}/journal-entry` |
| Customer receipts | `GET/POST /customer-payments` · `GET /customer-payments/{id}` · `POST /customer-payments/{id}/reverse` |
| Supplier bills | `GET/POST /supplier-bills` · `GET/PUT/DELETE /supplier-bills/{id}` · `POST /supplier-bills/{id}/post` · `POST /supplier-bills/{id}/reverse` · `GET /supplier-bills/{id}/journal-entry` |
| Supplier payments | `GET/POST /supplier-payments` · `GET /supplier-payments/{id}` · `POST /supplier-payments/{id}/reverse` |
| Payment methods | `GET /payment-methods` |
| Journal | `GET /journal-entries` · `GET /journal-entries/{id}` · `POST /journal-entries` *(manual)* · `POST /journal-entries/{id}/reverse` |
| Reports (§5) | `GET /reports/trial-balance` · `/profit-and-loss` · `/general-ledger` · `/customer-outstanding` · `/supplier-outstanding` |
| Infrastructure | `GET /health` · `GET /swagger` |

### Status codes

| Code | When |
|---|---|
| 200 / 201 / 204 | read or update / created (with `Location`) / delete |
| 400 | request validation failure (`ProblemDetails` with an `errors` dictionary) |
| 404 | entity not found |
| 409 | duplicate code · posting an already-posted document · editing/deleting a posted document · allocation exceeds outstanding · reversing while payments are allocated |
| 422 | a journal draft does not balance |
| 500 | unhandled — generic `ProblemDetails`, real detail to the log only |

## Reports (§5)

All five are efficient parameterised SQL run through Dapper (`Infrastructure/Sql/ReportSql.cs`):

- **Trial Balance** — one grouped pass over `JournalEntryLine`; signed `DebitBalance`/`CreditBalance` per account. The response's **`isBalanced`** boolean is §10 step 9.
- **Profit & Loss** — grouped by `AccountType` (`IsBalanceSheet = 0`), sign taken from `NormalBalance`. No account-code literal; shaped into `{ revenue[], totalRevenue, expenses[], totalExpenses, netProfit }`.
- **General Ledger** — `QueryMultiple`: (1) the account + its opening balance (movements before `fromDate`); (2) the period's lines with a **window-function running balance** (`SUM(...) OVER (ORDER BY ... ROWS UNBOUNDED PRECEDING)`).
- **Customer / Supplier Outstanding** — outstanding is derived from `PaymentAllocation` (reversed payments excluded), **not** from a stored balance column, with `DATEDIFF` ageing buckets (Current / 1‑30 / 31‑60 / 61‑90 / 90+).

## Dapper & database notes (§7, §8)

- **Dapper for every read and write.** No EF Core. Every SQL string is fully parameterised
  (`LIKE` search terms are wildcard-escaped, paired with `ESCAPE '\'`); nothing is concatenated
  with user input.
- **One transaction per posting.** `IUnitOfWork` holds a single `IDbConnection` + `IDbTransaction`
  per request; every repository call passes `_uow.Transaction`, so a posting's document header,
  lines, journal entry, journal lines and audit row all commit or roll back together. The
  document number is allocated inside the same transaction (`UPDATE dbo.NumberSequence WITH
  (UPDLOCK, ROWLOCK) … OUTPUT`), so a rolled-back post releases the number — gap-free.
- **Concurrency.** Payment allocation re-reads each target invoice/bill `WITH (UPDLOCK, ROWLOCK)`
  inside the transaction and re-checks `outstanding ≥ allocatedAmount`, so two concurrent
  receipts cannot jointly over-apply a document.
- **Mapping.** `QueryMultiple` for aggregates (invoice + lines + allocations; journal header +
  lines; list + count). Line collections are bulk-inserted through table-valued parameters
  (`dbo.JournalEntryLineTvp`, `dbo.SalesInvoiceLineTvp`, `dbo.SupplierBillLineTvp`) — one round
  trip.
- **SQL lives in the data layer.** Every query is a `const string` in `Infrastructure/Sql/*.cs`.
  Controllers and services contain zero SQL.
- **Money** is `DECIMAL(18,2)` (amounts) / `DECIMAL(18,4)` (unit price, quantity). **No
  `FLOAT` / `REAL` / `MONEY` column anywhere.** `DATETIME2(3)` UTC audit timestamps default to
  `SYSUTCDATETIME()`.
- **Integrity** is in the schema: PK/FK on every relationship, `CHECK` constraints (one-sided
  journal lines, `AmountPaid ≤ GrandTotal`, `Status IN (…)`, party-vs-type on payments),
  `UNIQUE` on all codes/numbers, a filtered unique index so a source document can have exactly
  one posting journal entry, and triggers for append-only journal lines and posted-document lock.

---

## Assumptions

The brief invites reasonable accounting assumptions; the ones made here are:

1. **Single company, single currency (LKR).** No multi-currency, no company dimension.
2. **Opening balances.** `03_demo_data.sql` posts one optional opening entry
   (`DR 1010 Cash 100,000 · DR 1020 Bank 400,000 · CR 3000 Owner's Capital 500,000`). It is
   *not* required by the brief, but without starting funds the demo leaves Cash with a credit
   balance, which reads like a bug. Skip the script and everything still works — the Trial
   Balance simply foots at a lower total.
3. **Tax** is a single simplified rate captured per invoice/bill line (`taxRatePercent`). Output
   tax on sales credits `2100 Tax Payable (Output)`; input tax on purchases debits
   `1210 Input Tax Receivable`. No tax-authority return / filing / netting.
4. **Discount** is a trade discount **netted against revenue** (revenue is credited with
   `LineSubTotal − LineDiscount`), not posted to a separate contra-revenue account. A
   `Discounts Allowed` contra account would be the alternative for settlement discounts.
5. **Rounding.** Each line amount is rounded to 2dp with `MidpointRounding.AwayFromZero`; header
   totals are the **sum of the already-rounded line values**, so a header can never disagree
   with its lines by a cent.
6. **Inventory is periodic.** A supplier-bill line defaults its debit to `5000 Purchases`
   (Expense); posting to `1200 Inventory` (Asset) is equally supported per line. No perpetual
   inventory, COGS recognition or stock valuation.
7. **Party balances are derived, never stored.** There is no `Customer.Balance` column;
   outstanding is computed from invoices/bills and their payment allocations. `SalesInvoice.AmountPaid` /
   `SupplierBill.AmountPaid` exist only as a maintained cache written inside the allocation
   transaction, with `CHECK (AmountPaid ≤ GrandTotal)` as the storage-layer backstop.
8. **Payments must be fully allocated.** A receipt/payment's `amount` must equal the sum of its
   allocations; unallocated (on-account / advance) money is rejected with 400 in this version.
9. **Document numbering.** `INV-`, `BILL-`, `RCT-`, `PAY-`, `JV-` numbers are allocated when the
   **draft** is created (the schema requires a non-null unique number). Deleting a draft can
   therefore leave a gap in the sequence. Journal (`JV-`) numbers are allocated at post time and
   are gap-free.
10. **No authentication / authorization.** Out of scope for the assessment; `CreatedBy` /
    `PerformedBy` fall back to `SUSER_SNAME()`. A real deployment would put JWT auth in front.
11. **No period close / lock.** Any date can be posted or reversed; there is no fiscal-period
    gate.
12. **Generic journal reversal** (`POST /journal-entries/{id}/reverse`) is restricted to
    `Manual` / `Opening` entries. Document-sourced entries must be reversed through their own
    document endpoint so the document's status stays consistent.

---

## Known gaps / incomplete functionality

- **§11 written answers (Q1–Q5).** `docs/accounting-answers.md` is not yet written.
- **Screenshots.** `docs/screenshots/` is empty; the demo is reproducible via `requests/demo.http`.
- **Balance Sheet** report — not implemented (trivial once Trial Balance exists; out of the §5 list).
- **Automated tests.** The xUnit projects are scaffolding only; the demo was verified manually
  end to end (`requests/demo.http`). The highest-value addition would be one integration test
  that runs the nine steps and asserts `TotalDebit == TotalCredit`.
- **Manual journal entry** endpoint accepts any active account with no chart-of-accounts
  policy checks beyond existence/active/one-sided.
- Multiple sales-invoice lines that share a revenue account are **grouped into a single journal
  line** at post time (cleaner ledger); the per-line detail is on the invoice, not the journal.

## Verified demo result

Running `requests/demo.http` against a fresh DB (schema + seed + opening entry) produces:

| Account | Debit | Credit |
|---|---|---|
| 1010 Cash in Hand | 70,000.00 | |
| 1020 Bank Account | 450,000.00 | |
| 1100 Accounts Receivable | 68,000.00 | |
| 2000 Accounts Payable | | 30,000.00 |
| 2100 Tax Payable (Output) | | 18,000.00 |
| 3000 Owner's Capital | | 500,000.00 |
| 4000 Sales Revenue | | 100,000.00 |
| 5000 Purchases | 60,000.00 | |
| **Total** | **648,000.00** | **648,000.00** |

`trial-balance` response → `"isBalanced": true`. `profit-and-loss` → Revenue 100,000 −
Expenses 60,000 = **Net Profit 40,000**.

# Database Setup Instructions — AccountingERP

## Overview

The AccountingERP backend uses **Microsoft SQL Server** for all data storage. This guide provides step-by-step instructions to set up the database schema, seed data, and optional demo data.

### What is included

- **01_schema.sql** — Creates the `AccountingERPDb` database and all objects (tables, constraints, indexes, triggers, table-valued parameters).
- **02_seed.sql** — Populates reference data: account types, chart of accounts, account mappings, payment methods, and document-number sequences.
- **03_demo_data.sql** — *Optional* — Adds opening-balance journal entry (Cash 100,000 + Bank 400,000 = Owner's Capital 500,000) for demo/trial-balance clarity.

All scripts are **idempotent** — safe to re-run. No separate `CREATE DATABASE` command is needed.

---

## Prerequisites

### 1. SQL Server Installation

You must have **SQL Server 2019, 2022, or later** installed. Choose one:

- **SQL Server Developer Edition** (free, full-featured) — [Download](https://www.microsoft.com/en-gb/sql-server/sql-server-downloads)
- **SQL Server Express Edition** (free, 10 GB database limit) — [Download](https://www.microsoft.com/en-gb/sql-server/sql-server-editions-express)
- **SQL Server Standard/Enterprise Edition** (paid)

### 2. Client Tools

Choose **one** of the following to run the SQL scripts:

#### Option A: `sqlcmd` Command-Line Tool (Recommended)

- Ships with SQL Server installation.
- Or download **SQL Server Command-Line Tools** separately.
- **Windows:** [Download here](https://learn.microsoft.com/en-us/sql/tools/sqlcmd-utility)

#### Option B: SQL Server Management Studio (SSMS)

- Full-featured GUI.
- [Download here](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)
- Free, works with any SQL Server edition.

#### Option C: Azure Data Studio

- Modern, lightweight alternative to SSMS.
- [Download here](https://learn.microsoft.com/en-us/azure-data-studio/download-azure-data-studio)

### 3. Docker Option (No Installation Required)

If you prefer a containerized SQL Server instance:

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Str0ng!Passw0rd" \
  -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

- **Server:** `localhost,1433`
- **Username:** `sa`
- **Password:** `Str0ng!Passw0rd`
- **Requires:** Docker Desktop

---

## Setup Instructions

### Method 1: Using `sqlcmd` (Command Line)

This is the fastest and most portable method.

#### Step 1: Navigate to Repository Root

```bash
cd "C:\Users\YourUsername\OneDrive\Desktop\Practical Technical Assessment\accounting-erp-backend"
```

Verify you can see the `db/` folder:

```bash
dir db
```

Expected output:

```
01_schema.sql
02_seed.sql
03_demo_data.sql
DATABASE_SETUP.md
README.md
```

#### Step 2A: Using Local SQL Server (Windows Authentication)

If SQL Server is installed locally and you are logged in with a Windows domain account:

```bash
sqlcmd -S localhost -E -I -i db/01_schema.sql
sqlcmd -S localhost -E -I -i db/02_seed.sql
sqlcmd -S localhost -E -I -i db/03_demo_data.sql   # optional
```

**Flag explanations:**
- `-S localhost` — Local server instance (alternatives: `.`, `.\SQLEXPRESS`, `(localdb)\MSSQLLocalDB`, `hostname,port`)
- `-E` — Use Windows (integrated) authentication
- `-I` — Set `QUOTED_IDENTIFIER ON` (required for filtered indexes)
- `-i db/01_schema.sql` — Input file

#### Step 2B: Using Docker or Remote SQL Server (SQL Authentication)

If using Docker or a remote instance with SA credentials:

```bash
sqlcmd -S localhost,1433 -U sa -P "Str0ng!Passw0rd" -I -i db/01_schema.sql
sqlcmd -S localhost,1433 -U sa -P "Str0ng!Passw0rd" -I -i db/02_seed.sql
sqlcmd -S localhost,1433 -U sa -P "Str0ng!Passw0rd" -I -i db/03_demo_data.sql   # optional
```

**Flag explanations:**
- `-S localhost,1433` — Server and port
- `-U sa` — SQL Server login username
- `-P "Str0ng!Passw0rd"` — Password (wrap in quotes if it contains spaces)
- `-I` — Set `QUOTED_IDENTIFIER ON` (required for filtered indexes)
- `-i db/01_schema.sql` — Input file

#### Step 2C: PowerShell One-Liner (Windows Authentication)

Run all three scripts sequentially in PowerShell:

```powershell
'01_schema.sql','02_seed.sql','03_demo_data.sql' | ForEach-Object {
  sqlcmd -S localhost -E -I -i "db/$_"
}
```

#### Step 3: Verify Success

After each script completes successfully, you should see:

```
(X rows affected)
```

or

```
(0 rows affected)  -- on verification SELECT
```

No errors should appear. Expected output (in order):
```
Schema created successfully.
Seed data loaded successfully.
Opening balance entry posted.
```

---

### Method 2: Using SQL Server Management Studio (SSMS)

#### Step 1: Open SSMS

1. Launch SQL Server Management Studio.
2. Connect to your SQL Server instance:
   - **Server name:** `localhost` (or `localhost,1433` for Docker)
   - **Authentication:** Windows or SQL Server (if using Docker, use `sa` / password)
   - Click **Connect**.

#### Step 2: Open and Run Scripts in Order

1. **File → Open → File** → Navigate to `db/01_schema.sql`
2. Click the **Execute** button (or press `F5`).
3. Wait for completion. Check the **Messages** tab for success.
4. Repeat for `db/02_seed.sql` and `db/03_demo_data.sql`.

#### Step 3: Verify the Database

In the **Object Explorer** pane:

1. Right-click **Databases** → **Refresh**.
2. Expand the **AccountingERPDb** database.
3. Verify:
   - **Tables** folder contains 16 tables (Accounts, Customers, Suppliers, SalesInvoice, etc.).
   - **Stored Procedures** folder contains any procedures (if added).
   - **Table Types** folder contains `SalesInvoiceLineTvp`, `SupplierBillLineTvp`, `JournalEntryLineTvp`.

---

### Method 3: Using Azure Data Studio

1. Open Azure Data Studio.
2. **File → Open Folder** → Select the repository root.
3. Right-click `db/01_schema.sql` → **Run** (or press `F5`).
4. Select your SQL Server connection from the dropdown.
5. Repeat for `02_seed.sql` and `03_demo_data.sql`.

---

## Connection Strings

Once the database is set up, configure your ASP.NET Core API with the appropriate connection string.

### Windows Authentication (Local SQL Server)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AccountingERPDb;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;"
  }
}
```

### SQL Authentication (Docker or Remote Server)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=AccountingERPDb;User Id=sa;Password=Str0ng!Passw0rd;Encrypt=true;TrustServerCertificate=false;"
  }
}
```

### Development/Testing (SQL Authentication, Minimal Encryption)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=AccountingERPDb;User Id=sa;Password=Str0ng!Passw0rd;Encrypt=false;TrustServerCertificate=true;"
  }
}
```

**Note:** Save these in `appsettings.Development.json` (development) or `appsettings.json` (production). See [ASP.NET Core Configuration](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration) for details.

---

## Verification

After running all three scripts, verify the setup:

### Quick Verification Query

Open SSMS/Azure Data Studio and run:

```sql
USE AccountingERPDb;

-- Check all schema objects created
SELECT (SELECT COUNT(*) FROM sys.tables)             AS Tables,        -- Expected: 16
       (SELECT COUNT(*) FROM sys.foreign_keys)       AS ForeignKeys,   -- Expected: 24
       (SELECT COUNT(*) FROM sys.check_constraints)  AS CheckConstraints, -- Expected: 32
       (SELECT COUNT(*) FROM sys.triggers)           AS Triggers,      -- Expected: 3
       (SELECT COUNT(*) FROM sys.table_types)        AS TvpTypes;      -- Expected: 3

-- Check reference data counts
SELECT (SELECT COUNT(*) FROM dbo.Account)         AS Accounts,       -- Expected: 14
       (SELECT COUNT(*) FROM dbo.AccountType)     AS AccountTypes,   -- Expected: 5
       (SELECT COUNT(*) FROM dbo.AccountMapping)  AS AccountMappings,-- Expected: 7
       (SELECT COUNT(*) FROM dbo.PaymentMethod)   AS PaymentMethods, -- Expected: 2
       (SELECT COUNT(*) FROM dbo.NumberSequence)  AS Sequences;      -- Expected: 5

-- Check opening balance entry (only if 03_demo_data.sql was run)
SELECT EntryNumber, TotalDebit, TotalCredit,
       CASE WHEN TotalDebit = TotalCredit THEN 'BALANCED' ELSE 'BROKEN' END AS Status
FROM   dbo.JournalEntry;
```

### Detailed Verification Query

Check all tables and seeded data:

```sql
USE AccountingERPDb;

-- Check table count
SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo';
-- Expected: 16

-- Check account count
SELECT COUNT(*) as AccountCount FROM dbo.Account;
-- Expected: 14 (after 02_seed.sql)

-- List chart of accounts with types
SELECT AccountCode, AccountName, (SELECT Name FROM dbo.AccountType WHERE AccountTypeId = dbo.Account.AccountTypeId) as Type
FROM dbo.Account
ORDER BY AccountCode;

-- List account mappings
SELECT MappingKey, a.AccountCode, a.AccountName, Description
FROM dbo.AccountMapping am
JOIN dbo.Account a ON am.AccountId = a.AccountId
ORDER BY MappingKey;

-- List payment methods
SELECT Name, a.AccountCode, a.AccountName
FROM dbo.PaymentMethod pm
JOIN dbo.Account a ON pm.LedgerAccountId = a.AccountId
ORDER BY Name;

-- List number sequences
SELECT SequenceKey, NextNumber, DocumentPrefix
FROM dbo.NumberSequence
ORDER BY SequenceKey;
```

### Using Command Line

```bash
sqlcmd -S localhost -E -I -Q "USE AccountingERPDb; SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo';"
```

Expected output: `16`

### Using SSMS/Azure Data Studio

1. Open a **New Query** window.
2. Run:

```sql
USE AccountingERPDb;

-- Check table count
SELECT COUNT(*) as TableCount FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo';

-- Expected: 16

-- Check account count
SELECT COUNT(*) as AccountCount FROM dbo.Account;

-- Expected: 14 (after 02_seed.sql)

-- Check chart of accounts
SELECT AccountCode, AccountName, (SELECT Name FROM dbo.AccountType WHERE AccountTypeId = dbo.Account.AccountTypeId) as Type
FROM dbo.Account
ORDER BY AccountCode;
```

---

## Troubleshooting

### Common Errors & Solutions

| Error Message | Cause | Solution |
|---|---|---|
| `CREATE INDEX failed … 'QUOTED_IDENTIFIER'` or error `1934` / `8624` | `QUOTED_IDENTIFIER` is OFF. | Add **`-I`** flag to `sqlcmd` command. In app connection, use `Microsoft.Data.SqlClient` (ON by default). |
| `Login failed for user 'sa'` | Incorrect password or no SQL login. | Verify Docker container password or use `-E` (Windows authentication). |
| `Cannot connect to server` | SQL Server is not running or incorrect server address. | Verify SQL Server service is running (Windows Services → `SQL Server (MSSQLSERVER)`). Check server name with `sqlcmd -S ?` |
| `Cannot open database "AccountingERPDb"` from 02/03 | `01_schema.sql` did not complete successfully. | Re-run `01_schema.sql` and check for earlier error messages. |
| `A network-related … error … server was not found` | Incorrect server name in `-S` flag. | Try `-S .` (local instance), `-S .\SQLEXPRESS`, `-S (localdb)\MSSQLLocalDB`, or `-S hostname,port` |
| `Timeout expired` | Database is busy or script is hanging. | Cancel and check if database was partially created. Try resetting (see Reset section below). |
| `Syntax error` or `Invalid SQL` | File encoding or line-ending issue. | Ensure files are UTF-8 encoded. Windows CRLF line endings are fine. Re-download if corruption suspected. |
| Port 1433 already in use (Docker) | Another SQL Server or container using port 1433. | Use different port: `-p 1434:1433` in Docker run, then connect to `localhost,1434` |
| `Database already exists` or `Object already exists` | Running scripts in wrong order or re-running without cleanup. | Expected — scripts are idempotent and safe to re-run. Continue or see Reset section. |

### Error: "Cannot connect to server"

- **Cause:** SQL Server is not running or incorrect server address.
- **Solution:**
  - Verify SQL Server service is running (Windows Services → `SQL Server (MSSQLSERVER)`).
  - If using Docker, ensure container is running: `docker ps`.
  - If using TCP/IP, verify network connectivity to the server.

### Error: "QUOTED_IDENTIFIER must be ON"

- **Cause:** `-I` flag was not used with `sqlcmd`.
- **Solution:** Always run `sqlcmd` with the `-I` flag:
  ```bash
  sqlcmd -S localhost -E -I -i db/01_schema.sql
  ```

### Error: "Login failed for user 'sa'"

- **Cause:** Incorrect password for Docker SQL Server.
- **Solution:** Verify the Docker container is running with the correct password, or use Windows authentication if available.

### Error: "Database already exists" or "Object already exists"

- **Cause:** Running scripts in the wrong order or running `01_schema.sql` twice without clearing the database.
- **Solution:** This is expected — scripts are idempotent and will drop/recreate objects safely. Re-run the scripts.

### Error: "Syntax error" or "Invalid SQL"

- **Cause:** File encoding or line-ending issue.
- **Solution:**
  - Ensure SQL script files are UTF-8 encoded.
  - On Windows, CRLF line endings are fine.
  - Re-download the files if corruption is suspected.

### Port 1433 Already in Use (Docker)

- **Cause:** Another SQL Server or Docker container is using port 1433.
- **Solution:**
  ```bash
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Str0ng!Passw0rd" \
    -p 1434:1433 -d mcr.microsoft.com/mssql/server:2022-latest
  ```
  Then use `-S localhost,1434` instead of `-S localhost,1433`.

---

## Reset Database

If you need to completely reset the database and start fresh, run these commands:

### Via SQL Query (SSMS / Azure Data Studio)

```sql
ALTER DATABASE AccountingERPDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
DROP DATABASE AccountingERPDb;
```

Then re-run the setup scripts:
```bash
sqlcmd -S localhost -E -I -i db/01_schema.sql
sqlcmd -S localhost -E -I -i db/02_seed.sql
sqlcmd -S localhost -E -I -i db/03_demo_data.sql
```

### Quick Rebuild (Without Dropping Database)

To rebuild all objects without dropping the database:

```bash
sqlcmd -S localhost -E -I -i db/01_schema.sql
```

This automatically drops and recreates all tables, indexes, triggers, and types.

---

## Configure the ASP.NET Core API

The API reads the connection string named **`ConnectionStrings:AccountingDb`**. Configure it using one of these methods:

### Option 1: User Secrets (Recommended for Development)

Store sensitive credentials in user secrets (not committed to git):

```bash
cd src/AccountingERP.Api

# For Windows authentication (local SQL Server)
dotnet user-secrets set "ConnectionStrings:AccountingDb" "Server=localhost;Database=AccountingERPDb;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;"

# For SQL authentication (Docker / remote)
dotnet user-secrets set "ConnectionStrings:AccountingDb" "Server=localhost,1433;Database=AccountingERPDb;User Id=sa;Password=Str0ng!Passw0rd;Encrypt=false;TrustServerCertificate=true;"
```

### Option 2: Environment Variable

Set an environment variable (PowerShell):

```powershell
$env:ConnectionStrings__AccountingDb = "Server=localhost,1433;Database=AccountingERPDb;User Id=sa;Password=Str0ng!Passw0rd;Encrypt=false;TrustServerCertificate=true;"
```

Or (Command Prompt):

```cmd
setx ConnectionStrings__AccountingDb "Server=localhost,1433;Database=AccountingERPDb;User Id=sa;Password=Str0ng!Passw0rd;Encrypt=false;TrustServerCertificate=true;"
```

### Option 3: Configuration Files

Edit `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "AccountingDb": "Server=localhost;Database=AccountingERPDb;Integrated Security=true;Encrypt=true;TrustServerCertificate=false;"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Note:** `appsettings.json` (production) should **not** contain connection strings or credentials.

### Run the API

```bash
cd accounting-erp-backend
dotnet build
dotnet run --project src/AccountingERP.Api
```

Navigate to **http://localhost:5006/swagger** (or the port shown in console) to explore APIs with Swagger UI.

---

## Database Schema Overview

### Core Tables

| Table | Purpose |
|-------|---------|
| `AccountType` | Reference: Asset, Liability, Equity, Revenue, Expense |
| `Account` | Chart of accounts (GL accounts) |
| `Customer` | Customer master data |
| `Supplier` | Supplier master data |
| `SalesInvoice` | Sales invoices to customers |
| `SalesInvoiceLine` | Line items on sales invoices |
| `SupplierBill` | Purchase invoices from suppliers |
| `SupplierBillLine` | Line items on supplier bills |
| `JournalEntry` | Posted accounting transactions |
| `JournalEntryLine` | Debit/credit lines of journal entries |
| `Payment` | Customer payments & supplier payments |
| `PaymentAllocation` | Allocation of payment to invoice/bill |
| `PaymentMethod` | Reference: Cash, Bank, etc. |
| `NumberSequence` | Document number sequencing (invoice, bill, JV, etc.) |
| `AuditLog` | Audit trail (optional; not currently used) |

### Key Constraints & Features

- **Primary Keys:** Every table has a PK (INT or TINYINT IDENTITY, or composite).
- **Foreign Keys:** 24 FKs ensure referential integrity.
- **CHECK Constraints:** 32 constraints validate domain rules (e.g., invoice status IN ('Draft', 'Posted')).
- **UNIQUE Constraints:** 15 constraints prevent duplicates (e.g., `AccountCode`, invoice numbers).
- **Filtered Indexes:** 3 indexes optimize queries on active/posted documents.
- **Triggers:** 3 triggers enforce business rules:
  - `TR_JournalEntryLine_NoMutation` — Posted journal lines are append-only.
  - `TR_SalesInvoice_LockPosted` — Posted sales invoices cannot be modified.
  - `TR_SupplierBill_LockPosted` — Posted supplier bills cannot be modified.

---

## Demo Scenario

After running all scripts, you can verify the setup with the following steps (matches §10 of the assessment):

```sql
USE AccountingERPDb;

-- 1. Check customer (will be empty; you'll create C001 via API)
SELECT * FROM dbo.Customer;

-- 2. Check supplier (will be empty; you'll create S001 via API)
SELECT * FROM dbo.Supplier;

-- 3. Check chart of accounts
SELECT AccountCode, AccountName FROM dbo.Account ORDER BY AccountCode;

-- 4. Check opening balance (if 03_demo_data.sql was run)
SELECT * FROM dbo.JournalEntry;
SELECT * FROM dbo.JournalEntryLine;

-- 5. Check trial balance
SELECT
    AccountCode,
    AccountName,
    SUM(CASE WHEN DebitAmount IS NOT NULL THEN DebitAmount ELSE 0 END) as TotalDebit,
    SUM(CASE WHEN CreditAmount IS NOT NULL THEN CreditAmount ELSE 0 END) as TotalCredit
FROM dbo.JournalEntryLine jel
JOIN dbo.Account a ON jel.AccountId = a.AccountId
GROUP BY AccountCode, AccountName
ORDER BY AccountCode;
```

---

## Next Steps

1. **Verify Setup:** Run the verification queries above.
2. **Configure API:** Update `appsettings.Development.json` with the connection string.
3. **Run API:** Build and run the ASP.NET Core application:
   ```bash
   cd accounting-erp-backend
   dotnet build
   dotnet run --project src/AccountingERP.Api
   ```
4. **Test APIs:** Navigate to `https://localhost:5001/swagger` to explore the API.
5. **Run Demo Scenario:** Use the API endpoints to create customers, suppliers, invoices, and payments as outlined in the assessment.

---

## Support

- **SQL Server Documentation:** [learn.microsoft.com/sql](https://learn.microsoft.com/en-us/sql)
- **Dapper Documentation:** [dapperlib.github.io](https://dapperlib.github.io)
- **ASP.NET Core Configuration:** [learn.microsoft.com/aspnet/core](https://learn.microsoft.com/en-us/aspnet/core)
- **Common SQL Server Issues:** See troubleshooting section above.

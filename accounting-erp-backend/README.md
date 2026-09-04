# AccountingERP — SSIT Practical Assessment

**Software Engineer – Accounting & ERP Systems**
ASP.NET Core Web API · Dapper · SQL Server. Backend & database only.

Double-entry posting engine: every posted document (sales invoice, supplier bill, customer
receipt, supplier payment) produces a balanced `JournalEntry`, and the Trial Balance foots.

---

## Solution layout

```
AccountingERP.sln
├─ src/
│  ├─ AccountingERP.Api/            ASP.NET Core Web API — thin controllers, middleware, Program.cs
│  │   ├─ Controllers/
│  │   └─ Middleware/
│  ├─ AccountingERP.Application/    business logic — no SQL, no HTTP
│  │   ├─ Dtos/  Services/  Abstractions/  Validation/
│  ├─ AccountingERP.Domain/         entities, enums, account-mapping keys
│  │   ├─ Entities/  Enums/
│  └─ AccountingERP.Infrastructure/ Dapper only — SqlConnectionFactory, UnitOfWork
│      ├─ Repositories/
│      └─ Sql/                      static parameterised SQL text, one class per aggregate
├─ tests/
│  ├─ AccountingERP.UnitTests/
│  └─ AccountingERP.IntegrationTests/
├─ db/
│  ├─ 01_schema.sql                 tables, constraints, indexes, TVP types, triggers
│  ├─ 02_seed.sql                   account types, chart of accounts, mappings, sequences
│  └─ 03_demo_data.sql              optional: demo customer/supplier, opening balances
├─ docs/
│  ├─ accounting-answers.md         §11 written answers
│  └─ screenshots/
├─ requests/demo.http               the §10 nine-step scenario
└─ README.md
```

Dependency direction: `Api → Application → Domain`, `Infrastructure → Application, Domain`.
`Application` has no reference to `Microsoft.Data.SqlClient`; `Api` touches `Infrastructure`
only in `Program.cs` DI registration.

## Stack

| Component | Version |
|---|---|
| .NET | 8.0 (LTS) |
| Dapper | 2.1.x |
| Microsoft.Data.SqlClient | 7.x |
| Swashbuckle.AspNetCore | 10.x |
| FluentValidation | 12.x |
| SQL Server | 2019 / 2022 |

## Prerequisites

- .NET 8 SDK
- SQL Server 2019/2022. Docker:
  ```bash
  docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=Str0ng!Passw0rd" \
    -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
  ```

## Run

1. Create the database and run, in order:
   `db/01_schema.sql` → `db/02_seed.sql` → `db/03_demo_data.sql` *(optional)*
2. Set the connection string (prefer user-secrets over committing it):
   ```bash
   dotnet user-secrets --project src/AccountingERP.Api \
     set "ConnectionStrings:AccountingDb" "Server=localhost,1433;Database=AccountingErp;User Id=sa;Password=Str0ng!Passw0rd;TrustServerCertificate=True"
   ```
   A working `appsettings.Development.json` default is included for local use.
3. `dotnet run --project src/AccountingERP.Api`
4. Swagger: `https://localhost:7165/swagger` · health: `https://localhost:7165/health`

---

## Assumptions

_To be completed — single company; LKR only; one tax rate per line; discount netted against
revenue; periodic inventory; no authentication; rounding 2dp away from zero; optional
opening-balance entry._

## Known gaps / incomplete functionality

_To be completed._

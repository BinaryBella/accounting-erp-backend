/* =====================================================================
   SSIT Practical Assessment — ABC Trading (Pvt) Ltd
   Database Schema (Microsoft SQL Server)

   This script creates the AccountingERPDb database if it does not exist,
   switches to it, then (re)creates every object. Just run it:
       sqlcmd -S localhost,1433 -U sa -P '<password>' -I -i 01_schema.sql

   Idempotent: safe to re-run — section 0 drops every object this script
   owns, in dependency order, before recreating it.
   ===================================================================== */

IF DB_ID('AccountingERPDb') IS NULL
    CREATE DATABASE AccountingERPDb;
GO

USE AccountingERPDb;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
-- Required so filtered indexes (UX_JournalEntry_Source, UX_PaymentAllocation_*, IX_Payment_*)
-- can be created. sqlcmd/ODBC default QUOTED_IDENTIFIER OFF, which makes CREATE INDEX fail.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------------------------------------------------------------------
   0. Clean slate (safe re-run during development)
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.TR_SupplierBill_LockPosted', 'TR')      IS NOT NULL DROP TRIGGER dbo.TR_SupplierBill_LockPosted;
IF OBJECT_ID('dbo.TR_SalesInvoice_LockPosted', 'TR')       IS NOT NULL DROP TRIGGER dbo.TR_SalesInvoice_LockPosted;
IF OBJECT_ID('dbo.TR_JournalEntryLine_NoMutation', 'TR')   IS NOT NULL DROP TRIGGER dbo.TR_JournalEntryLine_NoMutation;
GO

IF TYPE_ID('dbo.SupplierBillLineTvp')  IS NOT NULL DROP TYPE dbo.SupplierBillLineTvp;
IF TYPE_ID('dbo.SalesInvoiceLineTvp')  IS NOT NULL DROP TYPE dbo.SalesInvoiceLineTvp;
IF TYPE_ID('dbo.JournalEntryLineTvp')  IS NOT NULL DROP TYPE dbo.JournalEntryLineTvp;
GO

IF OBJECT_ID('dbo.AuditLog', 'U')            IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('dbo.PaymentAllocation', 'U')   IS NOT NULL DROP TABLE dbo.PaymentAllocation;
IF OBJECT_ID('dbo.Payment', 'U')             IS NOT NULL DROP TABLE dbo.Payment;
IF OBJECT_ID('dbo.SupplierBillLine', 'U')    IS NOT NULL DROP TABLE dbo.SupplierBillLine;
IF OBJECT_ID('dbo.SupplierBill', 'U')        IS NOT NULL DROP TABLE dbo.SupplierBill;
IF OBJECT_ID('dbo.SalesInvoiceLine', 'U')    IS NOT NULL DROP TABLE dbo.SalesInvoiceLine;
IF OBJECT_ID('dbo.SalesInvoice', 'U')        IS NOT NULL DROP TABLE dbo.SalesInvoice;
IF OBJECT_ID('dbo.JournalEntryLine', 'U')    IS NOT NULL DROP TABLE dbo.JournalEntryLine;
IF OBJECT_ID('dbo.JournalEntry', 'U')        IS NOT NULL DROP TABLE dbo.JournalEntry;
IF OBJECT_ID('dbo.NumberSequence', 'U')      IS NOT NULL DROP TABLE dbo.NumberSequence;
IF OBJECT_ID('dbo.Supplier', 'U')            IS NOT NULL DROP TABLE dbo.Supplier;
IF OBJECT_ID('dbo.Customer', 'U')            IS NOT NULL DROP TABLE dbo.Customer;
IF OBJECT_ID('dbo.PaymentMethod', 'U')       IS NOT NULL DROP TABLE dbo.PaymentMethod;
IF OBJECT_ID('dbo.AccountMapping', 'U')      IS NOT NULL DROP TABLE dbo.AccountMapping;
IF OBJECT_ID('dbo.Account', 'U')             IS NOT NULL DROP TABLE dbo.Account;
IF OBJECT_ID('dbo.AccountType', 'U')         IS NOT NULL DROP TABLE dbo.AccountType;
GO

/* ---------------------------------------------------------------------
   1. Reference tables
   --------------------------------------------------------------------- */
CREATE TABLE dbo.AccountType (
    AccountTypeId   TINYINT       NOT NULL CONSTRAINT PK_AccountType PRIMARY KEY,
    Name            NVARCHAR(20)  NOT NULL CONSTRAINT UQ_AccountType_Name UNIQUE,   -- Asset, Liability, Equity, Revenue, Expense
    NormalBalance   CHAR(1)       NOT NULL CONSTRAINT CK_AccountType_NormalBalance CHECK (NormalBalance IN ('D','C')),
    IsBalanceSheet  BIT           NOT NULL   -- 1 = Asset/Liability/Equity, 0 = Revenue/Expense
);
GO

CREATE TABLE dbo.Account (
    AccountId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Account PRIMARY KEY,
    AccountCode      NVARCHAR(20)  NOT NULL,
    AccountName      NVARCHAR(150) NOT NULL,
    AccountTypeId    TINYINT       NOT NULL CONSTRAINT FK_Account_AccountType REFERENCES dbo.AccountType(AccountTypeId),
    ParentAccountId  INT           NULL     CONSTRAINT FK_Account_Parent      REFERENCES dbo.Account(AccountId),
    IsActive         BIT           NOT NULL CONSTRAINT DF_Account_IsActive DEFAULT (1),
    IsSystem         BIT           NOT NULL CONSTRAINT DF_Account_IsSystem DEFAULT (0),
    CreatedAtUtc     DATETIME2(3)  NOT NULL CONSTRAINT DF_Account_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc     DATETIME2(3)  NULL,
    CONSTRAINT UQ_Account_AccountCode UNIQUE (AccountCode)   -- prevents duplicate account codes (§3A)
);
GO

CREATE TABLE dbo.AccountMapping (
    MappingKey   NVARCHAR(50)  NOT NULL CONSTRAINT PK_AccountMapping PRIMARY KEY,
    AccountId    INT           NOT NULL CONSTRAINT FK_AccountMapping_Account REFERENCES dbo.Account(AccountId),
    Description  NVARCHAR(200) NOT NULL
);
GO

CREATE TABLE dbo.PaymentMethod (
    PaymentMethodId  TINYINT      NOT NULL CONSTRAINT PK_PaymentMethod PRIMARY KEY,   -- 1 Cash, 2 Bank
    Name             NVARCHAR(30) NOT NULL CONSTRAINT UQ_PaymentMethod_Name UNIQUE,
    LedgerAccountId  INT          NOT NULL CONSTRAINT FK_PaymentMethod_Account REFERENCES dbo.Account(AccountId),
    IsActive         BIT          NOT NULL CONSTRAINT DF_PaymentMethod_IsActive DEFAULT (1)
);
GO

/* ---------------------------------------------------------------------
   2. Parties
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Customer (
    CustomerId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    CustomerCode   NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Customer_Code UNIQUE,
    Name           NVARCHAR(150) NOT NULL,
    ContactPerson  NVARCHAR(100) NULL,
    Email          NVARCHAR(150) NULL,
    Phone          NVARCHAR(30)  NULL,
    Address        NVARCHAR(300) NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Customer_IsActive DEFAULT (1),
    CreatedAtUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Customer_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc   DATETIME2(3)  NULL
);
GO

CREATE TABLE dbo.Supplier (
    SupplierId     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Supplier PRIMARY KEY,
    SupplierCode   NVARCHAR(20)  NOT NULL CONSTRAINT UQ_Supplier_Code UNIQUE,
    Name           NVARCHAR(150) NOT NULL,
    ContactPerson  NVARCHAR(100) NULL,
    Email          NVARCHAR(150) NULL,
    Phone          NVARCHAR(30)  NULL,
    Address        NVARCHAR(300) NULL,
    IsActive       BIT           NOT NULL CONSTRAINT DF_Supplier_IsActive DEFAULT (1),
    CreatedAtUtc   DATETIME2(3)  NOT NULL CONSTRAINT DF_Supplier_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc   DATETIME2(3)  NULL
);
GO

/* ---------------------------------------------------------------------
   3. Document numbering
   --------------------------------------------------------------------- */
CREATE TABLE dbo.NumberSequence (
    SequenceKey NVARCHAR(30) NOT NULL CONSTRAINT PK_NumberSequence PRIMARY KEY,   -- SalesInvoice, SupplierBill, Receipt, Payment, Journal
    Prefix      NVARCHAR(10) NOT NULL,
    NextNumber  INT          NOT NULL,
    PadLength   TINYINT      NOT NULL
);
GO

/* ---------------------------------------------------------------------
   4. Journal — created before the documents that reference it
   --------------------------------------------------------------------- */
CREATE TABLE dbo.JournalEntry (
    JournalEntryId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_JournalEntry PRIMARY KEY,
    EntryNumber     NVARCHAR(30)  NOT NULL CONSTRAINT UQ_JournalEntry_Number UNIQUE,
    EntryDate       DATE          NOT NULL,
    Description     NVARCHAR(300) NOT NULL,
    SourceType      TINYINT       NOT NULL,   -- 1 SalesInvoice 2 CustomerReceipt 3 SupplierBill 4 SupplierPayment 5 Manual 6 Opening
    SourceId        INT           NULL,       -- polymorphic; deliberately no FK (points at whichever table SourceType names)
    IsReversal      BIT           NOT NULL CONSTRAINT DF_JournalEntry_IsReversal DEFAULT (0),
    ReversesJournalEntryId INT    NULL CONSTRAINT FK_JournalEntry_Reverses REFERENCES dbo.JournalEntry(JournalEntryId),
    TotalDebit      DECIMAL(18,2) NOT NULL,
    TotalCredit     DECIMAL(18,2) NOT NULL,
    IsPosted        BIT           NOT NULL CONSTRAINT DF_JournalEntry_IsPosted DEFAULT (1),
    CreatedBy       NVARCHAR(100) NOT NULL CONSTRAINT DF_JournalEntry_CreatedBy DEFAULT (SUSER_SNAME()),
    CreatedAtUtc    DATETIME2(3)  NOT NULL CONSTRAINT DF_JournalEntry_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_JournalEntry_Balanced CHECK (TotalDebit = TotalCredit AND TotalDebit > 0),
    CONSTRAINT CK_JournalEntry_SourceType CHECK (SourceType BETWEEN 1 AND 6)
);
GO

-- One posting journal entry per source document, ever (reversals are separate rows, excluded here)
CREATE UNIQUE INDEX UX_JournalEntry_Source ON dbo.JournalEntry(SourceType, SourceId)
    WHERE SourceId IS NOT NULL AND IsReversal = 0;
GO

CREATE TABLE dbo.JournalEntryLine (
    JournalEntryLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_JournalEntryLine PRIMARY KEY,
    JournalEntryId  INT NOT NULL CONSTRAINT FK_JournalEntryLine_Entry REFERENCES dbo.JournalEntry(JournalEntryId),
    LineNumber      INT NOT NULL,
    AccountId       INT NOT NULL CONSTRAINT FK_JournalEntryLine_Account REFERENCES dbo.Account(AccountId),
    Debit           DECIMAL(18,2) NOT NULL CONSTRAINT DF_JournalEntryLine_Debit DEFAULT (0),
    Credit          DECIMAL(18,2) NOT NULL CONSTRAINT DF_JournalEntryLine_Credit DEFAULT (0),
    Description     NVARCHAR(300) NULL,
    CustomerId      INT NULL CONSTRAINT FK_JournalEntryLine_Customer REFERENCES dbo.Customer(CustomerId),
    SupplierId      INT NULL CONSTRAINT FK_JournalEntryLine_Supplier REFERENCES dbo.Supplier(SupplierId),
    CONSTRAINT UQ_JournalEntryLine_LineNumber UNIQUE (JournalEntryId, LineNumber),
    CONSTRAINT CK_JournalEntryLine_OneSided CHECK (
        (Debit > 0 AND Credit = 0) OR (Credit > 0 AND Debit = 0)),
    CONSTRAINT CK_JournalEntryLine_NonNegative CHECK (Debit >= 0 AND Credit >= 0)
);
GO

CREATE INDEX IX_JournalEntryLine_Account ON dbo.JournalEntryLine(AccountId) INCLUDE (JournalEntryId, Debit, Credit);
CREATE INDEX IX_JournalEntryLine_Entry   ON dbo.JournalEntryLine(JournalEntryId);
GO

/* ---------------------------------------------------------------------
   5. Sales invoice
   --------------------------------------------------------------------- */
CREATE TABLE dbo.SalesInvoice (
    SalesInvoiceId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesInvoice PRIMARY KEY,
    InvoiceNumber   NVARCHAR(30)  NOT NULL CONSTRAINT UQ_SalesInvoice_Number UNIQUE,
    CustomerId      INT           NOT NULL CONSTRAINT FK_SalesInvoice_Customer REFERENCES dbo.Customer(CustomerId),
    InvoiceDate     DATE          NOT NULL,
    DueDate         DATE          NULL,
    Status          TINYINT       NOT NULL CONSTRAINT DF_SalesInvoice_Status DEFAULT (1),   -- 1 Draft, 2 Posted, 3 Reversed
    SubTotal        DECIMAL(18,2) NOT NULL CONSTRAINT CK_SalesInvoice_SubTotal  CHECK (SubTotal >= 0),
    DiscountAmount  DECIMAL(18,2) NOT NULL CONSTRAINT CK_SalesInvoice_Discount  CHECK (DiscountAmount >= 0),
    TaxAmount       DECIMAL(18,2) NOT NULL CONSTRAINT CK_SalesInvoice_Tax       CHECK (TaxAmount >= 0),
    GrandTotal      DECIMAL(18,2) NOT NULL CONSTRAINT CK_SalesInvoice_Grand     CHECK (GrandTotal >= 0),
    AmountPaid      DECIMAL(18,2) NOT NULL CONSTRAINT DF_SalesInvoice_AmountPaid DEFAULT (0),
    Notes           NVARCHAR(500) NULL,
    JournalEntryId  INT           NULL CONSTRAINT FK_SalesInvoice_JournalEntry REFERENCES dbo.JournalEntry(JournalEntryId),
    PostedAtUtc     DATETIME2(3)  NULL,
    CreatedAtUtc    DATETIME2(3)  NOT NULL CONSTRAINT DF_SalesInvoice_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc    DATETIME2(3)  NULL,
    CONSTRAINT CK_SalesInvoice_Status CHECK (Status IN (1,2,3)),
    CONSTRAINT CK_SalesInvoice_Paid   CHECK (AmountPaid >= 0 AND AmountPaid <= GrandTotal),
    CONSTRAINT CK_SalesInvoice_Posted CHECK (Status <> 2 OR (JournalEntryId IS NOT NULL AND PostedAtUtc IS NOT NULL))
);
GO

CREATE INDEX IX_SalesInvoice_Customer ON dbo.SalesInvoice(CustomerId) INCLUDE (Status, GrandTotal, AmountPaid);
CREATE INDEX IX_SalesInvoice_Status   ON dbo.SalesInvoice(Status, InvoiceDate);
GO

CREATE TABLE dbo.SalesInvoiceLine (
    SalesInvoiceLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SalesInvoiceLine PRIMARY KEY,
    SalesInvoiceId  INT NOT NULL CONSTRAINT FK_SalesInvoiceLine_Invoice REFERENCES dbo.SalesInvoice(SalesInvoiceId) ON DELETE CASCADE,
    LineNumber      INT           NOT NULL,
    Description     NVARCHAR(250) NOT NULL,
    Quantity        DECIMAL(18,4) NOT NULL CONSTRAINT CK_SalesInvoiceLine_Qty   CHECK (Quantity > 0),
    UnitPrice       DECIMAL(18,4) NOT NULL CONSTRAINT CK_SalesInvoiceLine_Price CHECK (UnitPrice >= 0),
    DiscountPercent DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SalesInvoiceLine_DiscPct DEFAULT (0)
                                  CONSTRAINT CK_SalesInvoiceLine_DiscPct CHECK (DiscountPercent BETWEEN 0 AND 100),
    TaxRatePercent  DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SalesInvoiceLine_TaxPct DEFAULT (0)
                                  CONSTRAINT CK_SalesInvoiceLine_TaxPct CHECK (TaxRatePercent BETWEEN 0 AND 100),
    LineSubTotal    DECIMAL(18,2) NOT NULL,   -- Quantity * UnitPrice, computed and persisted by the service
    LineDiscount    DECIMAL(18,2) NOT NULL,   -- LineSubTotal * DiscountPercent / 100
    LineTax         DECIMAL(18,2) NOT NULL,   -- (LineSubTotal - LineDiscount) * TaxRatePercent / 100
    LineTotal       DECIMAL(18,2) NOT NULL,   -- LineSubTotal - LineDiscount + LineTax
    RevenueAccountId INT NOT NULL CONSTRAINT FK_SalesInvoiceLine_Revenue REFERENCES dbo.Account(AccountId),
    CONSTRAINT UQ_SalesInvoiceLine_LineNumber UNIQUE (SalesInvoiceId, LineNumber)
);
GO

/* ---------------------------------------------------------------------
   6. Supplier bill
   --------------------------------------------------------------------- */
CREATE TABLE dbo.SupplierBill (
    SupplierBillId  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupplierBill PRIMARY KEY,
    BillNumber      NVARCHAR(30)  NOT NULL CONSTRAINT UQ_SupplierBill_Number UNIQUE,
    SupplierId      INT           NOT NULL CONSTRAINT FK_SupplierBill_Supplier REFERENCES dbo.Supplier(SupplierId),
    BillDate        DATE          NOT NULL,
    DueDate         DATE          NULL,
    Status          TINYINT       NOT NULL CONSTRAINT DF_SupplierBill_Status DEFAULT (1),
    SubTotal        DECIMAL(18,2) NOT NULL CONSTRAINT CK_SupplierBill_SubTotal CHECK (SubTotal >= 0),
    DiscountAmount  DECIMAL(18,2) NOT NULL CONSTRAINT CK_SupplierBill_Discount CHECK (DiscountAmount >= 0),
    TaxAmount       DECIMAL(18,2) NOT NULL CONSTRAINT CK_SupplierBill_Tax      CHECK (TaxAmount >= 0),
    GrandTotal      DECIMAL(18,2) NOT NULL CONSTRAINT CK_SupplierBill_Grand    CHECK (GrandTotal >= 0),
    AmountPaid      DECIMAL(18,2) NOT NULL CONSTRAINT DF_SupplierBill_AmountPaid DEFAULT (0),
    Notes           NVARCHAR(500) NULL,
    JournalEntryId  INT           NULL CONSTRAINT FK_SupplierBill_JournalEntry REFERENCES dbo.JournalEntry(JournalEntryId),
    PostedAtUtc     DATETIME2(3)  NULL,
    CreatedAtUtc    DATETIME2(3)  NOT NULL CONSTRAINT DF_SupplierBill_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc    DATETIME2(3)  NULL,
    CONSTRAINT CK_SupplierBill_Status CHECK (Status IN (1,2,3)),
    CONSTRAINT CK_SupplierBill_Paid   CHECK (AmountPaid >= 0 AND AmountPaid <= GrandTotal),
    CONSTRAINT CK_SupplierBill_Posted CHECK (Status <> 2 OR (JournalEntryId IS NOT NULL AND PostedAtUtc IS NOT NULL))
);
GO

CREATE INDEX IX_SupplierBill_Supplier ON dbo.SupplierBill(SupplierId) INCLUDE (Status, GrandTotal, AmountPaid);
CREATE INDEX IX_SupplierBill_Status   ON dbo.SupplierBill(Status, BillDate);
GO

CREATE TABLE dbo.SupplierBillLine (
    SupplierBillLineId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SupplierBillLine PRIMARY KEY,
    SupplierBillId  INT NOT NULL CONSTRAINT FK_SupplierBillLine_Bill REFERENCES dbo.SupplierBill(SupplierBillId) ON DELETE CASCADE,
    LineNumber      INT           NOT NULL,
    Description     NVARCHAR(250) NOT NULL,
    Quantity        DECIMAL(18,4) NOT NULL CONSTRAINT CK_SupplierBillLine_Qty   CHECK (Quantity > 0),
    UnitPrice       DECIMAL(18,4) NOT NULL CONSTRAINT CK_SupplierBillLine_Price CHECK (UnitPrice >= 0),
    DiscountPercent DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SupplierBillLine_DiscPct DEFAULT (0)
                                  CONSTRAINT CK_SupplierBillLine_DiscPct CHECK (DiscountPercent BETWEEN 0 AND 100),
    TaxRatePercent  DECIMAL(9,4)  NOT NULL CONSTRAINT DF_SupplierBillLine_TaxPct DEFAULT (0)
                                  CONSTRAINT CK_SupplierBillLine_TaxPct CHECK (TaxRatePercent BETWEEN 0 AND 100),
    LineSubTotal    DECIMAL(18,2) NOT NULL,
    LineDiscount    DECIMAL(18,2) NOT NULL,
    LineTax         DECIMAL(18,2) NOT NULL,
    LineTotal       DECIMAL(18,2) NOT NULL,
    DebitAccountId  INT NOT NULL CONSTRAINT FK_SupplierBillLine_Debit REFERENCES dbo.Account(AccountId),  -- Purchases (5000) or Inventory (1200)
    CONSTRAINT UQ_SupplierBillLine_LineNumber UNIQUE (SupplierBillId, LineNumber)
);
GO

/* ---------------------------------------------------------------------
   7. Payments (customer receipts and supplier payments share one shape)
   --------------------------------------------------------------------- */
CREATE TABLE dbo.Payment (
    PaymentId       INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payment PRIMARY KEY,
    PaymentNumber   NVARCHAR(30) NOT NULL CONSTRAINT UQ_Payment_Number UNIQUE,
    PaymentType     TINYINT      NOT NULL,   -- 1 Customer Receipt, 2 Supplier Payment
    PaymentDate     DATE         NOT NULL,
    CustomerId      INT          NULL CONSTRAINT FK_Payment_Customer REFERENCES dbo.Customer(CustomerId),
    SupplierId      INT          NULL CONSTRAINT FK_Payment_Supplier REFERENCES dbo.Supplier(SupplierId),
    PaymentMethodId TINYINT      NOT NULL CONSTRAINT FK_Payment_Method REFERENCES dbo.PaymentMethod(PaymentMethodId),
    ReferenceNo     NVARCHAR(50) NULL,       -- cheque no / bank slip
    Amount          DECIMAL(18,2) NOT NULL CONSTRAINT CK_Payment_Amount CHECK (Amount > 0),
    Status          TINYINT      NOT NULL CONSTRAINT DF_Payment_Status DEFAULT (1),
    JournalEntryId  INT          NULL CONSTRAINT FK_Payment_JournalEntry REFERENCES dbo.JournalEntry(JournalEntryId),
    PostedAtUtc     DATETIME2(3) NULL,
    CreatedAtUtc    DATETIME2(3) NOT NULL CONSTRAINT DF_Payment_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT CK_Payment_Type  CHECK (PaymentType IN (1,2)),
    CONSTRAINT CK_Payment_Party CHECK (
        (PaymentType = 1 AND CustomerId IS NOT NULL AND SupplierId IS NULL) OR
        (PaymentType = 2 AND SupplierId IS NOT NULL AND CustomerId IS NULL))
);
GO

CREATE INDEX IX_Payment_Customer ON dbo.Payment(CustomerId) WHERE CustomerId IS NOT NULL;
CREATE INDEX IX_Payment_Supplier ON dbo.Payment(SupplierId) WHERE SupplierId IS NOT NULL;
GO

CREATE TABLE dbo.PaymentAllocation (
    PaymentAllocationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentAllocation PRIMARY KEY,
    PaymentId       INT NOT NULL CONSTRAINT FK_PaymentAllocation_Payment REFERENCES dbo.Payment(PaymentId) ON DELETE CASCADE,
    SalesInvoiceId  INT NULL CONSTRAINT FK_PaymentAllocation_Invoice REFERENCES dbo.SalesInvoice(SalesInvoiceId),
    SupplierBillId  INT NULL CONSTRAINT FK_PaymentAllocation_Bill    REFERENCES dbo.SupplierBill(SupplierBillId),
    AllocatedAmount DECIMAL(18,2) NOT NULL CONSTRAINT CK_PaymentAllocation_Amount CHECK (AllocatedAmount > 0),
    CONSTRAINT CK_PaymentAllocation_Target CHECK (
        (SalesInvoiceId IS NOT NULL AND SupplierBillId IS NULL) OR
        (SalesInvoiceId IS NULL AND SupplierBillId IS NOT NULL))
);
GO

CREATE UNIQUE INDEX UX_PaymentAllocation_Payment_Invoice ON dbo.PaymentAllocation(PaymentId, SalesInvoiceId) WHERE SalesInvoiceId IS NOT NULL;
CREATE UNIQUE INDEX UX_PaymentAllocation_Payment_Bill    ON dbo.PaymentAllocation(PaymentId, SupplierBillId) WHERE SupplierBillId IS NOT NULL;
CREATE INDEX IX_PaymentAllocation_Invoice ON dbo.PaymentAllocation(SalesInvoiceId) WHERE SalesInvoiceId IS NOT NULL;
CREATE INDEX IX_PaymentAllocation_Bill    ON dbo.PaymentAllocation(SupplierBillId) WHERE SupplierBillId IS NOT NULL;
GO

/* ---------------------------------------------------------------------
   8. Audit log
   --------------------------------------------------------------------- */
CREATE TABLE dbo.AuditLog (
    AuditLogId     BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
    EntityName     NVARCHAR(50)  NOT NULL,
    EntityId       INT           NOT NULL,
    Action         NVARCHAR(30)  NOT NULL,   -- Created, Updated, Posted, Reversed
    PerformedBy    NVARCHAR(100) NOT NULL,
    PerformedAtUtc DATETIME2(3)  NOT NULL CONSTRAINT DF_AuditLog_PerformedAtUtc DEFAULT (SYSUTCDATETIME()),
    DetailJson     NVARCHAR(MAX) NULL
);
GO

CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog(EntityName, EntityId);
GO

/* ---------------------------------------------------------------------
   9. Table-valued parameter types (clean Dapper bulk inserts)
   --------------------------------------------------------------------- */
CREATE TYPE dbo.JournalEntryLineTvp AS TABLE (
    LineNumber  INT           NOT NULL,
    AccountId   INT           NOT NULL,
    Debit       DECIMAL(18,2) NOT NULL,
    Credit      DECIMAL(18,2) NOT NULL,
    Description NVARCHAR(300) NULL,
    CustomerId  INT NULL,
    SupplierId  INT NULL
);
GO

CREATE TYPE dbo.SalesInvoiceLineTvp AS TABLE (
    LineNumber       INT           NOT NULL,
    Description      NVARCHAR(250) NOT NULL,
    Quantity         DECIMAL(18,4) NOT NULL,
    UnitPrice        DECIMAL(18,4) NOT NULL,
    DiscountPercent  DECIMAL(9,4)  NOT NULL,
    TaxRatePercent   DECIMAL(9,4)  NOT NULL,
    LineSubTotal     DECIMAL(18,2) NOT NULL,
    LineDiscount     DECIMAL(18,2) NOT NULL,
    LineTax          DECIMAL(18,2) NOT NULL,
    LineTotal        DECIMAL(18,2) NOT NULL,
    RevenueAccountId INT           NOT NULL
);
GO

CREATE TYPE dbo.SupplierBillLineTvp AS TABLE (
    LineNumber       INT           NOT NULL,
    Description      NVARCHAR(250) NOT NULL,
    Quantity         DECIMAL(18,4) NOT NULL,
    UnitPrice        DECIMAL(18,4) NOT NULL,
    DiscountPercent  DECIMAL(9,4)  NOT NULL,
    TaxRatePercent   DECIMAL(9,4)  NOT NULL,
    LineSubTotal     DECIMAL(18,2) NOT NULL,
    LineDiscount     DECIMAL(18,2) NOT NULL,
    LineTax          DECIMAL(18,2) NOT NULL,
    LineTotal        DECIMAL(18,2) NOT NULL,
    DebitAccountId   INT           NOT NULL
);
GO

/* ---------------------------------------------------------------------
   10. Immutability triggers (§3C — posted transactions are not freely editable)
   --------------------------------------------------------------------- */
CREATE TRIGGER dbo.TR_JournalEntryLine_NoMutation ON dbo.JournalEntryLine
INSTEAD OF UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 50010, 'Journal lines are append-only. Correct via a reversing entry.', 1;
END;
GO
-- Note: FK_JournalEntryLine_Entry has no ON DELETE CASCADE — SQL Server does not allow an
-- INSTEAD OF DELETE trigger on a table that is the target of a cascading delete. That is the
-- correct behaviour here anyway: journal entries and their lines are never deleted.

CREATE TRIGGER dbo.TR_SalesInvoice_LockPosted ON dbo.SalesInvoice
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN inserted i ON d.SalesInvoiceId = i.SalesInvoiceId
        WHERE d.Status = 2
          AND (  d.GrandTotal     <> i.GrandTotal
              OR d.SubTotal       <> i.SubTotal
              OR d.TaxAmount      <> i.TaxAmount
              OR d.DiscountAmount <> i.DiscountAmount
              OR d.CustomerId     <> i.CustomerId
              OR d.InvoiceDate    <> i.InvoiceDate)
    )
    BEGIN
        THROW 50011, 'A posted sales invoice cannot be modified. Use the reversal endpoint.', 1;
    END
END;
GO

CREATE TRIGGER dbo.TR_SupplierBill_LockPosted ON dbo.SupplierBill
AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (
        SELECT 1
        FROM deleted d
        JOIN inserted i ON d.SupplierBillId = i.SupplierBillId
        WHERE d.Status = 2
          AND (  d.GrandTotal     <> i.GrandTotal
              OR d.SubTotal       <> i.SubTotal
              OR d.TaxAmount      <> i.TaxAmount
              OR d.DiscountAmount <> i.DiscountAmount
              OR d.SupplierId     <> i.SupplierId
              OR d.BillDate       <> i.BillDate)
    )
    BEGIN
        THROW 50012, 'A posted supplier bill cannot be modified. Use the reversal endpoint.', 1;
    END
END;
GO

PRINT 'Schema created successfully.';
GO

namespace AccountingERP.Domain.Enums;

/// <summary>Mirrors dbo.AccountType.AccountTypeId from 02_seed.sql. Used for posting guards and report grouping.</summary>
public enum AccountCategory : byte
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

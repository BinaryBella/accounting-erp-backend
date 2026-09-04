namespace AccountingERP.Infrastructure.Sql;

/// <summary>
/// Every Account/AccountType query, as parameterised text. No SQL lives in a
/// controller or a service (PLAN §8).
/// </summary>
internal static class AccountSql
{
    private const string SelectResponseColumns = @"
    a.AccountId, a.AccountCode, a.AccountName, a.AccountTypeId,
    t.Name AS AccountTypeName, t.NormalBalance, t.IsBalanceSheet,
    a.ParentAccountId, a.IsActive, a.IsSystem, a.CreatedAtUtc, a.UpdatedAtUtc";

    public const string List = $@"
SELECT {SelectResponseColumns}
FROM   dbo.Account a
JOIN   dbo.AccountType t ON t.AccountTypeId = a.AccountTypeId
WHERE  (@AccountType IS NULL OR a.AccountTypeId = @AccountType)
  AND  (@IsActive    IS NULL OR a.IsActive     = @IsActive)
  AND  (@SearchLike  IS NULL OR a.AccountCode LIKE @SearchLike ESCAPE '\'
                              OR a.AccountName LIKE @SearchLike ESCAPE '\')
ORDER BY a.AccountCode
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.Account a
WHERE  (@AccountType IS NULL OR a.AccountTypeId = @AccountType)
  AND  (@IsActive    IS NULL OR a.IsActive     = @IsActive)
  AND  (@SearchLike  IS NULL OR a.AccountCode LIKE @SearchLike ESCAPE '\'
                              OR a.AccountName LIKE @SearchLike ESCAPE '\');";

    public const string GetById = $@"
SELECT {SelectResponseColumns}
FROM   dbo.Account a
JOIN   dbo.AccountType t ON t.AccountTypeId = a.AccountTypeId
WHERE  a.AccountId = @AccountId;";

    public const string GetEntityById = @"
SELECT AccountId, AccountCode, AccountName, AccountTypeId, ParentAccountId,
       IsActive, IsSystem, CreatedAtUtc, UpdatedAtUtc
FROM   dbo.Account
WHERE  AccountId = @AccountId;";

    public const string FindIdByCode = @"
SELECT AccountId FROM dbo.Account WHERE AccountCode = @AccountCode;";

    public const string Exists = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Account WHERE AccountId = @AccountId) THEN 1 ELSE 0 END;";

    public const string AccountTypeExists = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.AccountType WHERE AccountTypeId = @AccountTypeId) THEN 1 ELSE 0 END;";

    public const string HasJournalLines = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.JournalEntryLine WHERE AccountId = @AccountId) THEN 1 ELSE 0 END;";

    public const string Insert = @"
INSERT INTO dbo.Account (AccountCode, AccountName, AccountTypeId, ParentAccountId, IsActive, IsSystem)
OUTPUT INSERTED.AccountId
VALUES (@AccountCode, @AccountName, @AccountTypeId, @ParentAccountId, @IsActive, @IsSystem);";

    public const string Update = @"
UPDATE dbo.Account
SET    AccountCode     = @AccountCode,
       AccountName      = @AccountName,
       AccountTypeId    = @AccountTypeId,
       ParentAccountId  = @ParentAccountId,
       IsActive         = @IsActive,
       UpdatedAtUtc     = SYSUTCDATETIME()
WHERE  AccountId = @AccountId;";

    public const string SoftDelete = @"
UPDATE dbo.Account
SET    IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
WHERE  AccountId = @AccountId;";

    public const string ListTypes = @"
SELECT AccountTypeId, Name, NormalBalance, IsBalanceSheet
FROM   dbo.AccountType
ORDER BY AccountTypeId;";
}

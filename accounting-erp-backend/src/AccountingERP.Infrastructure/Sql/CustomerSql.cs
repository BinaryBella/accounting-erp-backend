namespace AccountingERP.Infrastructure.Sql;

internal static class CustomerSql
{
    private const string Columns = @"
    CustomerId, CustomerCode, Name, ContactPerson, Email, Phone, Address,
    IsActive, CreatedAtUtc, UpdatedAtUtc";

    public const string List = $@"
SELECT {Columns}
FROM   dbo.Customer
WHERE  (@IsActive   IS NULL OR IsActive = @IsActive)
  AND  (@SearchLike IS NULL OR CustomerCode LIKE @SearchLike ESCAPE '\'
                             OR Name         LIKE @SearchLike ESCAPE '\')
ORDER BY CustomerCode
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.Customer
WHERE  (@IsActive   IS NULL OR IsActive = @IsActive)
  AND  (@SearchLike IS NULL OR CustomerCode LIKE @SearchLike ESCAPE '\'
                             OR Name         LIKE @SearchLike ESCAPE '\');";

    public const string GetById = $@"
SELECT {Columns} FROM dbo.Customer WHERE CustomerId = @CustomerId;";

    public const string FindIdByCode = @"
SELECT CustomerId FROM dbo.Customer WHERE CustomerCode = @CustomerCode;";

    public const string Exists = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Customer WHERE CustomerId = @CustomerId) THEN 1 ELSE 0 END;";

    public const string HasTransactions = @"
SELECT CASE WHEN
    EXISTS (SELECT 1 FROM dbo.SalesInvoice WHERE CustomerId = @CustomerId)
 OR EXISTS (SELECT 1 FROM dbo.Payment      WHERE CustomerId = @CustomerId)
THEN 1 ELSE 0 END;";

    public const string Insert = @"
INSERT INTO dbo.Customer (CustomerCode, Name, ContactPerson, Email, Phone, Address, IsActive)
OUTPUT INSERTED.CustomerId
VALUES (@CustomerCode, @Name, @ContactPerson, @Email, @Phone, @Address, @IsActive);";

    public const string Update = @"
UPDATE dbo.Customer
SET    CustomerCode  = @CustomerCode,
       Name          = @Name,
       ContactPerson = @ContactPerson,
       Email         = @Email,
       Phone         = @Phone,
       Address       = @Address,
       IsActive      = @IsActive,
       UpdatedAtUtc  = SYSUTCDATETIME()
WHERE  CustomerId = @CustomerId;";

    public const string SoftDelete = @"
UPDATE dbo.Customer
SET    IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
WHERE  CustomerId = @CustomerId;";
}

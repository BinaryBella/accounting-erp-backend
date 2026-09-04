namespace AccountingERP.Infrastructure.Sql;

internal static class SupplierSql
{
    private const string Columns = @"
    SupplierId, SupplierCode, Name, ContactPerson, Email, Phone, Address,
    IsActive, CreatedAtUtc, UpdatedAtUtc";

    public const string List = $@"
SELECT {Columns}
FROM   dbo.Supplier
WHERE  (@IsActive   IS NULL OR IsActive = @IsActive)
  AND  (@SearchLike IS NULL OR SupplierCode LIKE @SearchLike ESCAPE '\'
                             OR Name         LIKE @SearchLike ESCAPE '\')
ORDER BY SupplierCode
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

SELECT COUNT(*)
FROM   dbo.Supplier
WHERE  (@IsActive   IS NULL OR IsActive = @IsActive)
  AND  (@SearchLike IS NULL OR SupplierCode LIKE @SearchLike ESCAPE '\'
                             OR Name         LIKE @SearchLike ESCAPE '\');";

    public const string GetById = $@"
SELECT {Columns} FROM dbo.Supplier WHERE SupplierId = @SupplierId;";

    public const string FindIdByCode = @"
SELECT SupplierId FROM dbo.Supplier WHERE SupplierCode = @SupplierCode;";

    public const string Exists = @"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Supplier WHERE SupplierId = @SupplierId) THEN 1 ELSE 0 END;";

    public const string HasTransactions = @"
SELECT CASE WHEN
    EXISTS (SELECT 1 FROM dbo.SupplierBill WHERE SupplierId = @SupplierId)
 OR EXISTS (SELECT 1 FROM dbo.Payment      WHERE SupplierId = @SupplierId)
THEN 1 ELSE 0 END;";

    public const string Insert = @"
INSERT INTO dbo.Supplier (SupplierCode, Name, ContactPerson, Email, Phone, Address, IsActive)
OUTPUT INSERTED.SupplierId
VALUES (@SupplierCode, @Name, @ContactPerson, @Email, @Phone, @Address, @IsActive);";

    public const string Update = @"
UPDATE dbo.Supplier
SET    SupplierCode  = @SupplierCode,
       Name          = @Name,
       ContactPerson = @ContactPerson,
       Email         = @Email,
       Phone         = @Phone,
       Address       = @Address,
       IsActive      = @IsActive,
       UpdatedAtUtc  = SYSUTCDATETIME()
WHERE  SupplierId = @SupplierId;";

    public const string SoftDelete = @"
UPDATE dbo.Supplier
SET    IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
WHERE  SupplierId = @SupplierId;";
}

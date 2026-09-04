namespace AccountingERP.Infrastructure.Sql;

internal static class PaymentMethodSql
{
    public const string List = @"
SELECT pm.PaymentMethodId, pm.Name,
       a.AccountCode AS LedgerAccountCode, a.AccountName AS LedgerAccountName,
       pm.IsActive
FROM   dbo.PaymentMethod pm
JOIN   dbo.Account a ON a.AccountId = pm.LedgerAccountId
ORDER BY pm.PaymentMethodId;";

    public const string GetById = @"
SELECT pm.PaymentMethodId, pm.Name, pm.LedgerAccountId, pm.IsActive
FROM   dbo.PaymentMethod pm
WHERE  pm.PaymentMethodId = @PaymentMethodId;";
}

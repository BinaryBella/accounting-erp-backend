namespace AccountingERP.Infrastructure.Sql;

internal static class AccountMappingSql
{
    public const string GetAll = "SELECT MappingKey, AccountId FROM dbo.AccountMapping;";
}

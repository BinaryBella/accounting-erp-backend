namespace AccountingERP.Infrastructure.Sql;

internal static class AuditSql
{
    public const string Insert = @"
INSERT INTO dbo.AuditLog (EntityName, EntityId, Action, PerformedBy, DetailJson)
VALUES (@EntityName, @EntityId, @Action, SUSER_SNAME(), @DetailJson);";
}

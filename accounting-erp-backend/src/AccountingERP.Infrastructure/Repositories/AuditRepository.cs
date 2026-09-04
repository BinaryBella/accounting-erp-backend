using AccountingERP.Application.Abstractions;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class AuditRepository : IAuditRepository
{
    private readonly IUnitOfWork _uow;

    public AuditRepository(IUnitOfWork uow) => _uow = uow;

    public Task WriteAsync(string entityName, int entityId, string action, string? detailJson = null) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            AuditSql.Insert,
            new { EntityName = entityName, EntityId = entityId, Action = action, DetailJson = detailJson },
            _uow.Transaction));
}

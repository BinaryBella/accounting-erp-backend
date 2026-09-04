namespace AccountingERP.Application.Abstractions;

/// <summary>Appends to dbo.AuditLog inside the current transaction (PLAN §2.11).</summary>
public interface IAuditRepository
{
    Task WriteAsync(string entityName, int entityId, string action, string? detailJson = null);
}

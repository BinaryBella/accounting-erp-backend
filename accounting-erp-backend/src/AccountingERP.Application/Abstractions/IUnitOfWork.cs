using System.Data;

namespace AccountingERP.Application.Abstractions;

/// <summary>
/// One database connection per request, with an optional ambient transaction.
/// Every repository call passes <see cref="Transaction"/> so a document's header,
/// lines, journal entry and audit row all enlist in a single <see cref="IDbTransaction"/>
/// (PLAN §8). Opened lazily, disposed by the DI container.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }

    IDbTransaction? Transaction { get; }

    void Begin(IsolationLevel level = IsolationLevel.ReadCommitted);

    void Commit();

    void Rollback();
}

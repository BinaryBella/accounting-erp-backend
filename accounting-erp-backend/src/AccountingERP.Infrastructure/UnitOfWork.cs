using System.Data;
using AccountingERP.Application.Abstractions;

namespace AccountingERP.Infrastructure;

/// <summary>
/// Scoped per request. Holds a single connection, opened on first use, and at most
/// one transaction at a time. Disposal rolls back any transaction still open, so a
/// request that throws before <see cref="Commit"/> leaves nothing partial behind.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly IDbConnection _connection;
    private IDbTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(ISqlConnectionFactory factory)
    {
        _connection = factory.Create();
    }

    public IDbConnection Connection
    {
        get
        {
            EnsureOpen();
            return _connection;
        }
    }

    public IDbTransaction? Transaction => _transaction;

    public void Begin(IsolationLevel level = IsolationLevel.ReadCommitted)
    {
        EnsureOpen();
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress on this unit of work.");

        _transaction = _connection.BeginTransaction(level);
    }

    public void Commit()
    {
        if (_transaction is null)
            throw new InvalidOperationException("There is no active transaction to commit.");

        try
        {
            _transaction.Commit();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    public void Rollback()
    {
        if (_transaction is null)
            return;

        try
        {
            _transaction.Rollback();
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    private void EnsureOpen()
    {
        if (_connection.State != ConnectionState.Open)
            _connection.Open();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_transaction is not null)
        {
            try { _transaction.Rollback(); } catch { /* connection already broken — nothing to save */ }
            _transaction.Dispose();
            _transaction = null;
        }

        _connection.Dispose();
        _disposed = true;
    }
}

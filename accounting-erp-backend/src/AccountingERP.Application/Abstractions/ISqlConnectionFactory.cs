using System.Data;

namespace AccountingERP.Application.Abstractions;

/// <summary>
/// Creates a new, closed <see cref="IDbConnection"/> to the accounting database.
/// The only place a connection string is read; repositories never see it.
/// </summary>
public interface ISqlConnectionFactory
{
    IDbConnection Create();
}

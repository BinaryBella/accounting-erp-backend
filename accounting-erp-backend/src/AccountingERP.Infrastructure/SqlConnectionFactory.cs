using System.Data;
using AccountingERP.Application.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AccountingERP.Infrastructure;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("AccountingDb")
            ?? throw new InvalidOperationException(
                "Connection string 'AccountingDb' is not configured. Set it in appsettings.Development.json "
                + "or via 'dotnet user-secrets set \"ConnectionStrings:AccountingDb\" \"<value>\"'.");
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}

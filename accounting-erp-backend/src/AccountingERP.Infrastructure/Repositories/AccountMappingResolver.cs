using AccountingERP.Application.Abstractions;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

/// <summary>
/// Singleton. Loads dbo.AccountMapping once and caches key → AccountId, so posting
/// resolves control accounts without a per-request round trip and without a code literal.
/// </summary>
public sealed class AccountMappingResolver : IAccountMappingResolver
{
    private readonly ISqlConnectionFactory _factory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile IReadOnlyDictionary<string, int>? _cache;

    public AccountMappingResolver(ISqlConnectionFactory factory) => _factory = factory;

    public async Task<int> ResolveAsync(string mappingKey)
    {
        var cache = _cache ?? await LoadAsync();
        if (!cache.TryGetValue(mappingKey, out var accountId))
            throw new InvalidOperationException(
                $"Account mapping '{mappingKey}' is not configured. Add a row to dbo.AccountMapping (see 02_seed.sql).");
        return accountId;
    }

    public void Invalidate() => _cache = null;

    private async Task<IReadOnlyDictionary<string, int>> LoadAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_cache is not null)
                return _cache;

            using var connection = _factory.Create();
            var rows = await connection.QueryAsync<AccountMappingRow>(AccountMappingSql.GetAll);
            _cache = rows.ToDictionary(r => r.MappingKey, r => r.AccountId, StringComparer.OrdinalIgnoreCase);
            return _cache;
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record AccountMappingRow(string MappingKey, int AccountId);
}

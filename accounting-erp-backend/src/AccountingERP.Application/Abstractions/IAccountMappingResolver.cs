namespace AccountingERP.Application.Abstractions;

/// <summary>
/// Resolves an account-mapping key (see <c>AccountMappingKeys</c>) to a concrete
/// AccountId via dbo.AccountMapping. Cached; <see cref="Invalidate"/> drops the cache
/// after a mapping row changes.
/// </summary>
public interface IAccountMappingResolver
{
    Task<int> ResolveAsync(string mappingKey);

    void Invalidate();
}

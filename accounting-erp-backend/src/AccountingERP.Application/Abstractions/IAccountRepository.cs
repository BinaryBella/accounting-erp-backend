using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Entities;

namespace AccountingERP.Application.Abstractions;

public interface IAccountRepository
{
    Task<PagedResult<AccountResponse>> ListAsync(AccountQuery query);

    Task<AccountResponse?> GetByIdAsync(int accountId);

    /// <summary>Entity view used for update/delete guard checks (needs IsSystem and the current code).</summary>
    Task<Account?> GetEntityByIdAsync(int accountId);

    /// <summary>The id of the account with this code, or null. Used to detect code collisions.</summary>
    Task<int?> FindIdByCodeAsync(string accountCode);

    Task<bool> ExistsAsync(int accountId);

    Task<bool> AccountTypeExistsAsync(byte accountTypeId);

    Task<bool> HasJournalLinesAsync(int accountId);

    Task<int> InsertAsync(Account account);

    Task UpdateAsync(Account account);

    Task SoftDeleteAsync(int accountId);

    Task<IReadOnlyList<AccountTypeResponse>> ListTypesAsync();
}

using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Entities;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly IUnitOfWork _uow;

    public AccountRepository(IUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<AccountResponse>> ListAsync(AccountQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new
        {
            query.AccountType,
            query.IsActive,
            SearchLike = SqlLike.Contains(query.Search),
            page.Skip,
            page.Take
        };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(AccountSql.List, parameters, _uow.Transaction));

        var items = (await grid.ReadAsync<AccountResponse>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<AccountResponse>(items, page.Page, page.PageSize, total);
    }

    public Task<AccountResponse?> GetByIdAsync(int accountId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<AccountResponse>(
            new CommandDefinition(AccountSql.GetById, new { AccountId = accountId }, _uow.Transaction));

    public Task<Account?> GetEntityByIdAsync(int accountId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<Account>(
            new CommandDefinition(AccountSql.GetEntityById, new { AccountId = accountId }, _uow.Transaction));

    public Task<int?> FindIdByCodeAsync(string accountCode) =>
        _uow.Connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(AccountSql.FindIdByCode, new { AccountCode = accountCode }, _uow.Transaction));

    public async Task<bool> ExistsAsync(int accountId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(AccountSql.Exists, new { AccountId = accountId }, _uow.Transaction));

    public async Task<bool> AccountTypeExistsAsync(byte accountTypeId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(AccountSql.AccountTypeExists, new { AccountTypeId = accountTypeId }, _uow.Transaction));

    public async Task<bool> HasJournalLinesAsync(int accountId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(AccountSql.HasJournalLines, new { AccountId = accountId }, _uow.Transaction));

    public Task<int> InsertAsync(Account account) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(AccountSql.Insert, new
        {
            account.AccountCode,
            account.AccountName,
            account.AccountTypeId,
            account.ParentAccountId,
            account.IsActive,
            account.IsSystem
        }, _uow.Transaction));

    public Task UpdateAsync(Account account) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(AccountSql.Update, new
        {
            account.AccountId,
            account.AccountCode,
            account.AccountName,
            account.AccountTypeId,
            account.ParentAccountId,
            account.IsActive
        }, _uow.Transaction));

    public Task SoftDeleteAsync(int accountId) =>
        _uow.Connection.ExecuteAsync(
            new CommandDefinition(AccountSql.SoftDelete, new { AccountId = accountId }, _uow.Transaction));

    public async Task<IReadOnlyList<AccountTypeResponse>> ListTypesAsync() =>
        (await _uow.Connection.QueryAsync<AccountTypeResponse>(
            new CommandDefinition(AccountSql.ListTypes, transaction: _uow.Transaction))).ToList();
}

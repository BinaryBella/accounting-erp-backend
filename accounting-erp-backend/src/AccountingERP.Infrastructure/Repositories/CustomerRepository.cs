using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Entities;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly IUnitOfWork _uow;

    public CustomerRepository(IUnitOfWork uow) => _uow = uow;

    public async Task<PagedResult<CustomerResponse>> ListAsync(PartyQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new
        {
            query.IsActive,
            SearchLike = SqlLike.Contains(query.Search),
            page.Skip,
            page.Take
        };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(CustomerSql.List, parameters, _uow.Transaction));

        var items = (await grid.ReadAsync<CustomerResponse>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        return new PagedResult<CustomerResponse>(items, page.Page, page.PageSize, total);
    }

    public Task<CustomerResponse?> GetByIdAsync(int customerId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<CustomerResponse>(
            new CommandDefinition(CustomerSql.GetById, new { CustomerId = customerId }, _uow.Transaction));

    public Task<Customer?> GetEntityByIdAsync(int customerId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<Customer>(
            new CommandDefinition(CustomerSql.GetById, new { CustomerId = customerId }, _uow.Transaction));

    public Task<int?> FindIdByCodeAsync(string customerCode) =>
        _uow.Connection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(CustomerSql.FindIdByCode, new { CustomerCode = customerCode }, _uow.Transaction));

    public async Task<bool> ExistsAsync(int customerId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(CustomerSql.Exists, new { CustomerId = customerId }, _uow.Transaction));

    public async Task<bool> HasTransactionsAsync(int customerId) =>
        await _uow.Connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(CustomerSql.HasTransactions, new { CustomerId = customerId }, _uow.Transaction));

    public Task<int> InsertAsync(Customer customer) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(CustomerSql.Insert, new
        {
            customer.CustomerCode,
            customer.Name,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.IsActive
        }, _uow.Transaction));

    public Task UpdateAsync(Customer customer) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(CustomerSql.Update, new
        {
            customer.CustomerId,
            customer.CustomerCode,
            customer.Name,
            customer.ContactPerson,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.IsActive
        }, _uow.Transaction));

    public Task SoftDeleteAsync(int customerId) =>
        _uow.Connection.ExecuteAsync(
            new CommandDefinition(CustomerSql.SoftDelete, new { CustomerId = customerId }, _uow.Transaction));
}

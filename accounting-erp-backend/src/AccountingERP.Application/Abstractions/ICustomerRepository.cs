using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Entities;

namespace AccountingERP.Application.Abstractions;

public interface ICustomerRepository
{
    Task<PagedResult<CustomerResponse>> ListAsync(PartyQuery query);

    Task<CustomerResponse?> GetByIdAsync(int customerId);

    Task<Customer?> GetEntityByIdAsync(int customerId);

    Task<int?> FindIdByCodeAsync(string customerCode);

    Task<bool> ExistsAsync(int customerId);

    /// <summary>True if any sales invoice or payment references this customer.</summary>
    Task<bool> HasTransactionsAsync(int customerId);

    Task<int> InsertAsync(Customer customer);

    Task UpdateAsync(Customer customer);

    Task SoftDeleteAsync(int customerId);
}

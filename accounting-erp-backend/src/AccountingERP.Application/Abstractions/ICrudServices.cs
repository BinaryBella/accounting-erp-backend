using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface IAccountService
{
    Task<PagedResult<AccountResponse>> ListAsync(AccountQuery query);
    Task<AccountResponse> GetAsync(int id);
    Task<AccountResponse> CreateAsync(CreateAccountRequest request);
    Task<AccountResponse> UpdateAsync(int id, UpdateAccountRequest request);
    Task DeleteAsync(int id);
    Task<IReadOnlyList<AccountTypeResponse>> ListTypesAsync();
}

public interface ICustomerService
{
    Task<PagedResult<CustomerResponse>> ListAsync(PartyQuery query);
    Task<CustomerResponse> GetAsync(int id);
    Task<CustomerResponse> CreateAsync(CreateCustomerRequest request);
    Task<CustomerResponse> UpdateAsync(int id, UpdateCustomerRequest request);
    Task DeleteAsync(int id);
}

public interface ISupplierService
{
    Task<PagedResult<SupplierResponse>> ListAsync(PartyQuery query);
    Task<SupplierResponse> GetAsync(int id);
    Task<SupplierResponse> CreateAsync(CreateSupplierRequest request);
    Task<SupplierResponse> UpdateAsync(int id, UpdateSupplierRequest request);
    Task DeleteAsync(int id);
}

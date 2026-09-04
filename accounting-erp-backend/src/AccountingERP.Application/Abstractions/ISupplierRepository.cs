using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Entities;

namespace AccountingERP.Application.Abstractions;

public interface ISupplierRepository
{
    Task<PagedResult<SupplierResponse>> ListAsync(PartyQuery query);

    Task<SupplierResponse?> GetByIdAsync(int supplierId);

    Task<Supplier?> GetEntityByIdAsync(int supplierId);

    Task<int?> FindIdByCodeAsync(string supplierCode);

    Task<bool> ExistsAsync(int supplierId);

    /// <summary>True if any supplier bill or payment references this supplier.</summary>
    Task<bool> HasTransactionsAsync(int supplierId);

    Task<int> InsertAsync(Supplier supplier);

    Task UpdateAsync(Supplier supplier);

    Task SoftDeleteAsync(int supplierId);
}

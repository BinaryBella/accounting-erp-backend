using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ISupplierBillRepository
{
    Task<int> InsertHeaderAsync(SupplierBillHeaderInsert header);

    Task InsertLinesAsync(int supplierBillId, IReadOnlyList<SupplierBillLineComputed> lines);

    Task DeleteLinesAsync(int supplierBillId);

    Task UpdateHeaderAsync(SupplierBillHeaderUpdate header);

    Task<SupplierBillResponse?> GetByIdAsync(int supplierBillId);

    Task<PagedResult<SupplierBillListItem>> ListAsync(SupplierBillQuery query);

    /// <summary>Reads the bill <c>WITH (UPDLOCK, ROWLOCK)</c> plus its lines, for posting.</summary>
    Task<SupplierBillPostView?> GetForPostAsync(int supplierBillId);

    Task<SupplierBillGuard?> GetGuardAsync(int supplierBillId);

    Task MarkPostedAsync(int supplierBillId, int journalEntryId);

    Task DeleteAsync(int supplierBillId);
}

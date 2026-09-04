using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ISupplierBillService
{
    Task<PagedResult<SupplierBillListItem>> ListAsync(SupplierBillQuery query);

    Task<SupplierBillResponse> GetAsync(int id);

    Task<SupplierBillResponse> CreateAsync(SupplierBillWriteRequest request);

    Task<SupplierBillResponse> UpdateAsync(int id, SupplierBillWriteRequest request);

    Task DeleteAsync(int id);

    /// <summary>Posts the draft: builds the §4C journal (DR Purchases/Inventory + Input Tax / CR AP) and commits it with the status change and audit row in one transaction.</summary>
    Task<PostSupplierBillResult> PostAsync(int id);

    /// <summary>Reverses a posted bill: mirror journal entry, Status → Reversed, audit row. 409 if payments are allocated.</summary>
    Task<ReverseSupplierBillResult> ReverseAsync(int id, ReverseRequest request);

    Task<JournalEntryResponse> GetJournalEntryAsync(int id);
}

using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ISalesInvoiceService
{
    Task<PagedResult<SalesInvoiceListItem>> ListAsync(SalesInvoiceQuery query);

    Task<SalesInvoiceResponse> GetAsync(int id);

    Task<SalesInvoiceResponse> CreateAsync(SalesInvoiceWriteRequest request);

    Task<SalesInvoiceResponse> UpdateAsync(int id, SalesInvoiceWriteRequest request);

    Task DeleteAsync(int id);

    /// <summary>Posts the draft: builds the §4A journal (DR AR / CR Revenue / CR Tax) and commits it with the status change and audit row in one transaction.</summary>
    Task<PostSalesInvoiceResult> PostAsync(int id);

    /// <summary>Reverses a posted invoice: mirror journal entry, Status → Reversed, audit row. 409 if payments are allocated.</summary>
    Task<ReverseSalesInvoiceResult> ReverseAsync(int id, ReverseRequest request);

    Task<JournalEntryResponse> GetJournalEntryAsync(int id);
}

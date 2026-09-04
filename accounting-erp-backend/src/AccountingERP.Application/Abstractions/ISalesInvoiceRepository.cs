using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ISalesInvoiceRepository
{
    Task<int> InsertHeaderAsync(SalesInvoiceHeaderInsert header);

    Task InsertLinesAsync(int salesInvoiceId, IReadOnlyList<SalesInvoiceLineComputed> lines);

    Task DeleteLinesAsync(int salesInvoiceId);

    Task UpdateHeaderAsync(SalesInvoiceHeaderUpdate header);

    Task<SalesInvoiceResponse?> GetByIdAsync(int salesInvoiceId);

    Task<PagedResult<SalesInvoiceListItem>> ListAsync(SalesInvoiceQuery query);

    /// <summary>Reads the invoice <c>WITH (UPDLOCK, ROWLOCK)</c> plus its lines, for posting.</summary>
    Task<SalesInvoicePostView?> GetForPostAsync(int salesInvoiceId);

    Task<SalesInvoiceGuard?> GetGuardAsync(int salesInvoiceId);

    Task MarkPostedAsync(int salesInvoiceId, int journalEntryId);

    Task DeleteAsync(int salesInvoiceId);
}

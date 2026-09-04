using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ICustomerPaymentRepository
{
    Task<int> InsertAsync(CustomerPaymentInsert payment);

    Task InsertAllocationAsync(int paymentId, int salesInvoiceId, decimal allocatedAmount);

    /// <summary>Reads the invoice <c>WITH (UPDLOCK, ROWLOCK)</c> so two concurrent receipts cannot jointly over-apply it (PLAN §4).</summary>
    Task<InvoiceAllocationTarget?> LockInvoiceAsync(int salesInvoiceId);

    /// <summary>Maintained cache: <c>AmountPaid += delta</c>, written in the same transaction as the allocation. CK_SalesInvoice_Paid is the storage-layer backstop.</summary>
    Task AddInvoicePaidAmountAsync(int salesInvoiceId, decimal delta);

    Task MarkPostedAsync(int paymentId, int journalEntryId);

    Task<CustomerPaymentResponse?> GetByIdAsync(int paymentId);

    Task<PagedResult<CustomerPaymentListItem>> ListAsync(CustomerPaymentQuery query);
}

using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ISupplierPaymentRepository
{
    Task<int> InsertAsync(SupplierPaymentInsert payment);

    Task InsertAllocationAsync(int paymentId, int supplierBillId, decimal allocatedAmount);

    /// <summary>Reads the bill <c>WITH (UPDLOCK, ROWLOCK)</c> so two concurrent payments cannot jointly over-apply it.</summary>
    Task<BillAllocationTarget?> LockBillAsync(int supplierBillId);

    /// <summary>Maintained cache: <c>AmountPaid += delta</c>, written in the same transaction as the allocation.</summary>
    Task AddBillPaidAmountAsync(int supplierBillId, decimal delta);

    Task MarkPostedAsync(int paymentId, int journalEntryId);

    Task MarkReversedAsync(int paymentId);

    Task<SupplierPaymentReverseInfo?> GetForReverseAsync(int paymentId);

    Task<SupplierPaymentResponse?> GetByIdAsync(int paymentId);

    Task<PagedResult<SupplierPaymentListItem>> ListAsync(SupplierPaymentQuery query);
}

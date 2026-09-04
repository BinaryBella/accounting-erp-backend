using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ISupplierPaymentService
{
    Task<PagedResult<SupplierPaymentListItem>> ListAsync(SupplierPaymentQuery query);

    Task<SupplierPaymentResponse> GetAsync(int id);

    /// <summary>Records a supplier payment and posts it atomically: allocations under UPDLOCK, journal DR AP / CR Cash-Bank, bill AmountPaid cache bumped — all in one transaction (§4D).</summary>
    Task<PostSupplierPaymentResult> CreateAsync(CreateSupplierPaymentRequest request);

    /// <summary>Reverses a posted payment: mirror journal entry, releases the allocations (bill AmountPaid restored), Status → Reversed.</summary>
    Task<ReverseSupplierPaymentResult> ReverseAsync(int id, ReverseRequest request);
}

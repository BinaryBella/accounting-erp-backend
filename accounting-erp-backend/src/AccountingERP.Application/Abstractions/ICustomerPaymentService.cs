using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface ICustomerPaymentService
{
    Task<PagedResult<CustomerPaymentListItem>> ListAsync(CustomerPaymentQuery query);

    Task<CustomerPaymentResponse> GetAsync(int id);

    /// <summary>Records a receipt and posts it atomically: allocations under UPDLOCK, journal DR Cash/Bank / CR AR, AmountPaid cache bumped — all in one transaction (§4B).</summary>
    Task<PostCustomerPaymentResult> CreateAsync(CreateCustomerPaymentRequest request);

    /// <summary>Reverses a posted receipt: mirror journal entry, releases the allocations (invoice AmountPaid restored), Status → Reversed.</summary>
    Task<ReverseCustomerPaymentResult> ReverseAsync(int id, ReverseRequest request);
}

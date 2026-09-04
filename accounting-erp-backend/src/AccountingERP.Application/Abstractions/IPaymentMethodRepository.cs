using AccountingERP.Application.Dtos;

namespace AccountingERP.Application.Abstractions;

public interface IPaymentMethodRepository
{
    Task<IReadOnlyList<PaymentMethodResponse>> ListAsync();

    Task<PaymentMethodInfo?> GetByIdAsync(byte paymentMethodId);
}

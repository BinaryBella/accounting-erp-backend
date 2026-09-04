using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly IUnitOfWork _uow;

    public PaymentMethodRepository(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<PaymentMethodResponse>> ListAsync() =>
        (await _uow.Connection.QueryAsync<PaymentMethodResponse>(
            new CommandDefinition(PaymentMethodSql.List, transaction: _uow.Transaction))).ToList();

    public Task<PaymentMethodInfo?> GetByIdAsync(byte paymentMethodId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<PaymentMethodInfo>(new CommandDefinition(
            PaymentMethodSql.GetById, new { PaymentMethodId = paymentMethodId }, _uow.Transaction));
}

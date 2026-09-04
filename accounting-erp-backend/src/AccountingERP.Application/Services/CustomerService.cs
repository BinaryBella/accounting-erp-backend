using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Entities;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _customers;
    private readonly IValidator<CreateCustomerRequest> _createValidator;
    private readonly IValidator<UpdateCustomerRequest> _updateValidator;

    public CustomerService(
        ICustomerRepository customers,
        IValidator<CreateCustomerRequest> createValidator,
        IValidator<UpdateCustomerRequest> updateValidator)
    {
        _customers = customers;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public Task<PagedResult<CustomerResponse>> ListAsync(PartyQuery query) => _customers.ListAsync(query);

    public async Task<CustomerResponse> GetAsync(int id)
        => await _customers.GetByIdAsync(id) ?? throw new NotFoundException("Customer", id);

    public async Task<CustomerResponse> CreateAsync(CreateCustomerRequest request)
    {
        await _createValidator.EnsureValidAsync(request);

        var code = request.CustomerCode.Trim();
        if (await _customers.FindIdByCodeAsync(code) is not null)
            throw new ConflictException($"A customer with code '{code}' already exists.");

        var id = await _customers.InsertAsync(new Customer
        {
            CustomerCode = code,
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = true
        });

        return await _customers.GetByIdAsync(id)
               ?? throw new InvalidOperationException("Customer disappeared immediately after insert.");
    }

    public async Task<CustomerResponse> UpdateAsync(int id, UpdateCustomerRequest request)
    {
        await _updateValidator.EnsureValidAsync(request);

        var existing = await _customers.GetEntityByIdAsync(id)
                       ?? throw new NotFoundException("Customer", id);

        var code = request.CustomerCode.Trim();
        var idForCode = await _customers.FindIdByCodeAsync(code);
        if (idForCode is not null && idForCode != id)
            throw new ConflictException($"A customer with code '{code}' already exists.");

        existing.CustomerCode = code;
        existing.Name = request.Name.Trim();
        existing.ContactPerson = request.ContactPerson?.Trim();
        existing.Email = request.Email?.Trim();
        existing.Phone = request.Phone?.Trim();
        existing.Address = request.Address?.Trim();
        existing.IsActive = request.IsActive;

        await _customers.UpdateAsync(existing);

        return await _customers.GetByIdAsync(id)
               ?? throw new InvalidOperationException("Customer disappeared immediately after update.");
    }

    public async Task DeleteAsync(int id)
    {
        _ = await _customers.GetEntityByIdAsync(id) ?? throw new NotFoundException("Customer", id);

        if (await _customers.HasTransactionsAsync(id))
            throw new ConflictException("This customer has transactions and cannot be deleted. Deactivate it instead.");

        await _customers.SoftDeleteAsync(id);
    }
}

using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Entities;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _suppliers;
    private readonly IValidator<CreateSupplierRequest> _createValidator;
    private readonly IValidator<UpdateSupplierRequest> _updateValidator;

    public SupplierService(
        ISupplierRepository suppliers,
        IValidator<CreateSupplierRequest> createValidator,
        IValidator<UpdateSupplierRequest> updateValidator)
    {
        _suppliers = suppliers;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public Task<PagedResult<SupplierResponse>> ListAsync(PartyQuery query) => _suppliers.ListAsync(query);

    public async Task<SupplierResponse> GetAsync(int id)
        => await _suppliers.GetByIdAsync(id) ?? throw new NotFoundException("Supplier", id);

    public async Task<SupplierResponse> CreateAsync(CreateSupplierRequest request)
    {
        await _createValidator.EnsureValidAsync(request);

        var code = request.SupplierCode.Trim();
        if (await _suppliers.FindIdByCodeAsync(code) is not null)
            throw new ConflictException($"A supplier with code '{code}' already exists.");

        var id = await _suppliers.InsertAsync(new Supplier
        {
            SupplierCode = code,
            Name = request.Name.Trim(),
            ContactPerson = request.ContactPerson?.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = true
        });

        return await _suppliers.GetByIdAsync(id)
               ?? throw new InvalidOperationException("Supplier disappeared immediately after insert.");
    }

    public async Task<SupplierResponse> UpdateAsync(int id, UpdateSupplierRequest request)
    {
        await _updateValidator.EnsureValidAsync(request);

        var existing = await _suppliers.GetEntityByIdAsync(id)
                       ?? throw new NotFoundException("Supplier", id);

        var code = request.SupplierCode.Trim();
        var idForCode = await _suppliers.FindIdByCodeAsync(code);
        if (idForCode is not null && idForCode != id)
            throw new ConflictException($"A supplier with code '{code}' already exists.");

        existing.SupplierCode = code;
        existing.Name = request.Name.Trim();
        existing.ContactPerson = request.ContactPerson?.Trim();
        existing.Email = request.Email?.Trim();
        existing.Phone = request.Phone?.Trim();
        existing.Address = request.Address?.Trim();
        existing.IsActive = request.IsActive;

        await _suppliers.UpdateAsync(existing);

        return await _suppliers.GetByIdAsync(id)
               ?? throw new InvalidOperationException("Supplier disappeared immediately after update.");
    }

    public async Task DeleteAsync(int id)
    {
        _ = await _suppliers.GetEntityByIdAsync(id) ?? throw new NotFoundException("Supplier", id);

        if (await _suppliers.HasTransactionsAsync(id))
            throw new ConflictException("This supplier has transactions and cannot be deleted. Deactivate it instead.");

        await _suppliers.SoftDeleteAsync(id);
    }
}

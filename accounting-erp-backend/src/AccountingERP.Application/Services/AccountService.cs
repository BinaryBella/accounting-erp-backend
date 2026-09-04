using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Entities;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _accounts;
    private readonly IValidator<CreateAccountRequest> _createValidator;
    private readonly IValidator<UpdateAccountRequest> _updateValidator;

    public AccountService(
        IAccountRepository accounts,
        IValidator<CreateAccountRequest> createValidator,
        IValidator<UpdateAccountRequest> updateValidator)
    {
        _accounts = accounts;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public Task<PagedResult<AccountResponse>> ListAsync(AccountQuery query) => _accounts.ListAsync(query);

    public Task<IReadOnlyList<AccountTypeResponse>> ListTypesAsync() => _accounts.ListTypesAsync();

    public async Task<AccountResponse> GetAsync(int id)
        => await _accounts.GetByIdAsync(id) ?? throw new NotFoundException("Account", id);

    public async Task<AccountResponse> CreateAsync(CreateAccountRequest request)
    {
        await _createValidator.EnsureValidAsync(request);

        var code = request.AccountCode.Trim();

        if (!await _accounts.AccountTypeExistsAsync(request.AccountTypeId))
            throw new ValidationException(nameof(request.AccountTypeId), $"Account type {request.AccountTypeId} does not exist.");

        if (await _accounts.FindIdByCodeAsync(code) is not null)
            throw new ConflictException($"An account with code '{code}' already exists.");

        if (request.ParentAccountId is { } parentId && !await _accounts.ExistsAsync(parentId))
            throw new ValidationException(nameof(request.ParentAccountId), $"Parent account {parentId} does not exist.");

        var id = await _accounts.InsertAsync(new Account
        {
            AccountCode = code,
            AccountName = request.AccountName.Trim(),
            AccountTypeId = request.AccountTypeId,
            ParentAccountId = request.ParentAccountId,
            IsActive = true,
            IsSystem = false
        });

        return await _accounts.GetByIdAsync(id)
               ?? throw new InvalidOperationException("Account disappeared immediately after insert.");
    }

    public async Task<AccountResponse> UpdateAsync(int id, UpdateAccountRequest request)
    {
        await _updateValidator.EnsureValidAsync(request);

        var existing = await _accounts.GetEntityByIdAsync(id)
                       ?? throw new NotFoundException("Account", id);

        var code = request.AccountCode.Trim();

        if (!await _accounts.AccountTypeExistsAsync(request.AccountTypeId))
            throw new ValidationException(nameof(request.AccountTypeId), $"Account type {request.AccountTypeId} does not exist.");

        var idForCode = await _accounts.FindIdByCodeAsync(code);
        if (idForCode is not null && idForCode != id)
            throw new ConflictException($"An account with code '{code}' already exists.");

        if (existing.IsSystem && !request.IsActive)
            throw new ConflictException("A system account cannot be deactivated.");

        if (request.ParentAccountId is { } parentId)
        {
            if (parentId == id)
                throw new ValidationException(nameof(request.ParentAccountId), "An account cannot be its own parent.");
            if (!await _accounts.ExistsAsync(parentId))
                throw new ValidationException(nameof(request.ParentAccountId), $"Parent account {parentId} does not exist.");
        }

        existing.AccountCode = code;
        existing.AccountName = request.AccountName.Trim();
        existing.AccountTypeId = request.AccountTypeId;
        existing.ParentAccountId = request.ParentAccountId;
        existing.IsActive = request.IsActive;

        await _accounts.UpdateAsync(existing);

        return await _accounts.GetByIdAsync(id)
               ?? throw new InvalidOperationException("Account disappeared immediately after update.");
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _accounts.GetEntityByIdAsync(id)
                       ?? throw new NotFoundException("Account", id);

        if (existing.IsSystem)
            throw new ConflictException("A system account cannot be deleted.");

        if (await _accounts.HasJournalLinesAsync(id))
            throw new ConflictException("This account has journal lines and cannot be deleted. Deactivate it instead.");

        await _accounts.SoftDeleteAsync(id);
    }
}

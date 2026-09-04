using AccountingERP.Application.Dtos;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    public CreateAccountRequestValidator()
    {
        RuleFor(x => x.AccountCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AccountTypeId)
            .InclusiveBetween((byte)1, (byte)5)
            .WithMessage("AccountTypeId must be between 1 (Asset) and 5 (Expense).");
        RuleFor(x => x.ParentAccountId).GreaterThan(0).When(x => x.ParentAccountId.HasValue);
    }
}

public sealed class UpdateAccountRequestValidator : AbstractValidator<UpdateAccountRequest>
{
    public UpdateAccountRequestValidator()
    {
        RuleFor(x => x.AccountCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.AccountName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.AccountTypeId)
            .InclusiveBetween((byte)1, (byte)5)
            .WithMessage("AccountTypeId must be between 1 (Asset) and 5 (Expense).");
        RuleFor(x => x.ParentAccountId).GreaterThan(0).When(x => x.ParentAccountId.HasValue);
    }
}

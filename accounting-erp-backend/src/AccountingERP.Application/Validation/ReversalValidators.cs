using AccountingERP.Application.Dtos;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public sealed class ReverseRequestValidator : AbstractValidator<ReverseRequest>
{
    public ReverseRequestValidator()
    {
        RuleFor(x => x.ReversalDate).NotEqual(default(DateOnly)).WithMessage("ReversalDate is required.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300).WithMessage("A reason for the reversal is required.");
    }
}

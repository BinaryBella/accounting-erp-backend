using AccountingERP.Application.Dtos;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public sealed class SupplierBillWriteRequestValidator : AbstractValidator<SupplierBillWriteRequest>
{
    public SupplierBillWriteRequestValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);

        RuleFor(x => x.BillDate)
            .NotEqual(default(DateOnly)).WithMessage("BillDate is required.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.BillDate)
            .When(x => x.DueDate.HasValue)
            .WithMessage("DueDate cannot be before BillDate.");

        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.Lines)
            .NotNull()
            .Must(lines => lines is { Count: >= 1 })
            .WithMessage("A bill needs at least one line.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(250);
            line.RuleFor(l => l.Quantity).GreaterThan(0m);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m);
            line.RuleFor(l => l.DiscountPercent).InclusiveBetween(0m, 100m);
            line.RuleFor(l => l.TaxRatePercent).InclusiveBetween(0m, 100m);
            line.RuleFor(l => l.DebitAccountId).GreaterThan(0).When(l => l.DebitAccountId.HasValue);
        });
    }
}

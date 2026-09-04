using AccountingERP.Application.Dtos;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public sealed class SalesInvoiceWriteRequestValidator : AbstractValidator<SalesInvoiceWriteRequest>
{
    public SalesInvoiceWriteRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);

        RuleFor(x => x.InvoiceDate)
            .NotEqual(default(DateOnly)).WithMessage("InvoiceDate is required.");

        RuleFor(x => x.DueDate)
            .GreaterThanOrEqualTo(x => x.InvoiceDate)
            .When(x => x.DueDate.HasValue)
            .WithMessage("DueDate cannot be before InvoiceDate.");

        RuleFor(x => x.Notes).MaximumLength(500);

        RuleFor(x => x.Lines)
            .NotNull()
            .Must(lines => lines is { Count: >= 1 })
            .WithMessage("An invoice needs at least one line.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Description).NotEmpty().MaximumLength(250);
            line.RuleFor(l => l.Quantity).GreaterThan(0m);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0m);
            line.RuleFor(l => l.DiscountPercent).InclusiveBetween(0m, 100m);
            line.RuleFor(l => l.TaxRatePercent).InclusiveBetween(0m, 100m);
            line.RuleFor(l => l.RevenueAccountId).GreaterThan(0).When(l => l.RevenueAccountId.HasValue);
        });
    }
}

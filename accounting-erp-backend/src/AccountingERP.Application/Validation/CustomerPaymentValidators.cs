using AccountingERP.Application.Dtos;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public sealed class CreateCustomerPaymentRequestValidator : AbstractValidator<CreateCustomerPaymentRequest>
{
    public CreateCustomerPaymentRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.PaymentDate).NotEqual(default(DateOnly)).WithMessage("PaymentDate is required.");
        RuleFor(x => x.PaymentMethodId).GreaterThan((byte)0);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.ReferenceNo).MaximumLength(50);

        RuleFor(x => x.Allocations)
            .NotNull()
            .Must(a => a is { Count: >= 1 })
            .WithMessage("At least one invoice allocation is required.");

        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.SalesInvoiceId).GreaterThan(0);
            a.RuleFor(x => x.AllocatedAmount).GreaterThan(0m);
        });

        When(x => x.Allocations is { Count: > 0 }, () =>
        {
            RuleFor(x => x.Allocations)
                .Must(a => a.Select(x => x.SalesInvoiceId).Distinct().Count() == a.Count)
                .WithMessage("The same invoice appears more than once in the allocations.");

            RuleFor(x => x)
                .Must(x => decimal.Round(x.Allocations.Sum(a => a.AllocatedAmount), 2) == decimal.Round(x.Amount, 2))
                .WithMessage("Allocations must sum exactly to the payment amount. Unallocated (on-account) receipts are not supported in this version.")
                .WithName("allocations");
        });
    }
}

using AccountingERP.Application.Dtos;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public sealed class CreateJournalEntryRequestValidator : AbstractValidator<CreateJournalEntryRequest>
{
    public CreateJournalEntryRequestValidator()
    {
        RuleFor(x => x.EntryDate)
            .NotEqual(default(DateOnly)).WithMessage("EntryDate is required.");

        RuleFor(x => x.Description).NotEmpty().MaximumLength(300);

        RuleFor(x => x.Lines)
            .NotNull()
            .Must(lines => lines is { Count: >= 2 })
            .WithMessage("A journal entry needs at least two lines.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.AccountId).GreaterThan(0);
            line.RuleFor(l => l.Debit).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l.Credit).GreaterThanOrEqualTo(0);
            line.RuleFor(l => l)
                .Must(l => (l.Debit > 0) ^ (l.Credit > 0))
                .WithMessage("Each line must be either a debit or a credit, not both and not neither.");
        });
    }
}

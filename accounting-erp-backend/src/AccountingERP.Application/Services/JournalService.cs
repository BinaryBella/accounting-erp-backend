using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Constants;
using AccountingERP.Domain.Enums;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class JournalService : IJournalService
{
    private readonly IUnitOfWork _uow;
    private readonly IJournalRepository _journal;
    private readonly INumberSequenceRepository _sequences;
    private readonly IAccountRepository _accounts;
    private readonly IValidator<CreateJournalEntryRequest> _manualValidator;

    public JournalService(
        IUnitOfWork uow,
        IJournalRepository journal,
        INumberSequenceRepository sequences,
        IAccountRepository accounts,
        IValidator<CreateJournalEntryRequest> manualValidator)
    {
        _uow = uow;
        _journal = journal;
        _sequences = sequences;
        _accounts = accounts;
        _manualValidator = manualValidator;
    }

    // ---- the single write path (PLAN §4) ------------------------------------

    public async Task<int> PostAsync(JournalDraft draft)
    {
        if (_uow.Transaction is null)
            throw new InvalidOperationException(
                "JournalService.PostAsync must run inside an active unit-of-work transaction opened by the caller.");

        AssertLineShape(draft.Lines);

        var totalDebit = RoundMoney(draft.Lines.Sum(l => l.Debit));
        var totalCredit = RoundMoney(draft.Lines.Sum(l => l.Credit));

        // Line 1 of defence: reject before anything touches the database.
        if (totalDebit != totalCredit || totalDebit <= 0)
            throw new UnbalancedJournalException(totalDebit, totalCredit);

        var entryNumber = await _sequences.NextAsync(NumberSequenceKeys.Journal);

        var journalEntryId = await _journal.InsertHeaderAsync(new JournalEntryHeaderInsert(
            EntryNumber: entryNumber,
            EntryDate: draft.EntryDate,
            Description: draft.Description,
            SourceType: (byte)draft.SourceType,
            SourceId: draft.SourceId,
            IsReversal: draft.IsReversal,
            ReversesJournalEntryId: draft.ReversesJournalEntryId,
            TotalDebit: totalDebit,
            TotalCredit: totalCredit));

        await _journal.InsertLinesAsync(journalEntryId, draft.Lines);

        // Line 2 of defence: recompute the header from the rows that actually landed;
        // CK_JournalEntry_Balanced fires here if the persisted lines do not balance.
        await _journal.ReconcileTotalsAsync(journalEntryId);

        return journalEntryId;
    }

    // ---- reads --------------------------------------------------------------

    public async Task<JournalEntryResponse> GetAsync(int id)
        => await _journal.GetByIdAsync(id) ?? throw new NotFoundException("Journal entry", id);

    public Task<JournalEntryResponse?> GetOrNullAsync(int id) => _journal.GetByIdAsync(id);

    public Task<PagedResult<JournalEntryResponse>> ListAsync(JournalEntryQuery query) => _journal.ListAsync(query);

    // ---- manual entry (owns its transaction) ------------------------------

    public async Task<JournalEntryResponse> CreateManualEntryAsync(CreateJournalEntryRequest request)
    {
        await _manualValidator.EnsureValidAsync(request);

        foreach (var accountId in request.Lines.Select(l => l.AccountId).Distinct())
        {
            var account = await _accounts.GetEntityByIdAsync(accountId);
            if (account is null)
                throw new ValidationException("lines", $"Account {accountId} does not exist.");
            if (!account.IsActive)
                throw new ValidationException("lines", $"Account {accountId} ({account.AccountCode}) is inactive and cannot be posted to.");
        }

        var draft = new JournalDraft
        {
            SourceType = JournalSourceType.Manual,
            SourceId = null,
            EntryDate = request.EntryDate,
            Description = request.Description.Trim(),
            Lines = request.Lines
                .Select(l => new JournalDraftLine(l.AccountId, RoundMoney(l.Debit), RoundMoney(l.Credit), l.Description?.Trim()))
                .ToList()
        };

        _uow.Begin();
        try
        {
            var id = await PostAsync(draft);
            _uow.Commit();
            return await _journal.GetByIdAsync(id)
                   ?? throw new InvalidOperationException("Journal entry vanished immediately after commit.");
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- helpers ----------------------------------------------------------

    private static decimal RoundMoney(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private static void AssertLineShape(IReadOnlyList<JournalDraftLine> lines)
    {
        if (lines.Count < 2)
            throw new ValidationException("lines", "A journal entry needs at least two lines.");

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (line.Debit < 0 || line.Credit < 0)
                throw new ValidationException($"lines[{i}]", "Debit and credit cannot be negative.");
            if ((line.Debit > 0) == (line.Credit > 0))
                throw new ValidationException($"lines[{i}]", "Each line must be either a debit or a credit, not both and not neither.");
        }
    }
}

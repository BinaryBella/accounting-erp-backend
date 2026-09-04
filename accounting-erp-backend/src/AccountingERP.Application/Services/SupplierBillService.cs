using System.Globalization;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Constants;
using AccountingERP.Domain.Enums;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class SupplierBillService : ISupplierBillService
{
    private static readonly byte[] AllowedDebitCategories =
        { (byte)AccountCategory.Asset, (byte)AccountCategory.Expense };

    private readonly IUnitOfWork _uow;
    private readonly ISupplierBillRepository _bills;
    private readonly ISupplierRepository _suppliers;
    private readonly IAccountRepository _accounts;
    private readonly IAccountMappingResolver _mappings;
    private readonly INumberSequenceRepository _sequences;
    private readonly IJournalService _journal;
    private readonly IAuditRepository _audit;
    private readonly IValidator<SupplierBillWriteRequest> _validator;
    private readonly IValidator<ReverseRequest> _reverseValidator;

    public SupplierBillService(
        IUnitOfWork uow,
        ISupplierBillRepository bills,
        ISupplierRepository suppliers,
        IAccountRepository accounts,
        IAccountMappingResolver mappings,
        INumberSequenceRepository sequences,
        IJournalService journal,
        IAuditRepository audit,
        IValidator<SupplierBillWriteRequest> validator,
        IValidator<ReverseRequest> reverseValidator)
    {
        _uow = uow;
        _bills = bills;
        _suppliers = suppliers;
        _accounts = accounts;
        _mappings = mappings;
        _sequences = sequences;
        _journal = journal;
        _audit = audit;
        _validator = validator;
        _reverseValidator = reverseValidator;
    }

    // ---- reads -----------------------------------------------------------

    public Task<PagedResult<SupplierBillListItem>> ListAsync(SupplierBillQuery query) => _bills.ListAsync(query);

    public async Task<SupplierBillResponse> GetAsync(int id)
        => await _bills.GetByIdAsync(id) ?? throw new NotFoundException("Supplier bill", id);

    public async Task<JournalEntryResponse> GetJournalEntryAsync(int id)
    {
        var guard = await _bills.GetGuardAsync(id) ?? throw new NotFoundException("Supplier bill", id);
        if (guard.JournalEntryId is not { } journalEntryId)
            throw new NotFoundException($"Supplier bill {id} has not been posted, so it has no journal entry.");
        return await _journal.GetAsync(journalEntryId);
    }

    // ---- create --------------------------------------------------------

    public async Task<SupplierBillResponse> CreateAsync(SupplierBillWriteRequest request)
    {
        await _validator.EnsureValidAsync(request);
        await EnsureSupplierActiveAsync(request.SupplierId);
        var lines = await BuildLinesAsync(request.Lines);

        _uow.Begin();
        try
        {
            var billNumber = await _sequences.NextAsync(NumberSequenceKeys.SupplierBill);
            var id = await _bills.InsertHeaderAsync(new SupplierBillHeaderInsert(
                billNumber,
                request.SupplierId,
                request.BillDate,
                request.DueDate,
                SubTotal: lines.Sum(l => l.LineSubTotal),
                DiscountAmount: lines.Sum(l => l.LineDiscount),
                TaxAmount: lines.Sum(l => l.LineTax),
                GrandTotal: lines.Sum(l => l.LineTotal),
                Notes: request.Notes?.Trim()));

            await _bills.InsertLinesAsync(id, lines);
            await _audit.WriteAsync("SupplierBill", id, "Created", $"{{\"billNumber\":\"{billNumber}\"}}");
            _uow.Commit();

            return await _bills.GetByIdAsync(id)
                   ?? throw new InvalidOperationException("Supplier bill vanished immediately after commit.");
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- update (draft only) ----------------------------------------------

    public async Task<SupplierBillResponse> UpdateAsync(int id, SupplierBillWriteRequest request)
    {
        await _validator.EnsureValidAsync(request);

        var guard = await _bills.GetGuardAsync(id) ?? throw new NotFoundException("Supplier bill", id);
        if (guard.Status != (byte)DocumentStatus.Draft)
            throw new ConflictException("Only a draft bill can be edited. Post-then-reverse to correct a posted bill.");

        await EnsureSupplierActiveAsync(request.SupplierId);
        var lines = await BuildLinesAsync(request.Lines);

        _uow.Begin();
        try
        {
            await _bills.UpdateHeaderAsync(new SupplierBillHeaderUpdate(
                id,
                request.SupplierId,
                request.BillDate,
                request.DueDate,
                SubTotal: lines.Sum(l => l.LineSubTotal),
                DiscountAmount: lines.Sum(l => l.LineDiscount),
                TaxAmount: lines.Sum(l => l.LineTax),
                GrandTotal: lines.Sum(l => l.LineTotal),
                Notes: request.Notes?.Trim()));

            await _bills.DeleteLinesAsync(id);
            await _bills.InsertLinesAsync(id, lines);
            await _audit.WriteAsync("SupplierBill", id, "Updated");
            _uow.Commit();

            return await _bills.GetByIdAsync(id)
                   ?? throw new InvalidOperationException("Supplier bill vanished immediately after commit.");
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- delete (draft only) --------------------------------------------

    public async Task DeleteAsync(int id)
    {
        var guard = await _bills.GetGuardAsync(id) ?? throw new NotFoundException("Supplier bill", id);
        if (guard.Status != (byte)DocumentStatus.Draft)
            throw new ConflictException("Only a draft bill can be deleted. A posted bill must be reversed.");

        await _bills.DeleteAsync(id);
    }

    // ---- post (the §4C template) --------------------------------------

    public async Task<PostSupplierBillResult> PostAsync(int id)
    {
        _uow.Begin();
        try
        {
            var view = await _bills.GetForPostAsync(id) ?? throw new NotFoundException("Supplier bill", id);

            switch ((DocumentStatus)view.Status)
            {
                case DocumentStatus.Posted:
                    throw new ConflictException($"Supplier bill {view.BillNumber} is already posted.");
                case DocumentStatus.Reversed:
                    throw new ConflictException($"Supplier bill {view.BillNumber} has been reversed.");
            }

            if (view.Lines.Count == 0)
                throw new ConflictException("Cannot post a bill that has no lines.");

            var apAccountId = await _mappings.ResolveAsync(AccountMappingKeys.AccountsPayable);
            var inputTaxAccountId = await _mappings.ResolveAsync(AccountMappingKeys.TaxReceivableInput);
            var narrative = $"Supplier Bill {view.BillNumber} - {view.SupplierName}";

            var draftLines = new List<JournalDraftLine>();

            foreach (var group in view.Lines
                         .GroupBy(l => l.DebitAccountId)
                         .Select(g => (AccountId: g.Key, Amount: g.Sum(x => x.LineSubTotal - x.LineDiscount)))
                         .Where(x => x.Amount > 0))
            {
                draftLines.Add(JournalDraftLine.ForDebit(group.AccountId, group.Amount, "Purchases / inventory"));
            }

            if (view.TaxAmount > 0)
                draftLines.Add(JournalDraftLine.ForDebit(inputTaxAccountId, view.TaxAmount, "Input tax"));

            draftLines.Add(JournalDraftLine.ForCredit(apAccountId, view.GrandTotal, narrative, supplierId: view.SupplierId));

            var journalEntryId = await _journal.PostAsync(new JournalDraft
            {
                SourceType = JournalSourceType.SupplierBill,
                SourceId = id,
                EntryDate = view.BillDate,
                Description = narrative,
                Lines = draftLines
            });

            await _bills.MarkPostedAsync(id, journalEntryId);
            await _audit.WriteAsync("SupplierBill", id, "Posted",
                $"{{\"journalEntryId\":{journalEntryId},\"grandTotal\":{view.GrandTotal.ToString(CultureInfo.InvariantCulture)}}}");

            _uow.Commit();

            var bill = await _bills.GetByIdAsync(id)
                       ?? throw new InvalidOperationException("Supplier bill vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(journalEntryId)
                               ?? throw new InvalidOperationException("Journal entry vanished after commit.");
            return new PostSupplierBillResult(bill, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- reverse (PLAN §5) -------------------------------------------------

    public async Task<ReverseSupplierBillResult> ReverseAsync(int id, ReverseRequest request)
    {
        await _reverseValidator.EnsureValidAsync(request);

        var guard = await _bills.GetGuardAsync(id) ?? throw new NotFoundException("Supplier bill", id);

        if (guard.Status != (byte)DocumentStatus.Posted)
            throw new ConflictException("Only a posted bill can be reversed.");
        if (guard.HasAllocations)
            throw new ConflictException("This bill has allocated payments. Reverse the payments first, then reverse the bill.");
        if (guard.JournalEntryId is not { } originalJournalEntryId)
            throw new ConflictException("This bill has no journal entry to reverse.");

        _uow.Begin();
        try
        {
            var reversalJournalEntryId = await _journal.ReverseAsync(originalJournalEntryId, request.ReversalDate, request.Reason);
            await _bills.MarkReversedAsync(id);
            await _audit.WriteAsync("SupplierBill", id, "Reversed",
                $"{{\"originalJournalEntryId\":{originalJournalEntryId},\"reversalJournalEntryId\":{reversalJournalEntryId}}}");

            _uow.Commit();

            var bill = await _bills.GetByIdAsync(id)
                       ?? throw new InvalidOperationException("Supplier bill vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(reversalJournalEntryId)
                               ?? throw new InvalidOperationException("Reversal journal entry vanished after commit.");
            return new ReverseSupplierBillResult(bill, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- helpers -------------------------------------------------------

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task EnsureSupplierActiveAsync(int supplierId)
    {
        var supplier = await _suppliers.GetByIdAsync(supplierId)
                       ?? throw new NotFoundException("Supplier", supplierId);
        if (!supplier.IsActive)
            throw new ValidationException("supplierId", $"Supplier {supplier.SupplierCode} is inactive.");
    }

    /// <summary>Computes every line amount server-side; a client-supplied line total is ignored (PLAN §2.7).</summary>
    private async Task<IReadOnlyList<SupplierBillLineComputed>> BuildLinesAsync(IReadOnlyList<SupplierBillLineWriteRequest> requestLines)
    {
        var defaultDebitAccountId = await _mappings.ResolveAsync(AccountMappingKeys.SupplierBillDefaultDebit);
        var computed = new List<SupplierBillLineComputed>(requestLines.Count);

        for (var i = 0; i < requestLines.Count; i++)
        {
            var line = requestLines[i];
            var debitAccountId = line.DebitAccountId ?? defaultDebitAccountId;

            if (line.DebitAccountId is { } explicitAccountId)
            {
                var account = await _accounts.GetEntityByIdAsync(explicitAccountId)
                              ?? throw new ValidationException($"lines[{i}].debitAccountId", $"Account {explicitAccountId} does not exist.");
                if (!account.IsActive)
                    throw new ValidationException($"lines[{i}].debitAccountId", $"Account {account.AccountCode} is inactive.");
                if (!AllowedDebitCategories.Contains(account.AccountTypeId))
                    throw new ValidationException($"lines[{i}].debitAccountId",
                        $"Account {account.AccountCode} must be an expense (e.g. Purchases) or asset (e.g. Inventory) account.");
            }

            var subTotal = Round2(line.Quantity * line.UnitPrice);
            var discount = Round2(subTotal * line.DiscountPercent / 100m);
            var tax = Round2((subTotal - discount) * line.TaxRatePercent / 100m);
            var total = subTotal - discount + tax;

            computed.Add(new SupplierBillLineComputed(
                LineNumber: i + 1,
                Description: line.Description.Trim(),
                Quantity: line.Quantity,
                UnitPrice: line.UnitPrice,
                DiscountPercent: line.DiscountPercent,
                TaxRatePercent: line.TaxRatePercent,
                LineSubTotal: subTotal,
                LineDiscount: discount,
                LineTax: tax,
                LineTotal: total,
                DebitAccountId: debitAccountId));
        }

        return computed;
    }
}

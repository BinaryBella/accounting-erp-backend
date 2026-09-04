using System.Globalization;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Constants;
using AccountingERP.Domain.Enums;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class SalesInvoiceService : ISalesInvoiceService
{
    private readonly IUnitOfWork _uow;
    private readonly ISalesInvoiceRepository _invoices;
    private readonly ICustomerRepository _customers;
    private readonly IAccountRepository _accounts;
    private readonly IAccountMappingResolver _mappings;
    private readonly INumberSequenceRepository _sequences;
    private readonly IJournalService _journal;
    private readonly IAuditRepository _audit;
    private readonly IValidator<SalesInvoiceWriteRequest> _validator;

    public SalesInvoiceService(
        IUnitOfWork uow,
        ISalesInvoiceRepository invoices,
        ICustomerRepository customers,
        IAccountRepository accounts,
        IAccountMappingResolver mappings,
        INumberSequenceRepository sequences,
        IJournalService journal,
        IAuditRepository audit,
        IValidator<SalesInvoiceWriteRequest> validator)
    {
        _uow = uow;
        _invoices = invoices;
        _customers = customers;
        _accounts = accounts;
        _mappings = mappings;
        _sequences = sequences;
        _journal = journal;
        _audit = audit;
        _validator = validator;
    }

    // ---- reads -----------------------------------------------------------

    public Task<PagedResult<SalesInvoiceListItem>> ListAsync(SalesInvoiceQuery query) => _invoices.ListAsync(query);

    public async Task<SalesInvoiceResponse> GetAsync(int id)
        => await _invoices.GetByIdAsync(id) ?? throw new NotFoundException("Sales invoice", id);

    public async Task<JournalEntryResponse> GetJournalEntryAsync(int id)
    {
        var guard = await _invoices.GetGuardAsync(id) ?? throw new NotFoundException("Sales invoice", id);
        if (guard.JournalEntryId is not { } journalEntryId)
            throw new NotFoundException($"Sales invoice {id} has not been posted, so it has no journal entry.");
        return await _journal.GetAsync(journalEntryId);
    }

    // ---- create --------------------------------------------------------

    public async Task<SalesInvoiceResponse> CreateAsync(SalesInvoiceWriteRequest request)
    {
        await _validator.EnsureValidAsync(request);
        await EnsureCustomerActiveAsync(request.CustomerId);
        var lines = await BuildLinesAsync(request.Lines);

        _uow.Begin();
        try
        {
            var invoiceNumber = await _sequences.NextAsync(NumberSequenceKeys.SalesInvoice);
            var id = await _invoices.InsertHeaderAsync(new SalesInvoiceHeaderInsert(
                invoiceNumber,
                request.CustomerId,
                request.InvoiceDate,
                request.DueDate,
                SubTotal: lines.Sum(l => l.LineSubTotal),
                DiscountAmount: lines.Sum(l => l.LineDiscount),
                TaxAmount: lines.Sum(l => l.LineTax),
                GrandTotal: lines.Sum(l => l.LineTotal),
                Notes: request.Notes?.Trim()));

            await _invoices.InsertLinesAsync(id, lines);
            await _audit.WriteAsync("SalesInvoice", id, "Created", $"{{\"invoiceNumber\":\"{invoiceNumber}\"}}");
            _uow.Commit();

            return await _invoices.GetByIdAsync(id)
                   ?? throw new InvalidOperationException("Sales invoice vanished immediately after commit.");
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- update (draft only) ----------------------------------------------

    public async Task<SalesInvoiceResponse> UpdateAsync(int id, SalesInvoiceWriteRequest request)
    {
        await _validator.EnsureValidAsync(request);

        var guard = await _invoices.GetGuardAsync(id) ?? throw new NotFoundException("Sales invoice", id);
        if (guard.Status != (byte)DocumentStatus.Draft)
            throw new ConflictException("Only a draft invoice can be edited. Post-then-reverse to correct a posted invoice.");

        await EnsureCustomerActiveAsync(request.CustomerId);
        var lines = await BuildLinesAsync(request.Lines);

        _uow.Begin();
        try
        {
            await _invoices.UpdateHeaderAsync(new SalesInvoiceHeaderUpdate(
                id,
                request.CustomerId,
                request.InvoiceDate,
                request.DueDate,
                SubTotal: lines.Sum(l => l.LineSubTotal),
                DiscountAmount: lines.Sum(l => l.LineDiscount),
                TaxAmount: lines.Sum(l => l.LineTax),
                GrandTotal: lines.Sum(l => l.LineTotal),
                Notes: request.Notes?.Trim()));

            await _invoices.DeleteLinesAsync(id);
            await _invoices.InsertLinesAsync(id, lines);
            await _audit.WriteAsync("SalesInvoice", id, "Updated");
            _uow.Commit();

            return await _invoices.GetByIdAsync(id)
                   ?? throw new InvalidOperationException("Sales invoice vanished immediately after commit.");
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
        var guard = await _invoices.GetGuardAsync(id) ?? throw new NotFoundException("Sales invoice", id);
        if (guard.Status != (byte)DocumentStatus.Draft)
            throw new ConflictException("Only a draft invoice can be deleted. A posted invoice must be reversed.");

        await _invoices.DeleteAsync(id);
    }

    // ---- post (the §4A template) --------------------------------------

    public async Task<PostSalesInvoiceResult> PostAsync(int id)
    {
        _uow.Begin();
        try
        {
            var view = await _invoices.GetForPostAsync(id) ?? throw new NotFoundException("Sales invoice", id);

            switch ((DocumentStatus)view.Status)
            {
                case DocumentStatus.Posted:
                    throw new ConflictException($"Sales invoice {view.InvoiceNumber} is already posted.");
                case DocumentStatus.Reversed:
                    throw new ConflictException($"Sales invoice {view.InvoiceNumber} has been reversed.");
            }

            if (view.Lines.Count == 0)
                throw new ConflictException("Cannot post an invoice that has no lines.");

            var arAccountId = await _mappings.ResolveAsync(AccountMappingKeys.AccountsReceivable);
            var taxAccountId = await _mappings.ResolveAsync(AccountMappingKeys.TaxPayableOutput);
            var narrative = $"Sales Invoice {view.InvoiceNumber} - {view.CustomerName}";

            var draftLines = new List<JournalDraftLine>
            {
                JournalDraftLine.ForDebit(arAccountId, view.GrandTotal, narrative, customerId: view.CustomerId)
            };

            foreach (var group in view.Lines
                         .GroupBy(l => l.RevenueAccountId)
                         .Select(g => (AccountId: g.Key, Amount: g.Sum(x => x.LineSubTotal - x.LineDiscount)))
                         .Where(x => x.Amount > 0))
            {
                draftLines.Add(JournalDraftLine.ForCredit(group.AccountId, group.Amount, "Sales revenue"));
            }

            if (view.TaxAmount > 0)
                draftLines.Add(JournalDraftLine.ForCredit(taxAccountId, view.TaxAmount, "Output tax"));

            var journalEntryId = await _journal.PostAsync(new JournalDraft
            {
                SourceType = JournalSourceType.SalesInvoice,
                SourceId = id,
                EntryDate = view.InvoiceDate,
                Description = narrative,
                Lines = draftLines
            });

            await _invoices.MarkPostedAsync(id, journalEntryId);
            await _audit.WriteAsync("SalesInvoice", id, "Posted",
                $"{{\"journalEntryId\":{journalEntryId},\"grandTotal\":{view.GrandTotal.ToString(CultureInfo.InvariantCulture)}}}");

            _uow.Commit();

            var invoice = await _invoices.GetByIdAsync(id)
                          ?? throw new InvalidOperationException("Sales invoice vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(journalEntryId)
                               ?? throw new InvalidOperationException("Journal entry vanished after commit.");
            return new PostSalesInvoiceResult(invoice, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- helpers -------------------------------------------------------

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private async Task EnsureCustomerActiveAsync(int customerId)
    {
        var customer = await _customers.GetByIdAsync(customerId)
                       ?? throw new NotFoundException("Customer", customerId);
        if (!customer.IsActive)
            throw new ValidationException("customerId", $"Customer {customer.CustomerCode} is inactive.");
    }

    /// <summary>Computes every line amount server-side; a client-supplied line total is ignored (PLAN §2.6).</summary>
    private async Task<IReadOnlyList<SalesInvoiceLineComputed>> BuildLinesAsync(IReadOnlyList<SalesInvoiceLineWriteRequest> requestLines)
    {
        var defaultRevenueAccountId = await _mappings.ResolveAsync(AccountMappingKeys.SalesRevenue);
        var computed = new List<SalesInvoiceLineComputed>(requestLines.Count);

        for (var i = 0; i < requestLines.Count; i++)
        {
            var line = requestLines[i];
            var revenueAccountId = line.RevenueAccountId ?? defaultRevenueAccountId;

            if (line.RevenueAccountId is { } explicitAccountId)
            {
                var account = await _accounts.GetEntityByIdAsync(explicitAccountId)
                              ?? throw new ValidationException($"lines[{i}].revenueAccountId", $"Account {explicitAccountId} does not exist.");
                if (!account.IsActive)
                    throw new ValidationException($"lines[{i}].revenueAccountId", $"Account {account.AccountCode} is inactive.");
                if (account.AccountTypeId != (byte)AccountCategory.Revenue)
                    throw new ValidationException($"lines[{i}].revenueAccountId", $"Account {account.AccountCode} is not a revenue account.");
            }

            var subTotal = Round2(line.Quantity * line.UnitPrice);
            var discount = Round2(subTotal * line.DiscountPercent / 100m);
            var tax = Round2((subTotal - discount) * line.TaxRatePercent / 100m);
            var total = subTotal - discount + tax;

            computed.Add(new SalesInvoiceLineComputed(
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
                RevenueAccountId: revenueAccountId));
        }

        return computed;
    }
}

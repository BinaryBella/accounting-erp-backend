using System.Globalization;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Constants;
using AccountingERP.Domain.Enums;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class CustomerPaymentService : ICustomerPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly ICustomerPaymentRepository _payments;
    private readonly ICustomerRepository _customers;
    private readonly IPaymentMethodRepository _paymentMethods;
    private readonly IAccountMappingResolver _mappings;
    private readonly INumberSequenceRepository _sequences;
    private readonly IJournalService _journal;
    private readonly IAuditRepository _audit;
    private readonly IValidator<CreateCustomerPaymentRequest> _validator;
    private readonly IValidator<ReverseRequest> _reverseValidator;

    public CustomerPaymentService(
        IUnitOfWork uow,
        ICustomerPaymentRepository payments,
        ICustomerRepository customers,
        IPaymentMethodRepository paymentMethods,
        IAccountMappingResolver mappings,
        INumberSequenceRepository sequences,
        IJournalService journal,
        IAuditRepository audit,
        IValidator<CreateCustomerPaymentRequest> validator,
        IValidator<ReverseRequest> reverseValidator)
    {
        _uow = uow;
        _payments = payments;
        _customers = customers;
        _paymentMethods = paymentMethods;
        _mappings = mappings;
        _sequences = sequences;
        _journal = journal;
        _audit = audit;
        _validator = validator;
        _reverseValidator = reverseValidator;
    }

    public Task<PagedResult<CustomerPaymentListItem>> ListAsync(CustomerPaymentQuery query) => _payments.ListAsync(query);

    public async Task<CustomerPaymentResponse> GetAsync(int id)
        => await _payments.GetByIdAsync(id) ?? throw new NotFoundException("Customer payment", id);

    public async Task<PostCustomerPaymentResult> CreateAsync(CreateCustomerPaymentRequest request)
    {
        await _validator.EnsureValidAsync(request);

        var amount = Round2(request.Amount);

        var customer = await _customers.GetByIdAsync(request.CustomerId)
                       ?? throw new NotFoundException("Customer", request.CustomerId);
        if (!customer.IsActive)
            throw new ValidationException("customerId", $"Customer {customer.CustomerCode} is inactive.");

        var method = await _paymentMethods.GetByIdAsync(request.PaymentMethodId)
                     ?? throw new ValidationException("paymentMethodId", $"Payment method {request.PaymentMethodId} does not exist.");
        if (!method.IsActive)
            throw new ValidationException("paymentMethodId", $"Payment method '{method.Name}' is inactive.");

        var arAccountId = await _mappings.ResolveAsync(AccountMappingKeys.AccountsReceivable);

        _uow.Begin();
        try
        {
            // Re-check every target invoice under UPDLOCK, inside the transaction.
            foreach (var allocation in request.Allocations)
            {
                var invoice = await _payments.LockInvoiceAsync(allocation.SalesInvoiceId)
                              ?? throw new NotFoundException("Sales invoice", allocation.SalesInvoiceId);

                if (invoice.Status != (byte)DocumentStatus.Posted)
                    throw new ConflictException($"Invoice {invoice.InvoiceNumber} is not posted; a receipt can only be applied to a posted invoice.");
                if (invoice.CustomerId != request.CustomerId)
                    throw new ValidationException("allocations", $"Invoice {invoice.InvoiceNumber} does not belong to customer {customer.CustomerCode}.");
                if (request.PaymentDate < invoice.InvoiceDate)
                    throw new ValidationException("paymentDate", $"Payment date {request.PaymentDate:yyyy-MM-dd} is before invoice {invoice.InvoiceNumber} date {invoice.InvoiceDate:yyyy-MM-dd}.");

                if (Round2(allocation.AllocatedAmount) > invoice.Outstanding)
                    throw new ConflictException(
                        $"Allocation {allocation.AllocatedAmount:0.00} exceeds invoice {invoice.InvoiceNumber} outstanding balance {invoice.Outstanding:0.00}.");
            }

            var paymentNumber = await _sequences.NextAsync(NumberSequenceKeys.Receipt);
            var paymentId = await _payments.InsertAsync(new CustomerPaymentInsert(
                paymentNumber, request.CustomerId, request.PaymentMethodId, request.PaymentDate,
                request.ReferenceNo?.Trim(), amount));

            foreach (var allocation in request.Allocations)
                await _payments.InsertAllocationAsync(paymentId, allocation.SalesInvoiceId, Round2(allocation.AllocatedAmount));

            var journalEntryId = await _journal.PostAsync(new JournalDraft
            {
                SourceType = JournalSourceType.CustomerReceipt,
                SourceId = paymentId,
                EntryDate = request.PaymentDate,
                Description = $"Customer Receipt {paymentNumber} - {customer.Name}",
                Lines = new[]
                {
                    JournalDraftLine.ForDebit(method.LedgerAccountId, amount, $"{method.Name} receipt {paymentNumber}"),
                    JournalDraftLine.ForCredit(arAccountId, amount, $"Receipt {paymentNumber}", customerId: request.CustomerId)
                }
            });

            await _payments.MarkPostedAsync(paymentId, journalEntryId);

            foreach (var allocation in request.Allocations)
                await _payments.AddInvoicePaidAmountAsync(allocation.SalesInvoiceId, Round2(allocation.AllocatedAmount));

            await _audit.WriteAsync("Payment", paymentId, "Posted",
                $"{{\"journalEntryId\":{journalEntryId},\"type\":\"CustomerReceipt\",\"amount\":{amount.ToString(CultureInfo.InvariantCulture)}}}");

            _uow.Commit();

            var payment = await _payments.GetByIdAsync(paymentId)
                          ?? throw new InvalidOperationException("Payment vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(journalEntryId)
                               ?? throw new InvalidOperationException("Journal entry vanished after commit.");
            return new PostCustomerPaymentResult(payment, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    // ---- reverse (PLAN §5) -------------------------------------------------

    public async Task<ReverseCustomerPaymentResult> ReverseAsync(int id, ReverseRequest request)
    {
        await _reverseValidator.EnsureValidAsync(request);

        var info = await _payments.GetForReverseAsync(id)
                   ?? throw new NotFoundException("Customer payment", id);

        if (info.Status != (byte)DocumentStatus.Posted)
            throw new ConflictException($"Receipt {info.PaymentNumber} is not posted, so it cannot be reversed.");
        if (info.JournalEntryId is not { } originalJournalEntryId)
            throw new ConflictException($"Receipt {info.PaymentNumber} has no journal entry to reverse.");

        _uow.Begin();
        try
        {
            var reversalJournalEntryId = await _journal.ReverseAsync(originalJournalEntryId, request.ReversalDate, request.Reason);

            // Release each allocation: restore the invoice's outstanding balance.
            // The PaymentAllocation rows are kept as history; the receipt's Reversed
            // status already removes it from the Outstanding report.
            foreach (var allocation in info.Allocations)
            {
                _ = await _payments.LockInvoiceAsync(allocation.SalesInvoiceId);
                await _payments.AddInvoicePaidAmountAsync(allocation.SalesInvoiceId, -Round2(allocation.AllocatedAmount));
            }

            await _payments.MarkReversedAsync(id);
            await _audit.WriteAsync("Payment", id, "Reversed",
                $"{{\"originalJournalEntryId\":{originalJournalEntryId},\"reversalJournalEntryId\":{reversalJournalEntryId}}}");

            _uow.Commit();

            var payment = await _payments.GetByIdAsync(id)
                          ?? throw new InvalidOperationException("Payment vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(reversalJournalEntryId)
                               ?? throw new InvalidOperationException("Reversal journal entry vanished after commit.");
            return new ReverseCustomerPaymentResult(payment, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

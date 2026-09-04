using System.Globalization;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Application.Exceptions;
using AccountingERP.Application.Validation;
using AccountingERP.Domain.Constants;
using AccountingERP.Domain.Enums;
using FluentValidation;

namespace AccountingERP.Application.Services;

public sealed class SupplierPaymentService : ISupplierPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly ISupplierPaymentRepository _payments;
    private readonly ISupplierRepository _suppliers;
    private readonly IPaymentMethodRepository _paymentMethods;
    private readonly IAccountMappingResolver _mappings;
    private readonly INumberSequenceRepository _sequences;
    private readonly IJournalService _journal;
    private readonly IAuditRepository _audit;
    private readonly IValidator<CreateSupplierPaymentRequest> _validator;
    private readonly IValidator<ReverseRequest> _reverseValidator;

    public SupplierPaymentService(
        IUnitOfWork uow,
        ISupplierPaymentRepository payments,
        ISupplierRepository suppliers,
        IPaymentMethodRepository paymentMethods,
        IAccountMappingResolver mappings,
        INumberSequenceRepository sequences,
        IJournalService journal,
        IAuditRepository audit,
        IValidator<CreateSupplierPaymentRequest> validator,
        IValidator<ReverseRequest> reverseValidator)
    {
        _uow = uow;
        _payments = payments;
        _suppliers = suppliers;
        _paymentMethods = paymentMethods;
        _mappings = mappings;
        _sequences = sequences;
        _journal = journal;
        _audit = audit;
        _validator = validator;
        _reverseValidator = reverseValidator;
    }

    public Task<PagedResult<SupplierPaymentListItem>> ListAsync(SupplierPaymentQuery query) => _payments.ListAsync(query);

    public async Task<SupplierPaymentResponse> GetAsync(int id)
        => await _payments.GetByIdAsync(id) ?? throw new NotFoundException("Supplier payment", id);

    public async Task<PostSupplierPaymentResult> CreateAsync(CreateSupplierPaymentRequest request)
    {
        await _validator.EnsureValidAsync(request);

        var amount = Round2(request.Amount);

        var supplier = await _suppliers.GetByIdAsync(request.SupplierId)
                       ?? throw new NotFoundException("Supplier", request.SupplierId);
        if (!supplier.IsActive)
            throw new ValidationException("supplierId", $"Supplier {supplier.SupplierCode} is inactive.");

        var method = await _paymentMethods.GetByIdAsync(request.PaymentMethodId)
                     ?? throw new ValidationException("paymentMethodId", $"Payment method {request.PaymentMethodId} does not exist.");
        if (!method.IsActive)
            throw new ValidationException("paymentMethodId", $"Payment method '{method.Name}' is inactive.");

        var apAccountId = await _mappings.ResolveAsync(AccountMappingKeys.AccountsPayable);

        _uow.Begin();
        try
        {
            foreach (var allocation in request.Allocations)
            {
                var bill = await _payments.LockBillAsync(allocation.SupplierBillId)
                           ?? throw new NotFoundException("Supplier bill", allocation.SupplierBillId);

                if (bill.Status != (byte)DocumentStatus.Posted)
                    throw new ConflictException($"Bill {bill.BillNumber} is not posted; a payment can only be applied to a posted bill.");
                if (bill.SupplierId != request.SupplierId)
                    throw new ValidationException("allocations", $"Bill {bill.BillNumber} does not belong to supplier {supplier.SupplierCode}.");
                if (request.PaymentDate < bill.BillDate)
                    throw new ValidationException("paymentDate", $"Payment date {request.PaymentDate:yyyy-MM-dd} is before bill {bill.BillNumber} date {bill.BillDate:yyyy-MM-dd}.");
                if (Round2(allocation.AllocatedAmount) > bill.Outstanding)
                    throw new ConflictException(
                        $"Allocation {allocation.AllocatedAmount:0.00} exceeds bill {bill.BillNumber} outstanding balance {bill.Outstanding:0.00}.");
            }

            var paymentNumber = await _sequences.NextAsync(NumberSequenceKeys.Payment);
            var paymentId = await _payments.InsertAsync(new SupplierPaymentInsert(
                paymentNumber, request.SupplierId, request.PaymentMethodId, request.PaymentDate,
                request.ReferenceNo?.Trim(), amount));

            foreach (var allocation in request.Allocations)
                await _payments.InsertAllocationAsync(paymentId, allocation.SupplierBillId, Round2(allocation.AllocatedAmount));

            var journalEntryId = await _journal.PostAsync(new JournalDraft
            {
                SourceType = JournalSourceType.SupplierPayment,
                SourceId = paymentId,
                EntryDate = request.PaymentDate,
                Description = $"Supplier Payment {paymentNumber} - {supplier.Name}",
                Lines = new[]
                {
                    JournalDraftLine.ForDebit(apAccountId, amount, $"Payment {paymentNumber}", supplierId: request.SupplierId),
                    JournalDraftLine.ForCredit(method.LedgerAccountId, amount, $"{method.Name} payment {paymentNumber}")
                }
            });

            await _payments.MarkPostedAsync(paymentId, journalEntryId);

            foreach (var allocation in request.Allocations)
                await _payments.AddBillPaidAmountAsync(allocation.SupplierBillId, Round2(allocation.AllocatedAmount));

            await _audit.WriteAsync("Payment", paymentId, "Posted",
                $"{{\"journalEntryId\":{journalEntryId},\"type\":\"SupplierPayment\",\"amount\":{amount.ToString(CultureInfo.InvariantCulture)}}}");

            _uow.Commit();

            var payment = await _payments.GetByIdAsync(paymentId)
                          ?? throw new InvalidOperationException("Payment vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(journalEntryId)
                               ?? throw new InvalidOperationException("Journal entry vanished after commit.");
            return new PostSupplierPaymentResult(payment, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    public async Task<ReverseSupplierPaymentResult> ReverseAsync(int id, ReverseRequest request)
    {
        await _reverseValidator.EnsureValidAsync(request);

        var info = await _payments.GetForReverseAsync(id)
                   ?? throw new NotFoundException("Supplier payment", id);

        if (info.Status != (byte)DocumentStatus.Posted)
            throw new ConflictException($"Payment {info.PaymentNumber} is not posted, so it cannot be reversed.");
        if (info.JournalEntryId is not { } originalJournalEntryId)
            throw new ConflictException($"Payment {info.PaymentNumber} has no journal entry to reverse.");

        _uow.Begin();
        try
        {
            var reversalJournalEntryId = await _journal.ReverseAsync(originalJournalEntryId, request.ReversalDate, request.Reason);

            foreach (var allocation in info.Allocations)
            {
                _ = await _payments.LockBillAsync(allocation.SupplierBillId);
                await _payments.AddBillPaidAmountAsync(allocation.SupplierBillId, -Round2(allocation.AllocatedAmount));
            }

            await _payments.MarkReversedAsync(id);
            await _audit.WriteAsync("Payment", id, "Reversed",
                $"{{\"originalJournalEntryId\":{originalJournalEntryId},\"reversalJournalEntryId\":{reversalJournalEntryId}}}");

            _uow.Commit();

            var payment = await _payments.GetByIdAsync(id)
                          ?? throw new InvalidOperationException("Payment vanished after commit.");
            var journalEntry = await _journal.GetOrNullAsync(reversalJournalEntryId)
                               ?? throw new InvalidOperationException("Reversal journal entry vanished after commit.");
            return new ReverseSupplierPaymentResult(payment, journalEntry);
        }
        catch
        {
            _uow.Rollback();
            throw;
        }
    }

    private static decimal Round2(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

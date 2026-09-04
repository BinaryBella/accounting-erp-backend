namespace AccountingERP.Application.Dtos;

// --- write contract -------------------------------------------------------

public sealed record CreateCustomerPaymentRequest(
    int CustomerId,
    DateOnly PaymentDate,
    byte PaymentMethodId,
    string? ReferenceNo,
    decimal Amount,
    IReadOnlyList<PaymentAllocationRequest> Allocations);

public sealed record PaymentAllocationRequest(
    int SalesInvoiceId,
    decimal AllocatedAmount);

// --- read contract -------------------------------------------------------

public sealed record CustomerPaymentResponse(
    int PaymentId,
    string PaymentNumber,
    int CustomerId,
    string CustomerCode,
    string CustomerName,
    DateOnly PaymentDate,
    byte PaymentMethodId,
    string PaymentMethod,
    string? ReferenceNo,
    decimal Amount,
    string Status,
    int? JournalEntryId,
    DateTime? PostedAtUtc,
    DateTime CreatedAtUtc,
    IReadOnlyList<CustomerPaymentAllocationResponse> Allocations);

public sealed record CustomerPaymentAllocationResponse(
    int PaymentAllocationId,
    int SalesInvoiceId,
    string InvoiceNumber,
    decimal AllocatedAmount,
    decimal InvoiceGrandTotal,
    decimal InvoiceAmountPaid)
{
    public decimal InvoiceOutstanding => InvoiceGrandTotal - InvoiceAmountPaid;
}

public sealed record CustomerPaymentListItem(
    int PaymentId,
    string PaymentNumber,
    int CustomerId,
    string CustomerName,
    DateOnly PaymentDate,
    string PaymentMethod,
    decimal Amount,
    string Status);

public sealed record CustomerPaymentQuery(
    int? CustomerId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    int Page = 1,
    int PageSize = 50);

public sealed record PostCustomerPaymentResult(
    CustomerPaymentResponse Payment,
    JournalEntryResponse JournalEntry);

// --- internal projections ------------------------------------------------

public sealed record CustomerPaymentInsert(
    string PaymentNumber,
    int CustomerId,
    byte PaymentMethodId,
    DateOnly PaymentDate,
    string? ReferenceNo,
    decimal Amount);

/// <summary>An invoice row read <c>WITH (UPDLOCK, ROWLOCK)</c> while allocating a receipt to it.</summary>
public sealed record InvoiceAllocationTarget(
    int SalesInvoiceId,
    string InvoiceNumber,
    byte Status,
    int CustomerId,
    DateOnly InvoiceDate,
    decimal GrandTotal,
    decimal AmountPaid)
{
    public decimal Outstanding => GrandTotal - AmountPaid;
}

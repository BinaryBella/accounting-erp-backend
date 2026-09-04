using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class CustomerPaymentRepository : ICustomerPaymentRepository
{
    private readonly IUnitOfWork _uow;

    public CustomerPaymentRepository(IUnitOfWork uow) => _uow = uow;

    public Task<int> InsertAsync(CustomerPaymentInsert p) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(PaymentSql.InsertCustomerReceipt, new
        {
            p.PaymentNumber,
            p.PaymentDate,
            p.CustomerId,
            p.PaymentMethodId,
            p.ReferenceNo,
            p.Amount
        }, _uow.Transaction));

    public Task InsertAllocationAsync(int paymentId, int salesInvoiceId, decimal allocatedAmount) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(PaymentSql.InsertInvoiceAllocation, new
        {
            PaymentId = paymentId,
            SalesInvoiceId = salesInvoiceId,
            AllocatedAmount = allocatedAmount
        }, _uow.Transaction));

    public Task<InvoiceAllocationTarget?> LockInvoiceAsync(int salesInvoiceId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<InvoiceAllocationTarget>(new CommandDefinition(
            PaymentSql.LockInvoice, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

    public Task AddInvoicePaidAmountAsync(int salesInvoiceId, decimal delta) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(PaymentSql.AddInvoicePaidAmount, new
        {
            SalesInvoiceId = salesInvoiceId,
            Delta = delta
        }, _uow.Transaction));

    public Task MarkPostedAsync(int paymentId, int journalEntryId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            PaymentSql.MarkPosted, new { PaymentId = paymentId, JournalEntryId = journalEntryId }, _uow.Transaction));

    public async Task<CustomerPaymentResponse?> GetByIdAsync(int paymentId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            PaymentSql.GetCustomerReceiptById, new { PaymentId = paymentId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<CustomerPaymentHeaderRow>();
        if (header is null)
            return null;

        var allocations = (await grid.ReadAsync<CustomerPaymentAllocationResponse>()).ToList();

        return new CustomerPaymentResponse(
            header.PaymentId, header.PaymentNumber, header.CustomerId, header.CustomerCode, header.CustomerName,
            header.PaymentDate, header.PaymentMethodId, header.PaymentMethod, header.ReferenceNo,
            header.Amount, ((DocumentStatus)header.Status).ToString(),
            header.JournalEntryId, header.PostedAtUtc, header.CreatedAtUtc, allocations);
    }

    public async Task<PagedResult<CustomerPaymentListItem>> ListAsync(CustomerPaymentQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new { query.CustomerId, query.FromDate, query.ToDate, page.Skip, page.Take };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(PaymentSql.ListCustomerReceipts, parameters, _uow.Transaction));

        var rows = (await grid.ReadAsync<CustomerPaymentListRow>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        var items = rows
            .Select(r => new CustomerPaymentListItem(
                r.PaymentId, r.PaymentNumber, r.CustomerId, r.CustomerName,
                r.PaymentDate, r.PaymentMethod, r.Amount, ((DocumentStatus)r.Status).ToString()))
            .ToList();

        return new PagedResult<CustomerPaymentListItem>(items, page.Page, page.PageSize, total);
    }

    private sealed class CustomerPaymentHeaderRow
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = default!;
        public string CustomerName { get; set; } = default!;
        public DateOnly PaymentDate { get; set; }
        public byte PaymentMethodId { get; set; }
        public string PaymentMethod { get; set; } = default!;
        public string? ReferenceNo { get; set; }
        public decimal Amount { get; set; }
        public byte Status { get; set; }
        public int? JournalEntryId { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    private sealed class CustomerPaymentListRow
    {
        public int PaymentId { get; set; }
        public string PaymentNumber { get; set; } = default!;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = default!;
        public DateOnly PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = default!;
        public decimal Amount { get; set; }
        public byte Status { get; set; }
    }
}

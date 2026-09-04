using System.Data;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly IUnitOfWork _uow;

    public SalesInvoiceRepository(IUnitOfWork uow) => _uow = uow;

    public Task<int> InsertHeaderAsync(SalesInvoiceHeaderInsert h) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(SalesInvoiceSql.InsertHeader, new
        {
            h.InvoiceNumber,
            h.CustomerId,
            h.InvoiceDate,
            h.DueDate,
            h.SubTotal,
            h.DiscountAmount,
            h.TaxAmount,
            h.GrandTotal,
            h.Notes
        }, _uow.Transaction));

    public Task InsertLinesAsync(int salesInvoiceId, IReadOnlyList<SalesInvoiceLineComputed> lines)
    {
        var table = new DataTable();
        table.Columns.Add("LineNumber", typeof(int));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("Quantity", typeof(decimal));
        table.Columns.Add("UnitPrice", typeof(decimal));
        table.Columns.Add("DiscountPercent", typeof(decimal));
        table.Columns.Add("TaxRatePercent", typeof(decimal));
        table.Columns.Add("LineSubTotal", typeof(decimal));
        table.Columns.Add("LineDiscount", typeof(decimal));
        table.Columns.Add("LineTax", typeof(decimal));
        table.Columns.Add("LineTotal", typeof(decimal));
        table.Columns.Add("RevenueAccountId", typeof(int));

        foreach (var l in lines)
        {
            table.Rows.Add(
                l.LineNumber, l.Description, l.Quantity, l.UnitPrice, l.DiscountPercent, l.TaxRatePercent,
                l.LineSubTotal, l.LineDiscount, l.LineTax, l.LineTotal, l.RevenueAccountId);
        }

        var tvp = table.AsTableValuedParameter("dbo.SalesInvoiceLineTvp");
        return _uow.Connection.ExecuteAsync(new CommandDefinition(
            SalesInvoiceSql.InsertLines, new { SalesInvoiceId = salesInvoiceId, Lines = tvp }, _uow.Transaction));
    }

    public Task DeleteLinesAsync(int salesInvoiceId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SalesInvoiceSql.DeleteLines, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

    public Task UpdateHeaderAsync(SalesInvoiceHeaderUpdate h) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(SalesInvoiceSql.UpdateHeader, new
        {
            h.SalesInvoiceId,
            h.CustomerId,
            h.InvoiceDate,
            h.DueDate,
            h.SubTotal,
            h.DiscountAmount,
            h.TaxAmount,
            h.GrandTotal,
            h.Notes
        }, _uow.Transaction));

    public async Task<SalesInvoiceResponse?> GetByIdAsync(int salesInvoiceId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            SalesInvoiceSql.GetById, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<SalesInvoiceHeaderRow>();
        if (header is null)
            return null;

        var lines = (await grid.ReadAsync<SalesInvoiceLineResponse>()).ToList();
        var allocations = (await grid.ReadAsync<SalesInvoiceAllocationResponse>()).ToList();

        return new SalesInvoiceResponse(
            header.SalesInvoiceId, header.InvoiceNumber, header.CustomerId, header.CustomerCode, header.CustomerName,
            header.InvoiceDate, header.DueDate, ((DocumentStatus)header.Status).ToString(),
            header.SubTotal, header.DiscountAmount, header.TaxAmount, header.GrandTotal, header.AmountPaid, header.Notes,
            header.JournalEntryId, header.PostedAtUtc, header.CreatedAtUtc, header.UpdatedAtUtc,
            lines, allocations);
    }

    public async Task<PagedResult<SalesInvoiceListItem>> ListAsync(SalesInvoiceQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new
        {
            query.CustomerId,
            query.Status,
            query.FromDate,
            query.ToDate,
            page.Skip,
            page.Take
        };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(SalesInvoiceSql.List, parameters, _uow.Transaction));

        var rows = (await grid.ReadAsync<SalesInvoiceListRow>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        var items = rows
            .Select(r => new SalesInvoiceListItem(
                r.SalesInvoiceId, r.InvoiceNumber, r.CustomerId, r.CustomerName,
                r.InvoiceDate, r.DueDate, ((DocumentStatus)r.Status).ToString(), r.GrandTotal, r.AmountPaid))
            .ToList();

        return new PagedResult<SalesInvoiceListItem>(items, page.Page, page.PageSize, total);
    }

    public async Task<SalesInvoicePostView?> GetForPostAsync(int salesInvoiceId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            SalesInvoiceSql.GetForPost, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<SalesInvoicePostHeader>();
        if (header is null)
            return null;

        var lines = (await grid.ReadAsync<SalesInvoicePostLine>()).ToList();
        return new SalesInvoicePostView(
            header.SalesInvoiceId, header.InvoiceNumber, header.Status, header.CustomerId, header.CustomerName,
            header.InvoiceDate, header.TaxAmount, header.GrandTotal, lines);
    }

    public Task<SalesInvoiceGuard?> GetGuardAsync(int salesInvoiceId) =>
        _uow.Connection.QuerySingleOrDefaultAsync<SalesInvoiceGuard>(new CommandDefinition(
            SalesInvoiceSql.GetGuard, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

    public Task MarkPostedAsync(int salesInvoiceId, int journalEntryId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SalesInvoiceSql.MarkPosted, new { SalesInvoiceId = salesInvoiceId, JournalEntryId = journalEntryId }, _uow.Transaction));

    public Task MarkReversedAsync(int salesInvoiceId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SalesInvoiceSql.MarkReversed, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

    public Task DeleteAsync(int salesInvoiceId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            SalesInvoiceSql.Delete, new { SalesInvoiceId = salesInvoiceId }, _uow.Transaction));

    // --- Dapper row shapes -------------------------------------------------

    private sealed class SalesInvoiceHeaderRow
    {
        public int SalesInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = default!;
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = default!;
        public string CustomerName { get; set; } = default!;
        public DateOnly InvoiceDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public byte Status { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public string? Notes { get; set; }
        public int? JournalEntryId { get; set; }
        public DateTime? PostedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    private sealed class SalesInvoiceListRow
    {
        public int SalesInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = default!;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = default!;
        public DateOnly InvoiceDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public byte Status { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
    }

    private sealed class SalesInvoicePostHeader
    {
        public int SalesInvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = default!;
        public byte Status { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = default!;
        public DateOnly InvoiceDate { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal GrandTotal { get; set; }
    }
}

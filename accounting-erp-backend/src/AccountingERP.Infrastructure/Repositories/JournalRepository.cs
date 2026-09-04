using System.Data;
using AccountingERP.Application.Abstractions;
using AccountingERP.Application.Dtos;
using AccountingERP.Domain.Enums;
using AccountingERP.Infrastructure.Sql;
using Dapper;

namespace AccountingERP.Infrastructure.Repositories;

public sealed class JournalRepository : IJournalRepository
{
    private readonly IUnitOfWork _uow;

    public JournalRepository(IUnitOfWork uow) => _uow = uow;

    public Task<int> InsertHeaderAsync(JournalEntryHeaderInsert h) =>
        _uow.Connection.QuerySingleAsync<int>(new CommandDefinition(JournalSql.InsertHeader, new
        {
            h.EntryNumber,
            h.EntryDate,
            h.Description,
            h.SourceType,
            h.SourceId,
            h.IsReversal,
            h.ReversesJournalEntryId,
            h.TotalDebit,
            h.TotalCredit
        }, _uow.Transaction));

    public Task InsertLinesAsync(int journalEntryId, IReadOnlyList<JournalDraftLine> lines)
    {
        var table = new DataTable();
        table.Columns.Add("LineNumber", typeof(int));
        table.Columns.Add("AccountId", typeof(int));
        table.Columns.Add("Debit", typeof(decimal));
        table.Columns.Add("Credit", typeof(decimal));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("CustomerId", typeof(int));
        table.Columns.Add("SupplierId", typeof(int));

        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            table.Rows.Add(
                i + 1,
                l.AccountId,
                l.Debit,
                l.Credit,
                (object?)l.Description ?? DBNull.Value,
                (object?)l.CustomerId ?? DBNull.Value,
                (object?)l.SupplierId ?? DBNull.Value);
        }

        var tvp = table.AsTableValuedParameter("dbo.JournalEntryLineTvp");

        return _uow.Connection.ExecuteAsync(new CommandDefinition(
            JournalSql.InsertLines,
            new { JournalEntryId = journalEntryId, Lines = tvp },
            _uow.Transaction));
    }

    public Task ReconcileTotalsAsync(int journalEntryId) =>
        _uow.Connection.ExecuteAsync(new CommandDefinition(
            JournalSql.ReconcileTotals, new { JournalEntryId = journalEntryId }, _uow.Transaction));

    public async Task<JournalEntryResponse?> GetByIdAsync(int journalEntryId)
    {
        using var grid = await _uow.Connection.QueryMultipleAsync(new CommandDefinition(
            JournalSql.GetById, new { JournalEntryId = journalEntryId }, _uow.Transaction));

        var header = await grid.ReadSingleOrDefaultAsync<JournalEntryHeaderRow>();
        if (header is null)
            return null;

        var lines = (await grid.ReadAsync<JournalEntryLineResponse>()).ToList();
        return Map(header, lines);
    }

    public async Task<PagedResult<JournalEntryResponse>> ListAsync(JournalEntryQuery query)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var parameters = new
        {
            query.FromDate,
            query.ToDate,
            query.SourceType,
            query.AccountId,
            page.Skip,
            page.Take
        };

        using var grid = await _uow.Connection.QueryMultipleAsync(
            new CommandDefinition(JournalSql.List, parameters, _uow.Transaction));

        var headers = (await grid.ReadAsync<JournalEntryHeaderRow>()).ToList();
        var total = await grid.ReadSingleAsync<int>();

        var items = headers
            .Select(h => Map(h, Array.Empty<JournalEntryLineResponse>()))
            .ToList();

        return new PagedResult<JournalEntryResponse>(items, page.Page, page.PageSize, total);
    }

    private static JournalEntryResponse Map(JournalEntryHeaderRow h, IReadOnlyList<JournalEntryLineResponse> lines) =>
        new(
            h.JournalEntryId,
            h.EntryNumber,
            h.EntryDate,
            h.Description,
            ((JournalSourceType)h.SourceType).ToString(),
            h.SourceId,
            h.IsReversal,
            h.ReversesJournalEntryId,
            h.TotalDebit,
            h.TotalCredit,
            h.CreatedBy,
            h.CreatedAtUtc,
            lines);

    private sealed class JournalEntryHeaderRow
    {
        public int JournalEntryId { get; set; }
        public string EntryNumber { get; set; } = default!;
        public DateOnly EntryDate { get; set; }
        public string Description { get; set; } = default!;
        public byte SourceType { get; set; }
        public int? SourceId { get; set; }
        public bool IsReversal { get; set; }
        public int? ReversesJournalEntryId { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public string CreatedBy { get; set; } = default!;
        public DateTime CreatedAtUtc { get; set; }
    }
}

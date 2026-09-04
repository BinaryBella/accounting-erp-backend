namespace AccountingERP.Application.Dtos;

public sealed record AccountResponse(
    int AccountId,
    string AccountCode,
    string AccountName,
    byte AccountTypeId,
    string AccountTypeName,
    string NormalBalance,
    bool IsBalanceSheet,
    int? ParentAccountId,
    bool IsActive,
    bool IsSystem,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record AccountTypeResponse(
    byte AccountTypeId,
    string Name,
    string NormalBalance,
    bool IsBalanceSheet);

public sealed record CreateAccountRequest(
    string AccountCode,
    string AccountName,
    byte AccountTypeId,
    int? ParentAccountId);

public sealed record UpdateAccountRequest(
    string AccountCode,
    string AccountName,
    byte AccountTypeId,
    int? ParentAccountId,
    bool IsActive);

public sealed record AccountQuery(
    byte? AccountType,
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 50);

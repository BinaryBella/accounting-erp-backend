namespace AccountingERP.Application.Dtos;

// --- Customer ---------------------------------------------------------------

public sealed record CustomerResponse(
    int CustomerId,
    string CustomerCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateCustomerRequest(
    string CustomerCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address);

public sealed record UpdateCustomerRequest(
    string CustomerCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive);

// --- Supplier -------------------------------------------------------------

public sealed record SupplierResponse(
    int SupplierId,
    string SupplierCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateSupplierRequest(
    string SupplierCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address);

public sealed record UpdateSupplierRequest(
    string SupplierCode,
    string Name,
    string? ContactPerson,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive);

// --- Shared query -------------------------------------------------------------

public sealed record PartyQuery(
    string? Search,
    bool? IsActive,
    int Page = 1,
    int PageSize = 50);

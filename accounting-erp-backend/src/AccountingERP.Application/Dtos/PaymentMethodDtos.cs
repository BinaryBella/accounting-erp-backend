namespace AccountingERP.Application.Dtos;

public sealed record PaymentMethodResponse(
    byte PaymentMethodId,
    string Name,
    string LedgerAccountCode,
    string LedgerAccountName,
    bool IsActive);

/// <summary>Internal lookup used by the posting engine to find the Cash/Bank ledger account.</summary>
public sealed record PaymentMethodInfo(
    byte PaymentMethodId,
    string Name,
    int LedgerAccountId,
    bool IsActive);

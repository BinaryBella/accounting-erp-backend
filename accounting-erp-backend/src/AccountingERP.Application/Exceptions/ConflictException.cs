namespace AccountingERP.Application.Exceptions;

/// <summary>
/// The request collides with current state: a duplicate code, posting an
/// already-posted document, editing/deleting a posted document, a payment that
/// exceeds the outstanding amount, deleting an account that has journal lines.
/// Mapped to HTTP 409 (PLAN §7).
/// </summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string message) : base(message) { }
}

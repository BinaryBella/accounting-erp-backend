namespace AccountingERP.Application.Exceptions;

/// <summary>
/// Base type for every expected, client-facing error. The API's
/// ExceptionHandlingMiddleware maps each concrete subclass to a status code
/// (PLAN §7); anything that is not an <see cref="AppException"/> becomes a 500.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }

    protected AppException(string message, Exception inner) : base(message, inner) { }
}

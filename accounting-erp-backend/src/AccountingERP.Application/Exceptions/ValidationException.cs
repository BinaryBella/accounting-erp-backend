namespace AccountingERP.Application.Exceptions;

/// <summary>
/// One or more request fields failed a business/validation rule. Mapped to HTTP 400
/// with an <c>errors</c> dictionary in the ProblemDetails body (PLAN §7, §9).
/// </summary>
public sealed class ValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string error)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]> { [field] = new[] { error } };
    }
}

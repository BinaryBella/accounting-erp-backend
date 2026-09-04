namespace AccountingERP.Application.Exceptions;

/// <summary>Requested entity does not exist. Mapped to HTTP 404.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entity, object key)
        : base($"{entity} '{key}' was not found.") { }
}

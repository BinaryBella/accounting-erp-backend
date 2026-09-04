namespace AccountingERP.Domain.Enums;

/// <summary>Lifecycle of a sales invoice / supplier bill / payment. Persisted as TINYINT.</summary>
public enum DocumentStatus : byte
{
    Draft = 1,
    Posted = 2,
    Reversed = 3
}

namespace AccountingERP.Application.Abstractions;

public interface INumberSequenceRepository
{
    /// <summary>
    /// Atomically allocates the next formatted document number for a sequence key
    /// (single <c>UPDATE … OUTPUT</c> under <c>UPDLOCK, ROWLOCK</c> — gap-free under
    /// concurrency, PLAN §2.10). Runs inside the caller's transaction, so a rolled-back
    /// posting releases the number.
    /// </summary>
    Task<string> NextAsync(string sequenceKey);
}

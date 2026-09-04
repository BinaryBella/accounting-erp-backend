using AccountingERP.Application.Exceptions;
using FluentValidation;

namespace AccountingERP.Application.Validation;

public static class ValidatorExtensions
{
    /// <summary>
    /// Runs the validator and, on failure, throws the application's own
    /// <see cref="ValidationException"/> (HTTP 400 + <c>errors</c> dictionary) rather
    /// than FluentValidation's — so the API layer never references FluentValidation.
    /// </summary>
    public static async Task EnsureValidAsync<T>(this IValidator<T> validator, T instance, CancellationToken ct = default)
    {
        var result = await validator.ValidateAsync(instance, ct);
        if (result.IsValid)
            return;

        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        throw new ValidationException(errors);
    }
}

// Within this assembly, an unqualified "ValidationException" always means the
// application's own type (HTTP 400 + errors dictionary), never FluentValidation's.
global using ValidationException = AccountingERP.Application.Exceptions.ValidationException;

namespace Sinchrony.Domain.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }
    public int HttpStatus { get; }

    // Campos extras do corpo do erro (ex.: lista de conflitos), mesclados em `error` pelo middleware.
    public IReadOnlyDictionary<string, object?>? Extensions { get; }

    public DomainException(string code, string message, int httpStatus = 400,
        IReadOnlyDictionary<string, object?>? extensions = null)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
        Extensions = extensions;
    }

    public static DomainException NotFound(string message) => new("NOT_FOUND", message, 404);
    public static DomainException Conflict(string code, string message) => new(code, message, 409);
    public static DomainException Conflict(string code, string message, IReadOnlyDictionary<string, object?> extensions)
        => new(code, message, 409, extensions);
    public static DomainException Unauthorized(string message) => new("UNAUTHORIZED", message, 401);
    public static DomainException Forbidden(string message) => new("FORBIDDEN", message, 403);
    public static DomainException Validation(string code, string message) => new(code, message, 422);
}
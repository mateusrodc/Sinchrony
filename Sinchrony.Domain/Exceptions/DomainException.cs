namespace Sinchrony.Domain.Exceptions;

public class DomainException : Exception
{
    public string Code { get; }
    public int HttpStatus { get; }

    public DomainException(string code, string message, int httpStatus = 400)
        : base(message)
    {
        Code = code;
        HttpStatus = httpStatus;
    }

    public static DomainException NotFound(string message) => new("NOT_FOUND", message, 404);
    public static DomainException Conflict(string code, string message) => new(code, message, 409);
    public static DomainException Unauthorized(string message) => new("UNAUTHORIZED", message, 401);
    public static DomainException Forbidden(string message) => new("FORBIDDEN", message, 403);
    public static DomainException Validation(string code, string message) => new(code, message, 422);
}
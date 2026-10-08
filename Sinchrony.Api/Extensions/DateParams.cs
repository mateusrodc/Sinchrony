using FluentValidation;
using FluentValidation.Results;

namespace Sinchrony.Api.Extensions;

// Parâmetros de data de query (yyyy-MM-dd). Data inválida vira 422 em vez de ser ignorada:
// ignorar `from`/`to` devolveria todas as aulas, justamente o que o filtro quer evitar.
public static class DateParams
{
    public static DateOnly? Parse(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", out var d))
            throw Invalid(field, $"{field} deve estar no formato yyyy-MM-dd.");
        return d;
    }

    public static DateOnly Require(string? value, string field)
        => Parse(value, field) ?? throw Invalid(field, $"{field} é obrigatório (yyyy-MM-dd).");

    public static (DateOnly? From, DateOnly? To) ParseRange(string? from, string? to)
    {
        var f = Parse(from, "from");
        var t = Parse(to, "to");
        if (f.HasValue && t.HasValue && f > t) throw Invalid("to", "to não pode ser anterior a from.");
        return (f, t);
    }

    private static ValidationException Invalid(string field, string message)
        => new([new ValidationFailure(field, message)]);
}

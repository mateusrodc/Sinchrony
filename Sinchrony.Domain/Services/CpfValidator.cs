namespace Sinchrony.Domain.Services;

public static class CpfValidator
{
    public static bool IsValid(string? cpf)
    {
        if (string.IsNullOrEmpty(cpf)) return false;

        cpf = cpf.Replace(".", "").Replace("-", "").Trim();

        if (cpf.Length != 11) return false;
        if (cpf.Distinct().Count() == 1) return false; // todos dígitos iguais

        // 1º dígito verificador
        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += int.Parse(cpf[i].ToString()) * (10 - i);
        var rem = (sum * 10) % 11;
        if (rem == 10) rem = 0;
        if (rem != int.Parse(cpf[9].ToString())) return false;

        // 2º dígito verificador
        sum = 0;
        for (var i = 0; i < 10; i++)
            sum += int.Parse(cpf[i].ToString()) * (11 - i);
        rem = (sum * 10) % 11;
        if (rem == 10) rem = 0;
        return rem == int.Parse(cpf[10].ToString());
    }

    public static string Sanitize(string cpf)
        => cpf.Replace(".", "").Replace("-", "").Trim();
}
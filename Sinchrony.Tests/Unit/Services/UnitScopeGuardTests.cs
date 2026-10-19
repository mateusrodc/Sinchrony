using Xunit;
using FluentAssertions;

namespace Sinchrony.Tests.Unit.Services;

// Trava de regressão: a secretária (admin sem User.UnitId, com unidade em "Unidades (filiais)") voltava
// para o login porque rotas comparavam só `unitContext.UnitId` e respondiam `Forbid()` (403 sem
// corpo). Toda decisão de unidade passa por UnitScope; recusa sempre com código e mensagem.
public class UnitScopeGuardTests
{
    private static string ApiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Sinchrony.slnx")))
            dir = dir.Parent;
        dir.Should().NotBeNull("a raiz da solution precisa ser encontrada");
        return Path.Combine(dir!.FullName, "Sinchrony.Api");
    }

    private static IEnumerable<string> SourceFiles(string sub) =>
        Directory.EnumerateFiles(Path.Combine(ApiRoot(), sub), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    [Fact]
    public void Controllers_NeverReturnForbidWithoutBody()
    {
        var offenders = SourceFiles("Controllers")
            .Where(f => File.ReadAllText(f).Contains("Forbid("))
            .Select(Path.GetFileName);

        offenders.Should().BeEmpty("use DomainException (UNIT_FORBIDDEN, NO_UNIT_ASSIGNED...) para 403 com corpo");
    }

    [Fact]
    public void Controllers_NeverCompareUserUnitIdDirectly()
    {
        var offenders = SourceFiles("Controllers")
            .Where(f => File.ReadAllText(f).Contains("unitContext.UnitId"))
            .Select(Path.GetFileName);

        offenders.Should().BeEmpty("use UnitScope (GetScopeAsync / EnsureCanAccessUnitAsync / ResolveDefaultUnitIdAsync)");
    }
}

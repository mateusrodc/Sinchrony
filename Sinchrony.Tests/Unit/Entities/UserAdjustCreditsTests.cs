using FluentAssertions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Xunit;

namespace Sinchrony.Tests.Unit.Entities;

// Fase 1 — DEMANDA_CONTROLE_ADMIN_PERMISSOES_BACKEND.md. AdjustCredits é a ferramenta que
// aposenta a correção via SQL direto no banco; estes testes cobrem exatamente a pendência
// técnica resolvida em 15/09/2026 (ck_users_credits nunca pode estourar como exceção crua).
public class UserAdjustCreditsTests
{
    private static User CreateStudent(int credits)
    {
        var u = User.Create("Student", "student@test.com", null, "hash", Role.student);
        if (credits > 0) u.AddCredits(credits);
        return u;
    }

    [Fact]
    public void ExpireCredits_ZeroesBalanceAndReturnsExpiredAmount()
    {
        var student = CreateStudent(5);

        var expired = student.ExpireCredits();

        expired.Should().Be(5);
        student.Credits.Should().Be(0);
    }

    [Fact]
    public void ExpireCredits_WithKeep_PreservesThatMuch()
    {
        var student = CreateStudent(9);

        var expired = student.ExpireCredits(keep: 8);

        expired.Should().Be(1);
        student.Credits.Should().Be(8);
    }

    [Fact]
    public void ExpireCredits_KeepGreaterThanBalance_ExpiresNothing()
    {
        var student = CreateStudent(3);

        student.ExpireCredits(keep: 8).Should().Be(0);
        student.Credits.Should().Be(3);
    }

    [Fact]
    public void AdjustCredits_PositiveDelta_IncreasesBalance()
    {
        var student = CreateStudent(1);

        student.AdjustCredits(31, "Correção de pacote com CreditsPerMember indevido");

        student.Credits.Should().Be(32);
    }

    [Fact]
    public void AdjustCredits_NegativeDeltaWithinBalance_DecreasesBalance()
    {
        var student = CreateStudent(32);

        student.AdjustCredits(-31, "Correção: pacote de 1 crédito havia concedido 32");

        student.Credits.Should().Be(1);
    }

    [Fact]
    public void AdjustCredits_WouldGoNegative_ThrowsCleanDomainException_NotDbConstraintViolation()
    {
        var student = CreateStudent(1);

        var act = () => student.AdjustCredits(-5, "Ajuste inválido");

        var ex = act.Should().Throw<DomainException>().Which;
        ex.Code.Should().Be("CREDITS_WOULD_BE_NEGATIVE");
        student.Credits.Should().Be(1); // estado não mutado quando a validação falha
    }

    [Fact]
    public void AdjustCredits_ExactlyToZero_Allowed()
    {
        var student = CreateStudent(5);

        student.AdjustCredits(-5, "Zerando saldo");

        student.Credits.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void AdjustCredits_EmptyReason_ThrowsValidation(string? reason)
    {
        var student = CreateStudent(10);

        var act = () => student.AdjustCredits(5, reason!);

        var ex = act.Should().Throw<DomainException>().Which;
        ex.Code.Should().Be("REASON_REQUIRED");
    }
}

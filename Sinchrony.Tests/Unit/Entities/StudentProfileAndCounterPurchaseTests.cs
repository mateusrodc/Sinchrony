using FluentAssertions;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Exceptions;
using Xunit;

namespace Sinchrony.Tests.Unit.Entities;

public class StudentProfileAndCounterPurchaseTests
{
    private static User Student() => User.Create("Student", "s@test.com", null, "hash", Role.student);

    [Fact]
    public void SetBirthDate_InTheFuture_IsRejected()
    {
        var act = () => Student().SetBirthDate(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_BIRTHDATE");
    }

    [Fact]
    public void SetBirthDate_Valid_IsStored()
    {
        var u = Student();
        u.SetBirthDate(new DateOnly(1990, 10, 5));

        u.BirthDate.Should().Be(new DateOnly(1990, 10, 5));
    }

    [Fact]
    public void SetNotes_Over1000Chars_IsRejected()
    {
        var act = () => Student().SetNotes(new string('x', 1001));

        act.Should().Throw<DomainException>().Which.Code.Should().Be("NOTES_TOO_LONG");
    }

    [Fact]
    public void SetNotes_TrimsAndClearsWhenBlank()
    {
        var u = Student();
        u.SetNotes("  lesão no joelho  ");
        u.Notes.Should().Be("lesão no joelho");

        u.SetNotes("   ");
        u.Notes.Should().BeNull();
    }

    [Theory]
    [InlineData("  obs  ", "obs")]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void NormalizeNotes_MatchesWhatSetNotesStores(string? input, string? expected)
    {
        User.NormalizeNotes(input).Should().Be(expected);
        var u = Student();
        u.SetNotes(input);
        u.Notes.Should().Be(User.NormalizeNotes(input));
    }

    [Fact]
    public void AnonymizeForDeletion_ClearsBirthDateAndNotes()
    {
        var u = Student();
        u.SetBirthDate(new DateOnly(1990, 10, 5));
        u.SetNotes("obs");

        u.AnonymizeForDeletion("hash2");

        u.BirthDate.Should().BeNull();
        u.Notes.Should().BeNull();
    }

    [Theory]
    [InlineData("pix")]
    [InlineData("card")]
    [InlineData("cash")]
    [InlineData("courtesy")]
    public void CounterPurchase_IsConfirmedAndBalcao_ForAllFourMethods(string method)
    {
        var p = Purchase.CreateCounterConfirmed(Guid.NewGuid(), Guid.NewGuid(), 50m, method);

        p.Channel.Should().Be("balcao");
        p.Status.Should().Be("confirmed");
        p.PaymentMethod.Should().Be(method);
    }

    [Fact]
    public void AppPurchases_DefaultToAppChannel()
    {
        Purchase.CreatePending(Guid.NewGuid(), Guid.NewGuid(), 50m, "pix", "pay_1").Channel.Should().Be("app");
        Purchase.Create(Guid.NewGuid(), Guid.NewGuid(), 50m, "card", "pay_2").Channel.Should().Be("app");
        Purchase.CreateRenewalPending(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 50m, "pay_3")
            .Channel.Should().Be("app");
    }
}

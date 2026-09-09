using FluentAssertions;
using Moq;
using Sinchrony.Application.Packages.Commands.CreatePackage;
using Sinchrony.Application.Packages.Commands.UpdatePackage;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Commands;

// Guarda de regressão para o bug de "pacote de 1 crédito virou 32 créditos": impede que
// CreditsPerMember seja salvo em um pacote sem dependentes, onde ele nunca deveria ser lido.
public class PackageCreditsPerMemberValidationTests
{
    private readonly Mock<IPackageRepository> _packageRepo = new();
    private readonly Mock<IBenefitRepository> _benefitRepo = new();

    [Fact]
    public async Task CreatePackage_CreditsPerMemberWithoutDependents_ThrowsValidation()
    {
        var handler = new CreatePackageCommandHandler(_packageRepo.Object, _benefitRepo.Object);
        var command = new CreatePackageCommand(
            "Aula Avulsa", null, Credits: 1, Price: 89m, ValidityDays: 30,
            Popular: false, Active: true, DisplayOrder: 0,
            MaxDependents: 0, CreditsPerMember: 32);

        var act = () => handler.Handle(command, default);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("CREDITS_PER_MEMBER_NOT_ALLOWED");
    }

    [Fact]
    public async Task CreatePackage_CreditsPerMemberWithDependents_Succeeds()
    {
        _packageRepo.Setup(r => r.AddAsync(It.IsAny<Package>(), default)).Returns(Task.CompletedTask);
        _packageRepo.Setup(r => r.SaveAsync(default)).Returns(Task.CompletedTask);
        var handler = new CreatePackageCommandHandler(_packageRepo.Object, _benefitRepo.Object);
        var command = new CreatePackageCommand(
            "Plano Família", null, Credits: 20, Price: 300m, ValidityDays: 30,
            Popular: false, Active: true, DisplayOrder: 0,
            MaxDependents: 2, CreditsPerMember: 5);

        var result = await handler.Handle(command, default);

        result.CreditsPerMember.Should().Be(5);
    }

    [Fact]
    public async Task UpdatePackage_CreditsPerMemberWithoutDependents_ThrowsValidation()
    {
        var handler = new UpdatePackageCommandHandler(_packageRepo.Object, _benefitRepo.Object);
        var command = new UpdatePackageCommand(
            Guid.NewGuid(), "Aula Avulsa", null, Credits: 1, Price: 89m, ValidityDays: 30,
            Popular: false, Active: true, DisplayOrder: 0,
            PackageTypeId: Guid.NewGuid(), PurchaseStrategy: "block",
            MaxDependents: 0, CreditsPerMember: 32,
            MaxFutureBookings: null, MaxBookingsPerDay: null, MaxBookingsPerWeek: null,
            MaxBookingsPerMonth: null, CancellationDeadlineHours: null, BookingWindowDays: null,
            EarlyAccessHours: null, AllowWaitlist: null, WaitlistPriority: null,
            ReschedulingAllowed: null, ReschedulingDeadlineHours: null,
            NoShowCreditPenalty: true, MaxNoShowsBeforeBlock: null, NoShowBlockWindowDays: 30,
            BenefitIds: []);

        var act = () => handler.Handle(command, default);

        var ex = await act.Should().ThrowAsync<DomainException>();
        ex.Which.Code.Should().Be("CREDITS_PER_MEMBER_NOT_ALLOWED");

        // Não deve nem tentar carregar o pacote — falha antes de qualquer acesso a dados.
        _packageRepo.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}

using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Domain.Services;
using Sinchrony.Infrastructure.Services;

namespace Sinchrony.Tests.Unit.Services;

public class NoShowPenaltyServiceTests
{
    private readonly Mock<IStudentPackageRepository> _packages = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly User _student = User.Create("Aluna", "aluna@test.com", null, "hash", Role.student);

    public NoShowPenaltyServiceTests()
    {
        _users.Setup(r => r.GetByIdAsync(_student.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_student);
        _users.Setup(r => r.SaveAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
    }

    private NoShowPenaltyService CreateService(int? freeNoShows, bool penalty = true, int noShowsInDb = 0)
    {
        var package = Package.Create("Plano", null, 8, 100m, 30, false, true, 1);
        package.UpdateFull("Plano", null, 8, 100m, 30, false, true, 1, null, "block", 0, null,
            null, null, null, null, null, null, null, null, null, null, null,
            penalty, null, 30);
        package.SetTermsRules(freeNoShows, null, null);

        var sp = StudentPackage.Create(_student.Id, package.Id, 30);
        typeof(StudentPackage).GetProperty(nameof(StudentPackage.Package))!.SetValue(sp, package);

        _packages.Setup(r => r.GetActiveByStudentAsync(_student.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sp);
        _bookings.Setup(r => r.CountNoShowsSinceAsync(
                _student.Id, null, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(noShowsInDb);

        return new NoShowPenaltyService(
            _packages.Object, _users.Object, _bookings.Object, Mock.Of<IPackageQueueService>(),
            NullLogger<NoShowPenaltyService>.Instance);
    }

    [Fact]
    public async Task Apply_FirstNoShowWithOneFree_ReturnsCredit()
    {
        var service = CreateService(freeNoShows: 1, noShowsInDb: 1);
        await service.ApplyAsync(_student.Id);
        _student.Credits.Should().Be(1);
    }

    [Fact]
    public async Task Apply_SecondNoShowWithOneFree_KeepsCreditConsumed()
    {
        var service = CreateService(freeNoShows: 1, noShowsInDb: 2);
        await service.ApplyAsync(_student.Id);
        _student.Credits.Should().Be(0);
    }

    [Fact]
    public async Task Apply_WithoutRule_BehavesAsBefore()
    {
        var service = CreateService(freeNoShows: null, noShowsInDb: 1);
        await service.ApplyAsync(_student.Id);
        _student.Credits.Should().Be(0);
    }

    [Fact]
    public async Task Apply_PenaltyDisabled_AlwaysReturnsCredit()
    {
        var service = CreateService(freeNoShows: null, penalty: false, noShowsInDb: 5);
        await service.ApplyAsync(_student.Id);
        _student.Credits.Should().Be(1);
    }

    [Fact]
    public async Task Reverse_UndoingFirstNoShow_RemovesReturnedCredit()
    {
        _student.AddCredits(1);
        var service = CreateService(freeNoShows: 1, noShowsInDb: 0); // já sem a falta desfeita
        (await service.ReverseAsync(_student.Id)).Should().BeTrue();
        _student.Credits.Should().Be(0);
    }

    [Fact]
    public async Task Reverse_UndoingSecondNoShow_DoesNotTouchBalance()
    {
        _student.AddCredits(3);
        var service = CreateService(freeNoShows: 1, noShowsInDb: 1); // sobrou a 1ª falta
        (await service.ReverseAsync(_student.Id)).Should().BeTrue();
        _student.Credits.Should().Be(3);
    }
}

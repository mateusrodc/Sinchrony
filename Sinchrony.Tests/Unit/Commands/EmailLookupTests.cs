using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Commands;

// Recuperar senha falhava em silêncio quando o e-mail digitado tinha espaço no fim ou maiúsculas.
public class EmailLookupTests
{
    [Theory]
    [InlineData("ana@test.com")]
    [InlineData("Ana@Test.com")]
    [InlineData("  ana@test.com ")]
    [InlineData("ANA@TEST.COM\n")]
    public async Task GetByEmail_IgnoresCaseAndSurroundingSpaces(string typed)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new ApplicationDbContext(options);
        var user = User.Create("Ana", "ana@test.com", null, "h", Role.student);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var found = await new UserRepository(db).GetByEmailAsync(typed);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }
}

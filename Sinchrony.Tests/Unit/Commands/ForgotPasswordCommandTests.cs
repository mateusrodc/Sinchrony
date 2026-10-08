using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Sinchrony.Application.Auth.Commands.ForgotPassword;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Enums;
using Sinchrony.Domain.Interfaces.Repositories;
using Sinchrony.Domain.Interfaces.Services;
using Sinchrony.Infrastructure.Persistence;
using Sinchrony.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Sinchrony.Tests.Unit.Commands;

// DEMANDA_EMAIL_REDEFINIR_SENHA_NAO_ENVIADO_BACKEND.md — o envio roda depois da resposta, quando o
// DbContext do escopo da requisição já foi descartado; as settings têm de ser lidas antes.
public class ForgotPasswordCommandTests
{
    private readonly User _user = User.Create("Ana", "ana@test.com", null, "h", Role.student);
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPasswordResetTokenRepository> _tokens = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<ILogger<ForgotPasswordCommandHandler>> _logger = new();
    private readonly IConfiguration _config = new ConfigurationBuilder().Build();

    public ForgotPasswordCommandTests()
    {
        _users.Setup(u => u.GetByEmailAsync("ana@test.com", It.IsAny<CancellationToken>())).ReturnsAsync(_user);
        _logger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private (ForgotPasswordCommandHandler Handler, ApplicationDbContext Db) NewHandler()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new ApplicationDbContext(options);
        db.Settings.Add(new Settings());
        db.SaveChanges();
        var handler = new ForgotPasswordCommandHandler(
            _users.Object, _tokens.Object, _email.Object, new SettingsRepository(db), _config, _logger.Object);
        return (handler, db);
    }

    [Fact]
    public async Task Handle_SendsEmail_EvenAfterTheRequestDbContextIsDisposed()
    {
        var (handler, db) = NewHandler();
        var sent = new TaskCompletionSource<Settings?>();
        var release = new TaskCompletionSource();
        _email.Setup(e => e.SendWithSettingsAsync(
                "ana@test.com", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Settings?>(), It.IsAny<CancellationToken>()))
            .Returns(async (string _, string _, string _, Settings? s, CancellationToken _) =>
            {
                await release.Task; // garante que o envio só segue depois do descarte do contexto
                sent.SetResult(s);
            });

        await handler.Handle(new ForgotPasswordCommand("ana@test.com"), default);
        await db.DisposeAsync(); // a requisição terminou
        release.SetResult();

        var settings = await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        settings.Should().NotBeNull(); // veio lido antes, não buscado depois
    }

    [Fact]
    public async Task Handle_ReadsSettingsOnlyDuringTheRequest()
    {
        var settingsRepo = new Mock<ISettingsRepository>();
        var calls = 0;
        settingsRepo.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ++calls == 1 ? new Settings() : throw new ObjectDisposedException("ApplicationDbContext"));
        var sent = new TaskCompletionSource();
        _email.Setup(e => e.SendWithSettingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Settings?>(), It.IsAny<CancellationToken>()))
            .Returns(() => { sent.SetResult(); return Task.CompletedTask; });
        var handler = new ForgotPasswordCommandHandler(
            _users.Object, _tokens.Object, _email.Object, settingsRepo.Object, _config, _logger.Object);

        await handler.Handle(new ForgotPasswordCommand("ana@test.com"), default);
        await sent.Task.WaitAsync(TimeSpan.FromSeconds(5));

        calls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSendFails_LogsErrorWithFlowAndRecipient()
    {
        var (handler, _) = NewHandler();
        var logged = new TaskCompletionSource<string>();
        _logger.Setup(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(new InvocationAction(i =>
            {
                var state = i.Arguments[2];
                var formatter = i.Arguments[4];
                var msg = (string)formatter.GetType().GetMethod("Invoke")!.Invoke(formatter, [state, i.Arguments[3]])!;
                logged.TrySetResult(msg);
            }));
        _email.Setup(e => e.SendWithSettingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Settings?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp errado"));

        await handler.Handle(new ForgotPasswordCommand("ana@test.com"), default);

        var message = await logged.Task.WaitAsync(TimeSpan.FromSeconds(5));
        message.Should().Contain("Redefinição de senha").And.Contain("ana@test.com");
    }

    [Fact]
    public async Task Handle_UnknownEmail_SendsNothing()
    {
        var (handler, _) = NewHandler();

        await handler.Handle(new ForgotPasswordCommand("naoexiste@test.com"), default);

        _email.Verify(e => e.SendWithSettingsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Settings?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

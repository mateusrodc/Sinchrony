using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Notifications.Commands.UpdateEmail;

public record UpdateEmailCommand(Guid UserId, bool Enabled) : IRequest;

public class UpdateEmailCommandHandler(INotificationPreferenceRepository repository)
    : IRequestHandler<UpdateEmailCommand>
{
    public async Task Handle(UpdateEmailCommand request, CancellationToken ct)
    {
        var pref = await repository.GetByUserIdAsync(request.UserId, ct);
        if (pref is null) { pref = NotificationPreference.CreateDefault(request.UserId); await repository.AddAsync(pref, ct); }
        pref.SetEmail(request.Enabled);
        await repository.SaveAsync(ct);
    }
}
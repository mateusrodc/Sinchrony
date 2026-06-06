using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Notifications.Commands.UpdatePush;

public record UpdatePushCommand(Guid UserId, bool Enabled) : IRequest;

public class UpdatePushCommandHandler(INotificationPreferenceRepository repository)
    : IRequestHandler<UpdatePushCommand>
{
    public async Task Handle(UpdatePushCommand request, CancellationToken ct)
    {
        var pref = await repository.GetByUserIdAsync(request.UserId, ct);
        if (pref is null) { pref = NotificationPreference.CreateDefault(request.UserId); await repository.AddAsync(pref, ct); }
        pref.SetPush(request.Enabled);
        await repository.SaveAsync(ct);
    }
}
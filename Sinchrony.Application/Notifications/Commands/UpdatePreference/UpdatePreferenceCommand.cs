using MediatR;
using Sinchrony.Domain.Entities;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Notifications.Commands.UpdatePreference;

public record UpdatePreferenceCommand(Guid UserId, string PreferenceId, bool Enabled) : IRequest;

public class UpdatePreferenceCommandHandler(INotificationPreferenceRepository repository)
    : IRequestHandler<UpdatePreferenceCommand>
{
    public async Task Handle(UpdatePreferenceCommand request, CancellationToken ct)
    {
        var pref = await repository.GetByUserIdAsync(request.UserId, ct);
        if (pref is null)
        {
            pref = NotificationPreference.CreateDefault(request.UserId);
            await repository.AddAsync(pref, ct);
        }

        pref.UpdatePreference(request.PreferenceId, request.Enabled);
        await repository.SaveAsync(ct);
    }
}
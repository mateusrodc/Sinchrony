using MediatR;
using Sinchrony.Domain.Exceptions;
using Sinchrony.Domain.Interfaces.Repositories;

namespace Sinchrony.Application.Referrals.Queries.GetReferral;

public record GetReferralQuery(Guid UserId) : IRequest<ReferralDto>;

public record ReferralDto(string Code, string Url, int TotalReferrals, int TotalCreditsEarned);

public class GetReferralQueryHandler(IUserRepository userRepository, IReferralRepository referralRepository)
    : IRequestHandler<GetReferralQuery, ReferralDto>
{
    public async Task<ReferralDto> Handle(GetReferralQuery request, CancellationToken ct)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        var referrals = (await referralRepository.ListByReferrerAsync(request.UserId, ct)).ToList();

        return new ReferralDto(
            user.ReferralCode ?? string.Empty,
            $"https://studio.com/ref/{user.ReferralCode}",
            referrals.Count,
            referrals.Sum(r => r.CreditsEarned));
    }
}
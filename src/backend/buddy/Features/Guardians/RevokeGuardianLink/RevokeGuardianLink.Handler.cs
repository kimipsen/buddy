using buddy.Common;

namespace buddy.Features.Guardians;

public static class RevokeGuardianLinkHandler
{
    public static async Task<Result<Unit>> Handle(RevokeGuardianLink command, IGuardianLinkEventStore guardianLinks, CancellationToken cancellationToken)
    {
        var guardianId = command.GuardianId;

        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, guardianId, cancellationToken);

        if (link is null)
        {
            return new Result<Unit>.NotFound();
        }

        await guardianLinks.AppendAsync(
            new GuardianLinkId(link.GuardianLinkId),
            [new GuardianRevoked(new GuardianLinkId(link.GuardianLinkId), DateTimeOffset.UtcNow)],
            cancellationToken);

        return new Result<Unit>.Success(Unit.Value);
    }
}

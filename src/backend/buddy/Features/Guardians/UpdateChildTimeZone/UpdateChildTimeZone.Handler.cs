using buddy.Common;
using buddy.Features.Calendars;
using buddy.Features.Users;

namespace buddy.Features.Guardians;

public static class UpdateChildTimeZoneHandler
{
    public static async Task<Result<ChildSummary>> Handle(
        UpdateChildTimeZone command, IGuardianLinkEventStore guardianLinks, IUserEventStore users, CancellationToken cancellationToken)
    {
        if (command.GuardianId is not { } guardianId)
        {
            return new Result<ChildSummary>.NotFound();
        }

        // Collapsed to NotFound rather than Forbidden -- same "can't distinguish no-such-child from
        // not-your-child" precedent RevokeGuardianLinkHandler follows for this exact lookup.
        var link = await guardianLinks.FindActiveLinkAsync(command.ChildId, guardianId, cancellationToken);

        if (link is null)
        {
            return new Result<ChildSummary>.NotFound();
        }

        if (!TimeZoneResolution.IsValid(command.TimeZoneId))
        {
            return new Result<ChildSummary>.Validation($"'{command.TimeZoneId.Value}' is not a recognized IANA time zone identifier.");
        }

        var existingEvents = await users.ReadAsync(command.ChildId, cancellationToken);
        var child = User.Rehydrate(existingEvents);

        if (child is null || child.IsDeleted)
        {
            return new Result<ChildSummary>.NotFound();
        }

        if (child.ResolvedTimeZoneId != command.TimeZoneId)
        {
            var timeZoneUpdated = new TimeZoneUpdated(command.ChildId, child.ResolvedTimeZoneId, command.TimeZoneId, DateTimeOffset.UtcNow);
            await users.AppendAsync(command.ChildId, [timeZoneUpdated], cancellationToken);
            child = child with { TimeZoneId = command.TimeZoneId };
        }

        return new Result<ChildSummary>.Success(new ChildSummary(
            child.Id, child.Name, new GuardianLinkId(link.GuardianLinkId), link.Kind, child.ResolvedLanguage, child.ResolvedTimeZoneId));
    }
}

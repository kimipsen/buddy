namespace buddy.Features.Guardians;

public sealed record PreviewGuardianInvite(string Token);

public sealed record GuardianInvitePreview(string ChildGivenName, GuardianKind Kind);

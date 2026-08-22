using buddy.Features.Guardians;

namespace buddy.IntegrationTests.Features.Guardians;

// Shared response shapes for the Guardians endpoint tests, matching ChildResponse/ChildSummary/
// GuardianSummary (Features/Guardians/*). Strongly-typed ids serialize as a raw Guid
// (StronglyTypedIdJsonConverterFactory); Name is a nested GivenName/FamilyName object.
internal sealed record NameDto(string GivenName, string FamilyName);

internal sealed record ChildResponseDto(Guid Id, NameDto Name, Guid GuardianLinkId, GuardianKind Kind, string Username, string TemporaryPassword);

internal sealed record ChildSummaryDto(Guid Id, NameDto Name, Guid GuardianLinkId, GuardianKind Kind);

internal sealed record GuardianSummaryDto(Guid Id, NameDto Name, Guid GuardianLinkId, GuardianKind Kind);

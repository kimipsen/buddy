namespace buddy.Features.VisitSummaries;

// Unauthenticated by design -- a doctor has no Buddy account. The token in the URL is the
// authentication, as for GetSharedSleepDiary. No from/to: the guardian fixed the period on the
// summary, so the link shows exactly that and nothing around it (Question 4).
public sealed record GetSharedVisitSummary(string Token);

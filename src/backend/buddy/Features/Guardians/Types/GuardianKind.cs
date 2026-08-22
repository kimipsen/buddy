namespace buddy.Features.Guardians;

// Descriptive/legal-record label only -- it never gates access. A Parent and a Guardian get the
// same default authority over the child's account (see CalendarAuthorization.ResolveRole).
public enum GuardianKind
{
    Parent,
    Guardian
}

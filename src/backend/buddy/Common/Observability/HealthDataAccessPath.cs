namespace buddy.Common.Observability;

// How a reader reached a child's health data, logged with every read of it (GDPR Question 7 in
// docs/backend/analysis/gdpr-data-protection.md): through a guardian link, as the child themself, or
// through a group the data is shared with.
public enum HealthDataAccessPath
{
    Guardian,
    Self,
    Group,
}

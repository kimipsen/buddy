using buddy.Common.Observability;

namespace buddy.Features.Medicines;

// Every read of a child's medicines -- health data, so who read it and how is logged (GDPR
// Question 7). See docs/backend/observability.md. IDs only: never a medicine name, dose or note.
internal static partial class MedicinesLog
{
    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "Medicine schedules of child {ChildId} read by {UserId} as {AccessPath}, group {GroupId} (empty: none)")]
    public static partial void MedicineSchedulesRead(this ILogger logger, Guid childId, Guid userId, HealthDataAccessPath accessPath, Guid? groupId);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Information, Message = "Medicine doses of child {ChildId} from {From} to {To} read by {UserId} as {AccessPath}, group {GroupId} (empty: none)")]
    public static partial void MedicineDosesRead(this ILogger logger, Guid childId, DateOnly from, DateOnly to, Guid userId, HealthDataAccessPath accessPath, Guid? groupId);
}

namespace buddy.Features.Medicines;

// Queryable read-model index kept alongside the MedicineSchedule event stream, so a child's
// schedules can be listed without scanning every stream in the store -- the same problem
// CalendarItemIndexDocument solves for calendar items. Unlike CalendarItemIndexDocument, this
// carries no "stopped" flag to filter on: a stopped schedule still belongs in
// ListMedicineSchedules (a guardian's history of a child's courses), so nothing is ever removed
// from this index. Only MedicineDoseExpansion, working off the rehydrated aggregate's IsStopped
// flag, decides whether a schedule still produces future doses.
public sealed record MedicineIndexDocument(Guid Id, Guid ChildId);

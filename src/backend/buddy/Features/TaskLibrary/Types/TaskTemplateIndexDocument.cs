namespace buddy.Features.TaskLibrary;

// Queryable read-model index kept alongside the TaskTemplate event stream, so a child's templates
// can be listed without scanning every stream in the store -- same problem MealIndexDocument
// solves for meals. Carries no "archived" flag: an archived TaskTemplate still belongs in
// ListTaskTemplates (a guardian's library, including retired templates), so nothing is ever
// removed from this index.
public sealed record TaskTemplateIndexDocument(Guid Id, Guid ChildId);

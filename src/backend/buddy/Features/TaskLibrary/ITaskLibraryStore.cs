using Marten;

namespace buddy.Features.TaskLibrary;

// One schema for the TaskTemplate stream, the same shared-marker-interface contract
// IMealplansStore/ICalendarsStore already use for their own feature's Marten store.
public interface ITaskLibraryStore : IDocumentStore;

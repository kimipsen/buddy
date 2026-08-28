# Task Library Flow

The task library lets a guardian define reusable routines as ordered task
templates with timed subtasks. Each template belongs to exactly one child. A
child can view their own library, while only an active guardian can create or
change it.

```mermaid
sequenceDiagram
    actor Guardian
    actor Child
    participant App as Client app
    participant API as Buddy API
    participant Library as Task Library feature
    participant Calendars as Calendars feature
    participant Store as Task template event store

    Guardian->>App: Create a reusable routine for Child
    App->>API: POST /task-templates/children/{childId}
    API->>Library: CreateTaskTemplate command
    Library->>Store: Append TaskTemplateCreated
    Store-->>Library: New template
    Library-->>API: Template response
    API-->>App: 200 OK

    Guardian->>App: Add and order timed subtasks
    App->>API: POST /task-templates/{templateId}/subtasks
    API->>Library: AddSubtask command
    Library->>Store: Append SubtaskAdded
    Library-->>API: Updated template
    API-->>App: 200 OK

    Child->>App: View their library
    App->>API: GET /task-templates/children/{childId}
    API->>Library: ListTaskTemplates query
    Library->>Store: Load templates for the child
    Library-->>API: Templates, including archived templates
    API-->>App: 200 OK

    Guardian->>App: Schedule the routine
    App->>API: POST /calendars/{calendarId}/items/from-template
    API->>Calendars: ScheduleTaskFromTemplate command
    Calendars->>Store: Validate the current template and its owning child
    Calendars-->>API: Calendar item linked to the template
    API-->>App: 200 OK
```

## Endpoints

| Method | Route | Behavior |
| --- | --- | --- |
| `POST` | `/task-templates/children/{childId}` | Creates a template owned by the child. |
| `GET` | `/task-templates/children/{childId}` | Lists the child's own templates by name, including archived templates and their subtasks. |
| `PATCH` | `/task-templates/{templateId}` | Updates a template's name, icon, and color. |
| `DELETE` | `/task-templates/{templateId}` | Archives a template and blocks new scheduling. |
| `POST` | `/task-templates/{templateId}/subtasks` | Adds a timed subtask, optionally at a requested position. |
| `PATCH` | `/task-templates/{templateId}/subtasks/{subtaskId}` | Updates a subtask's title, optional icon, and duration. |
| `DELETE` | `/task-templates/{templateId}/subtasks/{subtaskId}` | Removes a subtask. |
| `PUT` | `/task-templates/{templateId}/subtasks/order` | Reorders subtasks; the new order must contain each current subtask ID exactly once. |
| `POST` | `/calendars/{calendarId}/items/from-template` | Schedules a non-empty, active template owned by the assignee as a specific-time calendar task. |

Template and subtask names must be non-empty and at most 200 characters.
Subtask durations must be positive. Each template response includes the ordered
subtasks, their total duration, archive state, and creator/modifier IDs; there
is no separate endpoint for loading one template.

## Core lifecycle

`TaskTemplate` is event-sourced. Its stream begins with
`TaskTemplateCreated`, then records detail updates, subtask additions, updates,
removals and reordering. `TaskTemplateArchived` is a soft delete. Archived
templates remain visible in library listings and cannot be edited or scheduled
again.

A template is indexed under the child it was created for, and that ownership
never changes. Listing and lookup both resolve directly against that index
row -- there is no sharing across siblings.

Scheduling stores the template ID on the calendar item rather than copying the
subtasks. Occurrence listing and iCal generation load the current template and
produce one consecutive timed occurrence per subtask, starting at the scheduled
time. Later subtask edits therefore affect existing scheduled routines.
Archiving a template only prevents new schedules; already scheduled routines
continue to expand.

## Authorization model

The child named by `childId` and their active guardians can view that child's
library. Only an active guardian can create, update, archive, reorder, add, or
remove template content. Scheduling also requires contributor access to the
target calendar, and the selected template must be owned by the assignee, or
by the caller when the task is unassigned.

## Key event types

- `TaskTemplateCreated`
- `TaskTemplateDetailsUpdated`
- `SubtaskAdded`
- `SubtaskUpdated`
- `SubtaskRemoved`
- `SubtasksReordered`
- `TaskTemplateArchived`

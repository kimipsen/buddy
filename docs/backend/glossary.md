# Backend Glossary

This glossary reflects the vocabulary used in the Buddy backend, especially the calendar, task-library, and user features. The definitions below are based on the actual domain types and event streams used by the application.

## Core identities

### UserId
A stable identity for a user, derived from the authenticated Keycloak subject. The users feature uses the subject as the local user identity and stores the user as an event-sourced aggregate.

### CalendarId
The unique identifier for a calendar. A calendar is always owned by a group, can have members, and is stored as an event-sourced aggregate. Calendars created before group-only ownership was introduced can still be owned directly by a user; no new calendar can be.

### CalendarItemId
The unique identifier for an item that belongs to a calendar. A calendar item is either an event or a task.

### TaskTemplateId
The unique identifier for a reusable task template in a child's task library.

### SubtaskId
The unique identifier for a subtask within a task template. It is also used to identify which subtask occurrence is being marked complete after the template is scheduled.

### IcalTokenId
The unique identifier for an iCalendar subscription token issued for a calendar. The token is used to access the calendar feed without requiring a user session.

## User domain

### User
The local user aggregate created from a Keycloak identity. A user has a stable identity, profile data, email state, and optional verification metadata. The aggregate is rebuilt from its event stream when requests arrive.

### KeycloakSubject
The subject claim from Keycloak used as the stable external identity for a local user. This is the value that links a user session to the correct user aggregate.

### Email
The user’s email address and verification state.

Properties:
- Value: the email address itself
- IsVerified: whether the current address has been verified

The project uses a separate verification flow and clears pending verification state whenever the email changes.

### Name
The user’s profile name, consisting of:
- GivenName
- FamilyName

### UserCreated
The event that creates the local user record from the Keycloak claims and the initial profile data.

### UserDeleted
The event that marks the user as deleted. The user aggregate remains in the stream, but it is treated as deleted when rehydrated.

### NameUpdated
The event that changes the user’s name.

### EmailUpdated
The event that changes the user’s email address.

### EmailVerificationRequested
The event that stores a hash of the verification token plus its expiry time. The plaintext token is sent by email and never kept in the event stream.

### EmailVerified
The event that marks the email address as verified and clears the pending verification state.

## Guardian domain

### GuardianLink
The event-sourced relationship record between a guardian and a child, both plain `User`s. There is no separate `Child` type — a `User` is a child solely because a `GuardianLink` points `guardianId -> childId`. Multiple guardians can link to the same child, and one guardian can link to many children.

Properties:
- ChildId / GuardianId: the two `User`s the link connects
- Kind: the `GuardianKind` label
- IsRevoked: whether the relationship is still active

### GuardianKind
A descriptive, record-keeping label on a `GuardianLink`.

Values:
- Parent
- Guardian

Unlike `GroupRole` or `CalendarRole`, `GuardianKind` never gates permission level — a `Parent` and a `Guardian` have the same default authority over the child's account.

### GuardianInvite
An event-sourced invite that brings a second adult into an existing child's guardianship, mirroring the Groups invite/accept/revoke triad. It lives on its own dedicated stream rather than the child's `User` stream, since neither the `User` nor a `GuardianLink` pre-exists the invite. Accepting it appends a new `GuardianLinked` event.

### ChildSummary
A read-model shape returned by guardian-facing child-listing endpoints: the child's `UserId`, name, the `GuardianLinkId` and `GuardianKind` connecting them to the caller, language, and time zone, without requiring a full `User` aggregate rehydration per child.

### GuardianSummary
A read-model shape for a single guardian entry: the guardian's `UserId`, name, and the `GuardianLinkId`/`GuardianKind` connecting them to the child, returned when listing the guardians linked to a child or to the current user.

## Calendar domain

### Calendar
A Calendar represents a named scheduling container with a time zone and a set of members. Each calendar has an owner (always a group) and can also grant roles to other users directly, which override the group's role-derived defaults.

The aggregate stores:
- the calendar name
- its time zone
- member roles
- issued iCalendar tokens
- whether it was deleted

### CalendarRole
A role attached to a user in a calendar.

Values:
- Owner: the creator of the calendar.
- Contributor: can create and modify calendar items.
- Viewer: can read calendar content but cannot change it.

The project treats ownership as fixed and never transfers through member-role events.

### CalendarAccess
The outcome of a permission check for a specific user and calendar operation.

Values:
- Allowed: the user may perform the action.
- NotFound: the calendar is missing, deleted, or the user is not a member.
- Forbidden: the user can view the calendar but is not allowed to do the requested action.

## Calendar items

### CalendarItem
A single scheduled thing inside a calendar. The application distinguishes between two kinds of items:
- Event
- Task

A calendar item has a title, icon, color, and either a period (for events) or a due date (for tasks). It also tracks who created it, who last modified it, and whether it has been deleted.

### CalendarItemKind
The concrete type of a calendar item.

Values:
- Event: an item with a start and end period.
- Task: an item with a due date.

### ItemDetails
A lightweight summary of the editable metadata on a calendar item: title, icon, and color.

### Period
An event-time range defined by a start time and end time. A valid period always has a start that is strictly before its end.

### StartsAt / EndsAt
The start and end timestamps for a period. These values are stored as calendar-local date and time values and are validated before persisting.

### DueDate
The date and time due for a task item.

### RecurrenceRule
Defines how an event or task repeats.

Properties:
- Frequency: Daily, Weekly, Monthly, or Yearly
- IntervalCount: how often the recurrence repeats
- Until: optional end date for the recurrence

### RecurrenceFrequency
The recurrence pattern used by a repeating item.

Values:
- Daily
- Weekly
- Monthly
- Yearly

### Icon / Color
The visual metadata for an item.
- Icon is a string token representing the item’s icon.
- Color is a string token representing the item’s color.

## Event and task concepts

### Event item
A calendar item whose schedule is represented by a `Period`. It can be repeated using a `RecurrenceRule`.

### Task item
A calendar item whose schedule is represented by a `DueDate`. It can also repeat using a `RecurrenceRule`.

A task can be assigned to a calendar member. A task scheduled from a task template also retains the template identifier used to expand the task into its subtasks.

### Template-scheduled task
A calendar task created from a `TaskTemplate`. It has a specific start date and time, can recur, and expands into one occurrence per subtask in template order. Each subtask starts after the durations of the preceding subtasks have elapsed.

The calendar item keeps a reference to the template rather than copying its subtasks. Current template details are therefore used whenever occurrences are expanded. Archiving the template prevents new tasks from being scheduled from it but does not stop existing scheduled tasks from expanding.

### Task completion
Completion state is recorded per occurrence date. A plain task has one completion state for each date; a template-scheduled task has a separate completion state for each subtask and date.

A calendar contributor can change task completion. A viewer can also change completion for a task assigned to them. Future occurrences cannot be marked complete.

### Item deleted
A calendar item can be soft-deleted by appending an `ItemDeleted` event. The aggregate remains in the stream, but the item is marked as deleted when rehydrated.

## Task library domain

### Task library
A child-specific collection of reusable task templates. A child can view their own library, while an active guardian can create, edit, reorder, and archive its templates and subtasks.

### TaskTemplate
An event-sourced reusable routine made up of an ordered list of subtasks. A template has a name, icon, color, creator, last modifier, and archived state. It is indexed under the child it was created for, and that ownership never changes -- there is no sharing across siblings.

### TaskTemplateDetails
The editable top-level metadata of a task template: name, icon, and color.

### Subtask
One ordered step in a task template. A subtask has a title, an optional icon, and a duration. When its icon is omitted, a scheduled occurrence falls back to the calendar item's icon and then to the calendar's icon.

### TotalDuration
The sum of all subtask durations in a task template.

### Archived task template
A task template that remains visible in the task library but cannot be used to schedule a new calendar task. Archiving is a soft-delete operation; existing scheduled tasks continue to use the template.

### TaskLibraryAccessTier
The level of task-library access resolved for a caller.

Values:
- None: the caller has no relationship to the child.
- View: the caller is the child and can view their own task library.
- Manage: the caller is an active guardian and can view and modify the child's task library.

### TaskLibraryAccess
The outcome of a task-library permission check.

Values:
- Allowed: the caller has the required access tier.
- NotFound: the caller has no relationship to the child.
- Forbidden: the caller has some access but not the tier required for the requested action.

## Subscription and feed terms

### IcalToken
A generated subscription token used to access a calendar in iCalendar format. The plaintext token is never kept in the event stream; only a SHA-256 hash is persisted for validation.

### iCalendar feed
The exported calendar feed generated from a calendar and its recurring items. This is used for external calendar clients.

## Event-sourced concepts

### Event stream
An append-only sequence of domain events that represents the current state of an aggregate. The project stores calendar, task-template, and user state by rehydrating from events.

### Aggregate
A domain object rebuilt from its event stream, such as a user, calendar, calendar item, or task template.

### Event
An immutable message describing a state change. The project names events such as `CalendarCreated`, `MemberRoleGranted`, `ItemDetailsUpdated`, `TaskRescheduled`, and `SubtaskAdded`.

### Rehydration
The process of rebuilding the latest aggregate state by replaying all relevant events in order.

## Common terms in the project

### Owner
The group that owns the calendar. A group's Owner (the guardian who created it) resolves to `CalendarRole.Owner` by default through `CalendarPermissionPolicy`. Calendars created before group-only ownership was introduced can still be owned directly by a user, seeded as `CalendarRole.Owner` in `Members`. Ownership can be changed afterward via `TransferCalendarToGroup` -- the one exception to it otherwise being fixed at creation.

### Member
Any user with a role in a calendar, such as owner, contributor, or viewer.

### Deleted calendar / deleted item
A calendar or item is considered deleted when the aggregate is rehydrated from an event that marks it as deleted. Access checks treat deleted records as unavailable.

### Authentication subject
The subject claim from Keycloak used to identify the current user. This is the stable identity that the Buddy app uses for local user creation and lookup.

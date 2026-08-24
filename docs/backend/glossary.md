# Backend Glossary

This glossary reflects the vocabulary used in the Buddy backend, especially the calendar and user features. The definitions below are based on the actual domain types and event streams used by the application.

## Core identities

### UserId
A stable identity for a user, derived from the authenticated Keycloak subject. The users feature uses the subject as the local user identity and stores the user as an event-sourced aggregate.

### CalendarId
The unique identifier for a calendar. A calendar is always owned by a group, can have members, and is stored as an event-sourced aggregate. Calendars created before group-only ownership was introduced can still be owned directly by a user; no new calendar can be.

### CalendarItemId
The unique identifier for an item that belongs to a calendar. A calendar item is either an event or a task.

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

### Item deleted
A calendar item can be soft-deleted by appending an `ItemDeleted` event. The aggregate remains in the stream, but the item is marked as deleted when rehydrated.

## Subscription and feed terms

### IcalToken
A generated subscription token used to access a calendar in iCalendar format. The plaintext token is never kept in the event stream; only a SHA-256 hash is persisted for validation.

### iCalendar feed
The exported calendar feed generated from a calendar and its recurring items. This is used for external calendar clients.

## Event-sourced concepts

### Event stream
An append-only sequence of domain events that represents the current state of an aggregate. The project stores calendar and user state by rehydrating from events.

### Aggregate
A domain object rebuilt from its event stream, such as a user, calendar, or calendar item.

### Event
An immutable message describing a state change. The project names events such as `CalendarCreated`, `MemberRoleGranted`, `ItemDetailsUpdated`, and `TaskRescheduled`.

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

# Medicines Flow

The medicines feature tracks child medication schedules and dose status. It is designed around a daily schedule with multiple times per day, and records the child's actual taken/skipped state for each scheduled dose.

```mermaid
sequenceDiagram
    actor Guardian
    participant App as Client app
    participant API as Buddy API
    participant Medicines as Medicines feature
    participant Store as Medicine event store

    Guardian->>App: Create a medicine schedule for a child
    App->>API: POST /medicines/children/{childId}/schedules
    API->>Medicines: CreateMedicineSchedule command
    Medicines->>Store: Append MedicineScheduleCreated
    Store-->>Medicines: New schedule
    Medicines-->>API: MedicineScheduleResponse
    API-->>App: 200 OK

    Guardian->>App: View child medicine schedules
    App->>API: GET /medicines/children/{childId}/schedules
    API->>Medicines: ListMedicineSchedules query
    Medicines->>Store: Read medicine index + stream data
    Store-->>Medicines: schedules
    Medicines-->>API: list
    API-->>App: 200 OK

    Guardian->>App: Review today's doses
    App->>API: GET /medicines/children/{childId}/doses?from=...&to=...
    API->>Medicines: ListTodaysDoses query
    Medicines->>Store: Expand daily dose occurrences from current state
    Store-->>Medicines: dose occurrences with statuses
    Medicines-->>API: doses
    API-->>App: 200 OK

    Guardian->>App: Mark a dose as taken or skipped
    App->>API: PUT /medicines/children/{childId}/doses/{medicineId}?date=...&time=...
    API->>Medicines: SetDoseStatus command
    Medicines->>Store: Append DoseStatusChanged
    Medicines-->>API: Updated schedule / dose state
    API-->>App: 200 OK
```

## Endpoints

| Method | Route | Behavior |
| --- | --- | --- |
| `POST` | `/medicines/children/{childId}/schedules` | Creates a medicine schedule for the child. |
| `GET` | `/medicines/children/{childId}/schedules` | Lists active or stopped schedules for the child. |
| `PATCH` | `/medicines/children/{childId}/schedules/{medicineId}/details` | Updates medicine details such as name, dosage, icon, or color. |
| `PATCH` | `/medicines/children/{childId}/schedules/{medicineId}/schedule` | Reschedules the windows or time list for a medicine. |
| `DELETE` | `/medicines/children/{childId}/schedules/{medicineId}` | Stops a medicine schedule. |
| `GET` | `/medicines/children/{childId}/doses` | Lists dose occurrences for a `from`/`to` date range. |
| `PUT` | `/medicines/children/{childId}/doses/{medicineId}?date=...&time=...` | Marks a dose as `Taken`, `Skipped`, or `Pending`. |
| `PUT` | `/medicines/children/{childId}/group-share/{groupId}` | Shares the child's medicine schedules with a group. |
| `DELETE` | `/medicines/children/{childId}/group-share/{groupId}` | Unshares the child's medicine schedules from a group. |
| `GET` | `/medicines/children/{childId}/group-share` | Returns the group the child's medicine schedules are currently shared with, if any. |
| `POST` | `/medicines/groups/{groupId}/children/{childId}/schedules` | Creates a medicine schedule for a shared child via group access. |
| `GET` | `/medicines/groups/{groupId}/children/{childId}/schedules` | Lists a shared child's schedules via group access. |
| `PATCH` | `/medicines/groups/{groupId}/children/{childId}/schedules/{medicineId}/details` | Updates schedule details via group access. |
| `PATCH` | `/medicines/groups/{groupId}/children/{childId}/schedules/{medicineId}/schedule` | Reschedules via group access. |
| `DELETE` | `/medicines/groups/{groupId}/children/{childId}/schedules/{medicineId}` | Stops a schedule via group access. |
| `GET` | `/medicines/groups/{groupId}/children/{childId}/doses` | Lists dose occurrences via group access. |
| `PUT` | `/medicines/groups/{groupId}/children/{childId}/doses/{medicineId}` | Sets dose status via group access. |

## Group sharing

A guardian can share a child's medicine schedules with a group, granting members access under that group's `MedicinePermissionPolicy` (`None` or `Manage` — there is no `View` tier) instead of the guardian-child relationship alone. See [Medicine schedules](../analysis/medicine-schedules.md) for the full authorization model and the `MedicineGroupAccess` resolution rules.

## Core lifecycle

A medicine schedule is an event-sourced aggregate with its own stream. The timeline starts with `MedicineScheduleCreated`, then uses events such as `MedicineDetailsUpdated`, `MedicineScheduleRescheduled`, `MedicineScheduleStopped`, and `DoseStatusChanged` to track changes. Unlike a generic calendar item, the medicine model deliberately carries domain state that is specific to dosing workflows.

For listing and display, the backend does not persist a generated occurrence list forever. Instead, it recomputes dose occurrences from the aggregate and the dose log for the requested date range. This keeps the schedule state authoritative while allowing the user-facing dose listing to reflect the current schedule and statuses.

## Authorization model

Medicine access is tightly scoped to the guardian-child relationship. The API expects the caller to be authorized as a guardian or child in the relevant relationship before they can create or modify schedules. This is distinct from the more general calendar membership model and prevents medicine schedule data from being treated as a generic shared calendar item.

## Status model

Each dose can resolve to a status of:

- `Pending`
- `Taken`
- `Skipped`

The underlying event log stores only changes from the default. A dose that is effectively pending is implied by absence from the sparse dose log, while any explicit deviation is captured in `DoseStatusChanged` events.

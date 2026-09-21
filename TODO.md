# TODO

## Event-stream snapshots rollout

Pilot done for `Groups` (`Group`) — see
[docs/backend/analysis/event-stream-snapshots.md](docs/backend/analysis/event-stream-snapshots.md)
for the design and the two real Marten/serialization gotchas hit along the
way. Remaining aggregates need the same recipe: extract a `Fold` step
function, add a `<X>SnapshotProjection` + `<X>Snapshot(Guid Id, X X)`
wrapper, register it in the module's `StoreOptions` pointed at the shared
`snapshots` schema, add `FindSnapshotAsync` to the event store, cut the
read-only query handler over to it.

- [x] Groups — `Group`
- [ ] Calendars — `Calendar`
- [ ] Calendars — `CalendarItem`
- [ ] Guardians — `GuardianLink`
- [ ] Mealplans — `Meal`
- [ ] Mealplans — `MealPlan`
- [ ] Mealplans/AiAssistant — `AiProviderCredential`
- [ ] Mealplans/AiAssistant — `MealplanAiSession`
- [ ] Medicines — `MedicineSchedule`
- [ ] Medicines — `MedicineSharing`
- [ ] Pickups — `PickupSchedule`
- [ ] Progress — `ChildProgress`
- [ ] TaskLibrary — `TaskTemplate`
- [ ] Users — `User`

Not aggregates (event streams with no rehydrated state, so nothing to
snapshot): `GuardianInvite` events drive `GuardianInviteDocument` only, same
as `GroupInvite*` events drive `GroupInviteDocument` off the `Group` stream.

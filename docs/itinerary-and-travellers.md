# Itinerary & travellers (Slice 5)

This document covers the itinerary + collaborative-planning surface added in Slice 5, plus the
reusable traveller-applicability and traveller-filter primitives it introduces for future slices
(bookings, costs, offline, Today, calendar view).

## The `ItineraryItem` aggregate

One aggregate root, one table, one API resource: `MX.TripSideKick.Domain.Itinerary.ItineraryItem`.
"Idea" and "activity" are **states of the same entity**, not separate types:

| State | `Schedule.Status` | Semantics |
| --- | --- | --- |
| Idea | `Unscheduled` | Freeform suggestion; no date, no times. |
| Activity | `Scheduled` | Placed on a `LocalDate`, optionally with `StartTime`/`EndTime`. |

Promotion (idea → activity) is `item.PlaceOnDay(new ItinerarySchedule(...))`. Demotion (activity →
idea) is `item.Unschedule()`. Because the state is carried in an explicit
`ItineraryScheduleStatus` value on a single `ItinerarySchedule` complex property, this exactly
mirrors how `TripDates` models `Undecided`/`Provisional`/`Confirmed`: **no polymorphism, no
inheritance, no duplicated tables, no risk of an item existing in "both" collections at once**.

> Naming note: the aggregate exposes a `Schedule` property, so the method that changes the
> schedule is called `PlaceOnDay(...)`, not `Schedule(...)` — C# forbids the collision. The
> application service and controller both use `PlaceOnDay` internally; the HTTP endpoint is still
> `PUT /schedule` for consistency with the user-facing terminology.

### Confirmed-dates gating

Scheduling is only meaningful once `Trip.Dates.SupportsDayByDayScheduling` is true (i.e. dates
are `Confirmed`). This is enforced in the **application layer** (`ItineraryPlanningService`), not
the domain — an `ItineraryItem` doesn't know its parent `Trip`, and adding that reference would
make the aggregate non-atomic. If the trip's dates are not confirmed, the service throws
`SchedulingNotSupportedException` (a new `DomainRuleViolationException`), which
`ApiExceptionHandler` maps to `409 Conflict` alongside `LastOwnerViolationException`. The service
also validates that the schedule's `Date` falls within
`trip.Dates.StartDate..trip.Dates.EndDate` (throws `ArgumentException` → 400).

## Traveller applicability: "everyone by default = empty list"

Each `ItineraryItem` carries an `IReadOnlyList<TravellerId> ApplicableTravellerIds`. **An empty
list means "applies to everyone"**. A non-empty list restricts the item to the listed travellers.

This encoding is captured in the reusable pure-static class
`MX.TripSideKick.Domain.Travellers.TravellerApplicability`:

```csharp
TravellerApplicability.AppliesToEveryone(ids)     // ids.Count == 0
TravellerApplicability.AppliesTo(ids, travellerId) // empty ⇒ true, else contains
TravellerApplicability.Intersects(ids, otherIds)   // empty ⇒ true (unconditional), else any(id in otherIds)
```

### Explicit reuse seam

Bookings and costs (future slices) will each need their own "who does this apply to?" list. They
should:

1. Store `IReadOnlyList<TravellerId> ApplicableTravellerIds` on their aggregate (mapped as a JSON
   list, exactly like `ItineraryItem`).
2. Use the same **empty = everyone** convention — never introduce a nullable list or a
   `bool AppliesToEveryone` flag.
3. Call `TravellerApplicability.AppliesTo` / `Intersects` directly for filtering.

That keeps the semantics consistent across surfaces and lets a single trip-level traveller filter
(see below) work for any future feature without change.

## Persistent traveller filter

`MX.TripSideKick.Domain.Travellers.TripTravellerFilter` is a per-member, per-trip persisted
preference for who they want to see items for. One row per `(TripId, MembershipId)`, defended by
a unique index (races are handled via `SqlExceptionHelpers`' unique-constraint translation, not
serializable transactions — see `TripTravellerFilterService.GetOrCreateForCallerAsync`).

Three modes:

| Mode | `SelectedTravellerIds` used? | Effective set resolved as |
| --- | --- | --- |
| `Everyone` | ignored | (short-circuits `IsVisible` to `true`) |
| `Me` | ignored | The traveller linked to the caller's own membership, or empty. |
| `Selected` | required | The stored `SelectedTravellerIds`. |

Filtering rule (pure): `TripTravellerFilterEvaluator.IsVisible(mode, effective, itemApplicable)`:

- `Everyone` → `true`
- otherwise → `TravellerApplicability.Intersects(itemApplicable, effective)` — remembering that
  `Intersects` returns `true` when `itemApplicable` is empty (everyone-applicable items are
  **always** visible, regardless of filter).

### Reuse seam

Only the itinerary API consumes `TripTravellerFilterEvaluator.IsVisible` in this slice. Any
future surface — bookings, costs, a Today view, a calendar view — that stores its own
applicable-traveller-id list on each entity can reuse the same
`TripTravellerFilterEvaluator.IsVisible` call against its own list, without any changes here.

## API surface

All endpoints live under `v1/trips/{tripId}/itinerary/`. Role/ETag rules follow existing
Journey 1/2 conventions:

| Endpoint | Method | Min. role | ETag |
| --- | --- | --- | --- |
| `items` | GET | Viewer | — |
| `items` | POST | Editor | — |
| `items/{itemId}` | PUT | Editor | If-Match |
| `items/{itemId}/schedule` | PUT | Editor | If-Match |
| `items/{itemId}/schedule` | DELETE | Editor | If-Match |
| `items/{itemId}/applicability` | PUT | Editor | If-Match |
| `items/{itemId}` | DELETE | Editor | — |
| `items/{itemId}/comments` | GET | Viewer | — |
| `items/{itemId}/comments` | POST | Viewer | — (comments are the one Viewer-permitted mutation) |
| `activity-feed` | GET | Viewer | — |
| `traveller-filter` | GET | any member | ETag on response |
| `traveller-filter` | PUT | any member | If-Match |

The `GET items` endpoint applies the caller's persisted filter server-side before returning the
list. The client re-fetches whenever the filter changes.

## Activity feed

`TripActivityFeedEntry` is append-only (no `RowVersion`, no updates). Every mutation
(`CreateIdea`, `UpdateContent`, `PlaceOnDay`, `Unschedule`, `Delete`, `AddComment`,
`SetApplicability`) appends one entry inside the **same `IUnitOfWork.ExecuteAsync` transaction**
as the underlying write — the feed can never diverge from the data.

The stored `Summary` is trip content (e.g. `"Idea added: Colosseum tour"`), not telemetry. It
must **never** contain email addresses or display names, only item-title content and
subject-derived data. The controller resolves the raw `ActorSubjectId` on the wire to a display
name via `TripSubjectDisplayNameResolver` (which looks up the traveller linked to the actor's
membership on this trip and falls back to `"Trip member"`). The raw subject id is never sent to
the client — same rule as comments' `AuthorDisplayName`.

## Testing

- **Unit tests**: `Domain/Itinerary/ItineraryItemTests.cs` and
  `Domain/Travellers/TripTravellerFilterTests.cs` cover the aggregate, the
  applicability helpers, the filter, and the evaluator.
  `Application/Itinerary/ItineraryPlanningServiceTests.cs` and
  `Application/Travellers/TripTravellerFilterServiceTests.cs` cover orchestration with mocked
  repositories.
- **Integration tests**: `Integration/ItineraryIntegrationTests.cs` runs against a real
  Testcontainers SQL Server via the `SqlServerTestGroup` collection.
- **E2E**: `tests/e2e/tests/journey-itinerary.spec.ts` covers the Owner-Editor-Viewer story
  end-to-end plus server-side proofs for the Viewer 403s on content mutations.

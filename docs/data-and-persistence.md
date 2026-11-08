# Data and persistence

EF Core repositories persist trips, memberships, invitations, travellers,
itinerary items, comments, activity entries and traveller filters in
PostgreSQL. Configuration and schema initialization are described in the
[runtime contract](workload-consolidation.md#fresh-database-and-explicit-initialization);
the EF migrations and entity configurations are the source for the current
schema.

The database generates opaque concurrency values on insert and update. API
mutations that require an ETag use `If-Match`; stale writes are rejected.
Unique constraints are translated into domain conflicts. Cross-row owner
invariants use serializable transactions, with serialization and deadlock
conflicts reported rather than silently retried.

## Domain invariants

* A trip has an owner; its creator also receives a linked traveller. Traveller
  presence and membership are independent.
* Membership is keyed by the identity's stable subject ID, not email. Owners
  manage membership, editors edit trip content, and viewers have read access
  and may add itinerary comments. The last owner cannot leave, be removed or
  be demoted.
* Invitations bind acceptance to the invitee's verified email. An invite can
  link a traveller or grant access to a non-travelling planner.
* Invitation email delivery is not configured. The API returns an acceptance
  link for the caller to share; the placeholder notifier logs neither the
  email nor the token. Treat the link as sensitive.

See [itinerary and travellers](itinerary-and-travellers.md) for scheduling and
filter semantics. Backups must include the database, keyring and document
mounts and be tested through isolated restores; follow the
[runtime contract](workload-consolidation.md) and platform backup procedures.

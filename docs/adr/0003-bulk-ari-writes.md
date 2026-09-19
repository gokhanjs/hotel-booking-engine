# ADR 0003: Bulk ARI writes with MERGE and advisory locks

**Status:** Accepted

## Context

ARI updates arrive as date ranges with optional weekday filters and routinely touch hundreds or thousands of dates. Restriction updates are partial: only the fields present in a request may change. Several updates for the same property can arrive concurrently.

## Decision

- Ranges are expanded in the application layer. When ranges overlap within one request, later values win, field by field for restrictions.
- Writes bypass the EF change tracker and use one SQL statement per request with array parameters and `unnest`:
  - availability: `INSERT ... ON CONFLICT DO UPDATE`;
  - restrictions: `MERGE`, where a `NULL` input keeps the stored value, and `max_stay = 0` clears the limit.
- Each write runs in a transaction that first takes `pg_advisory_xact_lock` keyed by the property id.
- Requests are bounded: at most 20,000 expanded dates, and dates must lie between the property's local today and 730 days ahead.
- Database `CHECK` constraints repeat the domain invariants because this path does not go through entity constructors.

## Consequences

- A 365-day update for several plans is a single round trip.
- Writes for the same property are serialized; writes for different properties run in parallel.
- The integration suite contains a test with ten concurrent writers on the same dates. With the lock removed it failed with `23505 duplicate key` in each of three runs; with the lock it passes.

## Alternatives considered

- **EF Core `AddRange`/change tracking:** simple, but loads or tracks thousands of entities per request.
- **`SERIALIZABLE` isolation with retries:** correct, but pushes retry logic to every caller for a conflict that is predictable and cheap to prevent.
- **Queueing writes per property:** stronger ordering guarantees across API replicas, but adds infrastructure that the current load does not justify.

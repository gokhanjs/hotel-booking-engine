# ADR 0004: Versioned cache invalidation

**Status:** Accepted

## Context

Storefront search and calendar requests are read heavy and expensive (loading ARI for all plans and pricing every combination). Any write to a property (definitions or ARI) must make cached results for that property invisible immediately. The API runs as several replicas.

## Decision

- Redis stores entries under `property:{id}:v{version}:{key}` with a 5 minute TTL.
- Every write increments `property:{id}:version`. Readers resolve the current version first, so entries written under an older version are never read again and expire through their TTL.
- Redis is an optimization, not a dependency: on connection errors the API computes the response from PostgreSQL, logs a warning, and the readiness check reports `Degraded` (HTTP 200) instead of taking pods out of rotation.
- Short client timeouts (`AbortOnConnectFail=false`, 1 s async timeout) keep a Redis outage from turning into slow responses.

## Consequences

- Invalidation is a single `INCR`, independent of how many keys exist for the property.
- A request that computes a result while a write happens stores it under the old version; it is never served, so no stale read occurs.
- If the `INCR` itself fails, entries can be stale for up to the TTL. The failure is logged at error level.

## Alternatives considered

- **Deleting keys by pattern:** requires `SCAN` over the keyspace and races with concurrent readers.
- **`HybridCache` with tags:** in-process L1 plus Redis L2 would cut a network hop, but tag invalidation does not reach the L1 caches of other replicas, which conflicts with the no-stale-price requirement.

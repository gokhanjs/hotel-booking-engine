# ADR 0005: API conventions and error contract

**Status:** Accepted

## Context

The API has two audiences: management clients (revenue managers, channel integrations) and public storefronts. Both need a stable, self-describing contract.

## Decision

- **Minimal APIs** with typed results (`Results<Ok<T>, ProblemHttpResult>`) so the generated OpenAPI document lists the real response types.
- **Resource nesting under the property** (`/properties/{propertyId}/rate-plans/{id}`). A resource requested through another property returns `404`, which prepares for per-property authorization.
- **JSON:** `snake_case` properties and enum values, strict number handling (numbers sent as strings are rejected).
- **Errors:** RFC 9457 problem details with a stable `code` extension.
  - `400`: malformed request, validation errors keyed by snake_case field paths (e.g. `occupancies[0].adults`).
  - `404`: missing resource. `409`: conflict with current state (duplicate code, dependent derived plans).
  - `422`: well-formed request that violates a business rule.
- **Authentication:** management endpoints require `X-Api-Key`, compared in constant time via SHA-256 hashes. Storefront endpoints are anonymous and rate limited per client IP (fixed window, `429`).
- **Documentation:** OpenAPI 3.1 is generated at build time into `docs/openapi/booking-engine.json` and committed. CI fails if a build changes it without the change being committed. Scalar renders it at `/docs`.

## Consequences

- API changes show up as reviewable diffs in the contract file.
- A single shared API key is a portfolio-scope simplification. A multi-tenant deployment would issue keys per account and scope them to properties; the nested routes already carry the property id needed for that check.
- The rate limiter is in-memory per replica, so the effective limit scales with the replica count. A Redis-backed limiter would make it global.

## Alternatives considered

- **Controllers:** familiar, more ceremony, no advantage for this surface.
- **camelCase JSON:** the .NET default, but snake_case matches the conventions of the distribution APIs this service interoperates with.
- **Swagger UI via Swashbuckle:** no longer part of the ASP.NET Core templates; the built-in generator plus Scalar needs fewer dependencies.

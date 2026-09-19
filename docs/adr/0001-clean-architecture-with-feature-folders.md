# ADR 0001: Clean Architecture with feature folders

**Status:** Accepted

## Context

The service has a small but rule-heavy domain (pricing and restrictions), a thin HTTP surface and two storage technologies. The codebase should make the rules easy to find and test, and keep infrastructure concerns from leaking into them.

## Decision

- Four projects: `Domain`, `Application`, `Infrastructure`, `Api`. The dependency rule is enforced by project references, so the domain cannot reference EF Core or ASP.NET Core; a violation fails to compile.
- Inside `Application`, code is grouped by feature (`Properties`, `RoomTypes`, `RatePlans`, `Ari`, `Storefront`) instead of by technical type.
- Use cases are plain service classes. No mediator library: the indirection adds little at this size, and the most popular option moved to a commercial license in 2025.
- Expected failures (not found, conflict, rule violation) are returned as `Result<T>`; invariant violations inside entities throw `DomainException`, which the API maps to `422`.
- `Application` depends on EF Core through `IBookingDbContext` instead of a generic repository layer.

## Consequences

- Entities protect their invariants (private setters, guarded constructors), and the pricing engine is a pure function tested without a database.
- `Application` is coupled to EF Core's `DbSet` and LINQ provider. This is a deliberate trade-off: `DbContext` already implements Unit of Work and Repository, and wrapping it would add code without adding substitutability. Tests use real PostgreSQL instead of in-memory fakes.

## Alternatives considered

- **Vertical slices in a single project:** less ceremony, but the dependency rule becomes a convention instead of a compiler guarantee.
- **Modular monolith:** module boundaries (catalog, inventory, pricing) would be premature for a single bounded context.

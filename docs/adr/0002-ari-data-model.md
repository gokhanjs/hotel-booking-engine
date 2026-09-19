# ADR 0002: ARI data model and restriction semantics

**Status:** Accepted

## Context

Availability, rates and restrictions (ARI) must be representable in the shape that channel managers and OTAs exchange (OpenTravel `OTA_HotelAvailNotifRQ` / `OTA_HotelRateAmountNotifRQ` and equivalent JSON APIs), otherwise every integration needs a lossy translation layer.

## Decision

- **Inventory belongs to the room type, price and restrictions belong to the rate plan.** `room_type_availability(room_type_id, date)` and `rate_plan_restrictions(rate_plan_id, date)` store one row per date.
- **Restriction set:** `stop_sell`, `closed_to_arrival`, `closed_to_departure`, `min_stay_arrival`, `min_stay_through`, `max_stay`.
  - `stop_sell` is checked on every night; `closed_to_arrival` on the arrival date; `closed_to_departure` on the departure date, which is not a night of the stay.
  - `min_stay_arrival` uses the arrival date's value; `min_stay_through` uses the highest value across the stay's nights.
  - `max_stay` is evaluated on the arrival date. Sources disagree on this point; the behaviour is documented in the API and tested.
- **Missing data means closed.** A night without an availability row or a restriction row is not sellable.
- **Money values** (rates, fees, adjustments) accept at most two decimal places and are rejected otherwise, instead of being rounded silently by the `numeric(12,2)` columns.
- **Pricing modes:** `per_room`, or `per_person` with an adjustment per adult count relative to the daily base rate (stored as `jsonb` on the rate plan). Children add a flat per-night fee.
- **Derived rate plans** reference a parent in the same property with a percent or fixed adjustment. The price is computed at read time from the parent's occupancy price; restrictions stay per plan. Chains are rejected. When a plan becomes derived its own stored rates are cleared, so detaching it later leaves it closed until new rates arrive instead of reviving outdated prices.
- `code` and `sell_mode` are immutable after creation because channels map by code and stored rates depend on the mode.

## Consequences

- Rows per property grow with `dates x rate plans`; the composite primary keys serve every read pattern (range scans by plan or room type).
- Computing derived prices at read time avoids fan-out writes when a parent rate changes; search results are cached (ADR 0004).
- `min_stay_through` is kept even though not every channel supports it, because the evaluation is a single `max()` and dropping it would lose data from channels that send it.

## Alternatives considered

- **Date-range rows instead of daily rows:** fewer rows, but every update has to split and merge ranges, and reads need interval logic. Daily rows keep writes and reads simple.
- **Price per occupancy per date:** full OpenTravel fidelity (`BaseByGuestAmts`), but multiplies rows and payloads by the number of occupancies for a rarely used degree of freedom.
- **Cascading restrictions from parent to derived plans:** realistic but adds hidden coupling; out of scope.

using BookingEngine.Application.Abstractions;
using BookingEngine.Application.Ari;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BookingEngine.Infrastructure.Persistence;

internal sealed class AriWriter(BookingDbContext db) : IAriWriter
{
    private const string UpsertAvailabilitySql = """
        INSERT INTO room_type_availability (room_type_id, date, availability)
        SELECT * FROM unnest(@room_type_ids, @dates, @availability)
        ON CONFLICT (room_type_id, date) DO UPDATE SET availability = EXCLUDED.availability
        """;

    private const string MergeRestrictionsSql = """
        MERGE INTO rate_plan_restrictions AS r
        USING (
            SELECT * FROM unnest(@rate_plan_ids, @dates, @rates, @stop_sell, @closed_to_arrival, @closed_to_departure,
                                 @min_stay_arrival, @min_stay_through, @max_stay)
                AS t(rate_plan_id, date, rate, stop_sell, closed_to_arrival, closed_to_departure,
                     min_stay_arrival, min_stay_through, max_stay)
        ) AS i
        ON r.rate_plan_id = i.rate_plan_id AND r.date = i.date
        WHEN MATCHED THEN UPDATE SET
            rate = COALESCE(i.rate, r.rate),
            stop_sell = COALESCE(i.stop_sell, r.stop_sell),
            closed_to_arrival = COALESCE(i.closed_to_arrival, r.closed_to_arrival),
            closed_to_departure = COALESCE(i.closed_to_departure, r.closed_to_departure),
            min_stay_arrival = COALESCE(i.min_stay_arrival, r.min_stay_arrival),
            min_stay_through = COALESCE(i.min_stay_through, r.min_stay_through),
            max_stay = CASE WHEN i.max_stay IS NULL THEN r.max_stay ELSE NULLIF(i.max_stay, 0) END
        WHEN NOT MATCHED THEN INSERT
            (rate_plan_id, date, rate, stop_sell, closed_to_arrival, closed_to_departure, min_stay_arrival, min_stay_through, max_stay)
        VALUES (
            i.rate_plan_id, i.date, i.rate,
            COALESCE(i.stop_sell, false), COALESCE(i.closed_to_arrival, false), COALESCE(i.closed_to_departure, false),
            COALESCE(i.min_stay_arrival, 1), COALESCE(i.min_stay_through, 1), NULLIF(i.max_stay, 0))
        """;

    public Task UpsertAvailabilityAsync(Guid propertyId, IReadOnlyList<AvailabilityChange> changes, CancellationToken ct) =>
        ExecuteLockedAsync(propertyId, UpsertAvailabilitySql, ct,
        [
            Array("room_type_ids", NpgsqlDbType.Uuid, changes.Select(c => c.RoomTypeId).ToArray()),
            Array("dates", NpgsqlDbType.Date, changes.Select(c => c.Date).ToArray()),
            Array("availability", NpgsqlDbType.Integer, changes.Select(c => c.Availability).ToArray()),
        ]);

    public Task UpsertRestrictionsAsync(Guid propertyId, IReadOnlyList<RestrictionChange> changes, CancellationToken ct) =>
        ExecuteLockedAsync(propertyId, MergeRestrictionsSql, ct,
        [
            Array("rate_plan_ids", NpgsqlDbType.Uuid, changes.Select(c => c.RatePlanId).ToArray()),
            Array("dates", NpgsqlDbType.Date, changes.Select(c => c.Date).ToArray()),
            Array("rates", NpgsqlDbType.Numeric, changes.Select(c => c.Rate).ToArray()),
            Array("stop_sell", NpgsqlDbType.Boolean, changes.Select(c => c.StopSell).ToArray()),
            Array("closed_to_arrival", NpgsqlDbType.Boolean, changes.Select(c => c.ClosedToArrival).ToArray()),
            Array("closed_to_departure", NpgsqlDbType.Boolean, changes.Select(c => c.ClosedToDeparture).ToArray()),
            Array("min_stay_arrival", NpgsqlDbType.Integer, changes.Select(c => c.MinStayArrival).ToArray()),
            Array("min_stay_through", NpgsqlDbType.Integer, changes.Select(c => c.MinStayThrough).ToArray()),
            Array("max_stay", NpgsqlDbType.Integer, changes.Select(c => c.MaxStay).ToArray()),
        ]);

    // Serializes concurrent bulk writes per property so MERGE never races on inserting the same (plan, date) row.
    private async Task ExecuteLockedAsync(Guid propertyId, string sql, CancellationToken ct, NpgsqlParameter[] parameters)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({propertyId.ToString()}, 0))", ct);
        await db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
        await transaction.CommitAsync(ct);
    }

    private static NpgsqlParameter Array<T>(string name, NpgsqlDbType type, T[] values) =>
        new(name, NpgsqlDbType.Array | type) { Value = values };
}

using BookingEngine.Application.Abstractions;
using BookingEngine.Application.Common;
using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;

namespace BookingEngine.Application.RatePlans;

public sealed class RatePlanService(IBookingDbContext db, IPropertyCache cache)
{
    public async Task<Result<RatePlanResponse>> CreateAsync(Guid propertyId, CreateRatePlanRequest request, CancellationToken ct)
    {
        var roomType = await db.RoomTypes.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.RoomTypeId && r.PropertyId == propertyId, ct);
        if (roomType is null)
        {
            return Error.NotFound("room_type");
        }

        var ratePlan = new RatePlan(roomType.Id, request.Code, request.Name, request.SellMode, request.MealPlan, request.ChildFee);
        if (await db.RatePlans.AnyAsync(r => r.RoomTypeId == roomType.Id && r.Code == ratePlan.Code, ct))
        {
            return Error.Conflict("rate_plan.code_taken", $"Rate plan code '{ratePlan.Code}' is already used for this room type.");
        }

        var applied = await ApplyPricingAsync(propertyId, ratePlan, roomType, request.Occupancies, request.Derived, ct);
        if (!applied.IsSuccess)
        {
            return applied.Error;
        }

        db.RatePlans.Add(ratePlan);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(propertyId, ct);

        return RatePlanResponse.From(ratePlan);
    }

    public async Task<Result<IReadOnlyList<RatePlanResponse>>> ListAsync(Guid propertyId, CancellationToken ct)
    {
        if (!await db.Properties.AnyAsync(p => p.Id == propertyId, ct))
        {
            return Error.NotFound("property");
        }

        var ratePlans = await InProperty(propertyId).AsNoTracking().OrderBy(r => r.Code).ToListAsync(ct);

        return ratePlans.Select(RatePlanResponse.From).ToList();
    }

    public async Task<Result<RatePlanResponse>> GetAsync(Guid propertyId, Guid id, CancellationToken ct)
    {
        var ratePlan = await InProperty(propertyId).AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);

        return ratePlan is null ? Error.NotFound("rate_plan") : RatePlanResponse.From(ratePlan);
    }

    public async Task<Result<RatePlanResponse>> UpdateAsync(Guid propertyId, Guid id, UpdateRatePlanRequest request, CancellationToken ct)
    {
        var ratePlan = await InProperty(propertyId).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (ratePlan is null)
        {
            return Error.NotFound("rate_plan");
        }

        if (request.Derived is not null && await db.RatePlans.AnyAsync(r => r.ParentRatePlanId == id, ct))
        {
            return Error.Conflict(
                "rate_plan.has_derived_dependents",
                "A rate plan that other plans derive from cannot become derived itself.");
        }

        var roomType = await db.RoomTypes.AsNoTracking().SingleAsync(r => r.Id == ratePlan.RoomTypeId, ct);
        var wasDerived = ratePlan.IsDerived;
        ratePlan.Update(request.Name, request.MealPlan, request.ChildFee);

        var applied = await ApplyPricingAsync(propertyId, ratePlan, roomType, request.Occupancies, request.Derived, ct);
        if (!applied.IsSuccess)
        {
            return applied.Error;
        }

        // Rates of a plan that becomes derived must not come back if it is detached later; it then stays closed until new rates arrive.
        if (!wasDerived && ratePlan.IsDerived)
        {
            await db.Restrictions.Where(r => r.RatePlanId == id).ExecuteUpdateAsync(s => s.SetProperty(r => r.Rate, (decimal?)null), ct);
        }

        await db.SaveChangesAsync(ct);
        await cache.InvalidateAsync(propertyId, ct);

        return RatePlanResponse.From(ratePlan);
    }

    public async Task<Result> DeleteAsync(Guid propertyId, Guid id, CancellationToken ct)
    {
        if (await db.RatePlans.AnyAsync(r => r.ParentRatePlanId == id, ct))
        {
            return Error.Conflict("rate_plan.has_derived_dependents", "Other rate plans derive from this rate plan; delete them first.");
        }

        var deleted = await InProperty(propertyId).Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        await cache.InvalidateAsync(propertyId, ct);

        return deleted == 0 ? Error.NotFound("rate_plan") : Result.Success();
    }

    private IQueryable<RatePlan> InProperty(Guid propertyId) =>
        db.RatePlans.Where(rp => db.RoomTypes.Any(rt => rt.Id == rp.RoomTypeId && rt.PropertyId == propertyId));

    private async Task<Result> ApplyPricingAsync(
        Guid propertyId,
        RatePlan ratePlan,
        RoomType roomType,
        IReadOnlyList<OccupancyDto>? occupancies,
        DerivedPricingDto? derived,
        CancellationToken ct)
    {
        if (derived is null)
        {
            ratePlan.ClearDerivation();
        }
        else
        {
            var parent = await InProperty(propertyId).AsNoTracking().SingleOrDefaultAsync(r => r.Id == derived.ParentRatePlanId, ct);
            if (parent is null)
            {
                return Error.Unprocessable("rate_plan.parent_not_found", "Parent rate plan was not found in this property.");
            }

            ratePlan.DeriveFrom(parent, derived.Type, derived.Value);
        }

        if (ratePlan.SellMode == SellMode.PerPerson && !ratePlan.IsDerived)
        {
            if (occupancies is not { Count: > 0 })
            {
                return Error.Unprocessable("rate_plan.occupancies_required", "Per-person rate plans need occupancy pricing.");
            }

            ratePlan.SetOccupancies(occupancies.Select(o => new RatePlanOccupancy(o.Adults, o.PriceAdjustment)), roomType.MaxAdults);
        }
        else if (occupancies is { Count: > 0 })
        {
            return Error.Unprocessable(
                "rate_plan.occupancies_not_allowed",
                "Occupancy pricing applies only to non-derived per-person rate plans.");
        }

        return Result.Success();
    }
}

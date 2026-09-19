using BookingEngine.Domain.Common;

namespace BookingEngine.Domain.RatePlans;

public sealed class RatePlan : Entity
{
    private readonly List<RatePlanOccupancy> _occupancies = [];

    public Guid RoomTypeId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public SellMode SellMode { get; private set; }
    public MealPlan MealPlan { get; private set; }
    public decimal ChildFee { get; private set; }
    public Guid? ParentRatePlanId { get; private set; }
    public DerivedAdjustmentType? DerivedType { get; private set; }
    public decimal? DerivedValue { get; private set; }
    public IReadOnlyList<RatePlanOccupancy> Occupancies => _occupancies;

    public DerivedPricing? Derived =>
        ParentRatePlanId is { } parentId ? new DerivedPricing(parentId, DerivedType!.Value, DerivedValue!.Value) : null;

    public bool IsDerived => ParentRatePlanId is not null;

    private RatePlan() { }

    public RatePlan(Guid roomTypeId, string code, string name, SellMode sellMode, MealPlan mealPlan, decimal childFee)
    {
        RoomTypeId = roomTypeId;
        Code = Guard.NotBlank(code, "Code", 50).ToUpperInvariant();
        SellMode = sellMode;
        Update(name, mealPlan, childFee);
    }

    public void Update(string name, MealPlan mealPlan, decimal childFee)
    {
        Name = Guard.NotBlank(name, "Name", 200);
        MealPlan = mealPlan;
        ChildFee = Guard.NotNegative(childFee, "Child fee");
    }

    /// <summary>Per-adult price adjustments relative to the daily base rate; per-person plans only.</summary>
    public void SetOccupancies(IEnumerable<RatePlanOccupancy> occupancies, int maxAdults)
    {
        if (SellMode != SellMode.PerPerson)
        {
            throw new DomainException("Occupancy pricing applies to per-person rate plans only.");
        }

        if (IsDerived)
        {
            throw new DomainException("Derived rate plans take occupancy pricing from their parent.");
        }

        var list = occupancies.OrderBy(o => o.Adults).ToList();
        if (list.Count == 0 || list.Any(o => o.Adults < 1 || o.Adults > maxAdults))
        {
            throw new DomainException($"Occupancies must cover 1 to {maxAdults} adults.");
        }

        if (list.Select(o => o.Adults).Distinct().Count() != list.Count)
        {
            throw new DomainException("Each adult count may appear only once.");
        }

        foreach (var occupancy in list)
        {
            Guard.TwoDecimals(occupancy.PriceAdjustment, "Price adjustment");
        }

        _occupancies.Clear();
        _occupancies.AddRange(list);
    }

    public void DeriveFrom(RatePlan parent, DerivedAdjustmentType type, decimal value)
    {
        if (parent.Id == Id)
        {
            throw new DomainException("A rate plan cannot derive from itself.");
        }

        if (parent.IsDerived)
        {
            throw new DomainException("A rate plan cannot derive from another derived rate plan.");
        }

        if (parent.SellMode != SellMode)
        {
            throw new DomainException("A derived rate plan must use the same sell mode as its parent.");
        }

        var derived = new DerivedPricing(parent.Id, type, value);
        _occupancies.Clear();
        ParentRatePlanId = derived.ParentRatePlanId;
        DerivedType = derived.Type;
        DerivedValue = derived.Value;
    }

    public void ClearDerivation()
    {
        ParentRatePlanId = null;
        DerivedType = null;
        DerivedValue = null;
    }
}

using BookingEngine.Domain.RatePlans;
using BookingEngine.Domain.RoomTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingEngine.Infrastructure.Persistence.Configurations;

internal sealed class RatePlanConfiguration : IEntityTypeConfiguration<RatePlan>
{
    public void Configure(EntityTypeBuilder<RatePlan> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_rate_plans_child_fee", "child_fee >= 0");
            t.HasCheckConstraint(
                "ck_rate_plans_derived_complete",
                "(parent_rate_plan_id IS NULL) = (derived_type IS NULL) AND (derived_type IS NULL) = (derived_value IS NULL)");
        });

        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Code).HasMaxLength(50);
        builder.Property(r => r.Name).HasMaxLength(200);
        builder.Property(r => r.SellMode).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.MealPlan).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.DerivedType).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ChildFee).HasPrecision(12, 2);
        builder.Property(r => r.DerivedValue).HasPrecision(12, 2);
        builder.Ignore(r => r.Derived);

        builder.Ignore(r => r.Occupancies);
        builder.ComplexCollection<List<RatePlanOccupancy>, RatePlanOccupancy>("_occupancies", o => o.ToJson("occupancies"));

        builder.HasOne<RoomType>().WithMany().HasForeignKey(r => r.RoomTypeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RatePlan>().WithMany().HasForeignKey(r => r.ParentRatePlanId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(r => new { r.RoomTypeId, r.Code }).IsUnique();
    }
}

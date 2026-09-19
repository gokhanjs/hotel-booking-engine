using BookingEngine.Domain.Inventory;
using BookingEngine.Domain.RatePlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BookingEngine.Infrastructure.Persistence.Configurations;

internal sealed class RatePlanRestrictionConfiguration : IEntityTypeConfiguration<RatePlanRestriction>
{
    public void Configure(EntityTypeBuilder<RatePlanRestriction> builder)
    {
        builder.ToTable("rate_plan_restrictions", t =>
        {
            t.HasCheckConstraint("ck_rate_plan_restrictions_rate", "rate IS NULL OR rate >= 0");
            t.HasCheckConstraint("ck_rate_plan_restrictions_min_stay_arrival", "min_stay_arrival >= 1");
            t.HasCheckConstraint("ck_rate_plan_restrictions_min_stay_through", "min_stay_through >= 1");
            t.HasCheckConstraint("ck_rate_plan_restrictions_max_stay", "max_stay IS NULL OR max_stay >= 1");
        });

        builder.HasKey(r => new { r.RatePlanId, r.Date });
        builder.Property(r => r.Rate).HasPrecision(12, 2);
        builder.HasOne<RatePlan>().WithMany().HasForeignKey(r => r.RatePlanId).OnDelete(DeleteBehavior.Cascade);
    }
}

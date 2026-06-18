using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class RecentlyViewedProductConfiguration : IEntityTypeConfiguration<RecentlyViewedProduct>
{
    public void Configure(EntityTypeBuilder<RecentlyViewedProduct> builder)
    {
        builder.ToTable("RecentlyViewedProduct");
    }
}

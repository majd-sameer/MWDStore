using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductBackInStockSubscriptionConfiguration : IEntityTypeConfiguration<ProductBackInStockSubscription>
{
    public void Configure(EntityTypeBuilder<ProductBackInStockSubscription> builder)
    {
        builder.ToTable("ProductBackInStockSubscription");
    }
}

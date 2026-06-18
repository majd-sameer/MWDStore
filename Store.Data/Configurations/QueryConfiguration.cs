using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class QueryConfiguration : IEntityTypeConfiguration<Query>
{
    public void Configure(EntityTypeBuilder<Query> builder)
    {
        builder.ToTable("Query");

        builder.Property(e => e.QueryText).HasMaxLength(500);
    }
}

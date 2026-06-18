using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ContactAreaConfiguration : IEntityTypeConfiguration<ContactArea>
{
    public void Configure(EntityTypeBuilder<ContactArea> builder)
    {
        builder.ToTable("ContactArea");

        builder.Property(e => e.Name).HasMaxLength(450);
    }
}

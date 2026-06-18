using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ContactConfiguration : IEntityTypeConfiguration<Contact>
{
    public void Configure(EntityTypeBuilder<Contact> builder)
    {
        builder.ToTable("Contact");

        builder.HasIndex(e => e.ContactAreaId, "IX_Contacts_Contact_ContactAreaId");

        builder.Property(e => e.Address).HasMaxLength(450);
        builder.Property(e => e.EmailAddress).HasMaxLength(450);
        builder.Property(e => e.FullName).HasMaxLength(450);
        builder.Property(e => e.PhoneNumber).HasMaxLength(450);

        builder.HasOne(d => d.ContactArea).WithMany(p => p.Contacts)
            .HasForeignKey(d => d.ContactAreaId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}

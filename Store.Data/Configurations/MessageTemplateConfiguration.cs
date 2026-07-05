using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class MessageTemplateConfiguration : IEntityTypeConfiguration<MessageTemplate>
{
    public void Configure(EntityTypeBuilder<MessageTemplate> builder)
    {
        builder.ToTable("MessageTemplate");

        builder.HasIndex(e => e.Name, "IX_Messaging_MessageTemplate_Name").IsUnique();

        builder.Property(e => e.Name).HasMaxLength(255).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(1000).IsRequired();
        builder.Property(e => e.BccEmailAddresses).HasMaxLength(450);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class QueuedEmailConfiguration : IEntityTypeConfiguration<QueuedEmail>
{
    public void Configure(EntityTypeBuilder<QueuedEmail> builder)
    {
        builder.ToTable("QueuedEmail");

        // Drained by ProcessQueueAsync, which filters on the "still pending" predicate; index it.
        builder.HasIndex(e => e.SentOn, "IX_Messaging_QueuedEmail_SentOn");

        builder.Property(e => e.To).HasMaxLength(255).IsRequired();
        builder.Property(e => e.ToName).HasMaxLength(255);
        builder.Property(e => e.Bcc).HasMaxLength(450);
        builder.Property(e => e.Subject).HasMaxLength(1000).IsRequired();

        builder.HasOne(d => d.EmailAccount).WithMany(p => p.QueuedEmails)
            .HasForeignKey(d => d.EmailAccountId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}

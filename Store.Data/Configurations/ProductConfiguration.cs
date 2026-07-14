using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Product");

        builder.HasIndex(e => e.BrandId, "IX_Catalog_Product_BrandId");

        builder.HasIndex(e => e.CreatedById, "IX_Catalog_Product_CreatedById");

        builder.HasIndex(e => e.LatestUpdatedById, "IX_Catalog_Product_LatestUpdatedById");

        builder.HasIndex(e => e.TaxClassId, "IX_Catalog_Product_TaxClassId");

        builder.HasIndex(e => e.ThumbnailImageId, "IX_Catalog_Product_ThumbnailImageId");

        // Curated signature products only — filtered so the index stays tiny.
        builder.HasIndex(e => new { e.IsSignature, e.SignatureSortOrder }, "IX_Catalog_Product_Signature")
            .HasFilter("[IsSignature] = 1");

        builder.ConfigureSeoColumns();
        builder.Property(e => e.Gtin).HasMaxLength(450);
        builder.Property(e => e.NormalizedName).HasMaxLength(450);
        builder.Property(e => e.OldPrice).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        builder.Property(e => e.ShortDescription).HasMaxLength(450);
        builder.Property(e => e.Sku).HasMaxLength(450);
        builder.Property(e => e.SpecialPrice).HasColumnType("decimal(18, 2)");

        builder.HasOne(d => d.Brand).WithMany(p => p.Products).HasForeignKey(d => d.BrandId);

        builder.HasOne(d => d.CreatedBy).WithMany(p => p.ProductCreatedBies)
            .HasForeignKey(d => d.CreatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.LatestUpdatedBy).WithMany(p => p.ProductLatestUpdatedBies)
            .HasForeignKey(d => d.LatestUpdatedById)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(d => d.TaxClass).WithMany(p => p.Products).HasForeignKey(d => d.TaxClassId);

        builder.HasOne(d => d.ThumbnailImage).WithMany(p => p.Products).HasForeignKey(d => d.ThumbnailImageId);
    }
}

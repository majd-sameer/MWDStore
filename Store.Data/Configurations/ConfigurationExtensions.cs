using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

/// <summary>
/// Column blocks that several tables define identically. Properties are addressed by name so one
/// method can serve entities that share columns without sharing a CLR base class (a base class
/// would change the EF model; these helpers cannot). Model building throws at startup if a caller's
/// entity lacks one of the columns.
/// </summary>
internal static class ConfigurationExtensions
{
    /// <summary>The slug/SEO column lengths shared by <see cref="ISeoEntity"/> tables.</summary>
    public static void ConfigureSeoColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ISeoEntity
    {
        builder.Property(nameof(ISeoEntity.MetaKeywords)).HasMaxLength(450);
        builder.Property(nameof(ISeoEntity.MetaTitle)).HasMaxLength(450);
        builder.Property(nameof(ISeoEntity.Name)).HasMaxLength(450);
        builder.Property(nameof(ISeoEntity.Slug)).HasMaxLength(450);
    }

    /// <summary>
    /// The address column lengths kept in lockstep between <see cref="Address"/> and its
    /// order-time snapshot <see cref="OrderAddress"/> (separate tables by design).
    /// </summary>
    public static void ConfigureAddressColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property(nameof(Address.AddressLine1)).HasMaxLength(450);
        builder.Property(nameof(Address.AddressLine2)).HasMaxLength(450);
        builder.Property(nameof(Address.City)).HasMaxLength(450);
        builder.Property(nameof(Address.ContactName)).HasMaxLength(450);
        builder.Property(nameof(Address.Phone)).HasMaxLength(450);
        builder.Property(nameof(Address.ZipCode)).HasMaxLength(450);
    }

    /// <summary>The rule-header columns shared by <see cref="CartRule"/> and <see cref="CatalogRule"/>.</summary>
    public static void ConfigureRuleColumns<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class
    {
        builder.Property(nameof(CartRule.DiscountAmount)).HasColumnType("decimal(18, 2)");
        builder.Property(nameof(CartRule.MaxDiscountAmount)).HasColumnType("decimal(18, 2)");
        builder.Property(nameof(CartRule.Name)).HasMaxLength(450);
        builder.Property(nameof(CartRule.RuleToApply)).HasMaxLength(450);
    }
}

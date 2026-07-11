using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Store.Domain;

namespace Store.Data.Configurations;

internal static class LocalizedStringConfiguration
{
    /// <summary>Maps a LocalizedString as an owned type on the owner's table: Ar keeps the original
    /// base column name (zero data movement), En gets "&lt;baseColumn&gt;En". Explicit HasColumnName is
    /// mandatory — the owned-type default would be "Name_Ar"/"Name_En" and would RENAME live columns.</summary>
    public static void OwnsLocalized<T>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, LocalizedString?>> property,
        string baseColumn, int? maxLength = null, bool required = false)
        where T : class
    {
        builder.OwnsOne(property, ls =>
        {
            var ar = ls.Property(x => x.Ar).HasColumnName(baseColumn);
            var en = ls.Property(x => x.En).HasColumnName(baseColumn + "En");
            if (maxLength is int len) { ar.HasMaxLength(len); en.HasMaxLength(len); }
            if (required) ar.IsRequired();
        });
        if (required) builder.Navigation(property!).IsRequired();
    }
}

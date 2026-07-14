namespace Store.Domain;

/// <summary>Content addressable by slug with the standard SEO meta columns.</summary>
public interface ISeoEntity
{
    string Name { get; set; }

    string Slug { get; set; }

    string? MetaTitle { get; set; }

    string? MetaKeywords { get; set; }

    string? MetaDescription { get; set; }
}

using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Medium
{
    public long Id { get; set; }

    public string? Caption { get; set; }

    public int FileSize { get; set; }

    public string? FileName { get; set; }

    public int MediaType { get; set; }

    public ICollection<Category> Categories { get; set; } = [];

    public ICollection<ProductMedium> ProductMedia { get; set; } = [];

    public ICollection<Product> Products { get; set; } = [];

    public ICollection<NewsItem> NewsItems { get; set; } = [];
}


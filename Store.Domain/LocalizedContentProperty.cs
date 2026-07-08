using System;
using System.Collections.Generic;

namespace Store.Domain;

public class LocalizedContentProperty
{
    public long Id { get; set; }

    public long EntityId { get; set; }

    /// <summary>
    /// String key for entities whose primary key is not a <see cref="long"/> (e.g. <c>Country</c>,
    /// keyed by ISO code). When set, this — not <see cref="EntityId"/> — identifies the row; numeric
    /// entities leave it null and continue to use <see cref="EntityId"/>.
    /// </summary>
    public string? EntityKey { get; set; }

    public string? EntityType { get; set; }

    public string CultureId { get; set; } = null!;

    public string ProperyName { get; set; } = null!;

    public string? Value { get; set; }

    public Culture Culture { get; set; } = null!;
}


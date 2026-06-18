using System;
using System.Collections.Generic;

namespace Store.Domain;

public class PaymentProvider
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public string? ConfigureUrl { get; set; }

    public string? LandingViewComponentName { get; set; }

    public string? AdditionalSettings { get; set; }
}


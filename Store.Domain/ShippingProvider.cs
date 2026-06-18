using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ShippingProvider
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public string? ConfigureUrl { get; set; }

    public bool ToAllShippingEnabledCountries { get; set; }

    public string? OnlyCountryIdsString { get; set; }

    public bool ToAllShippingEnabledStatesOrProvinces { get; set; }

    public string? OnlyStateOrProvinceIdsString { get; set; }

    public string? AdditionalSettings { get; set; }

    public string? ShippingPriceServiceTypeName { get; set; }
}


using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Country
{
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Code3 { get; set; }

    public bool IsBillingEnabled { get; set; }

    public bool IsShippingEnabled { get; set; }

    public bool IsCityEnabled { get; set; }

    public bool IsZipCodeEnabled { get; set; }

    public bool IsDistrictEnabled { get; set; }

    public ICollection<Address> Addresses { get; set; } = [];

    public ICollection<StateOrProvince> StateOrProvinces { get; set; } = [];

    public ICollection<OrderAddress> OrderAddresses { get; set; } = [];

    public ICollection<PriceAndDestination> PriceAndDestinations { get; set; } = [];

    public ICollection<TaxRate> TaxRates { get; set; } = [];
}


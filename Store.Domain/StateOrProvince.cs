using System;
using System.Collections.Generic;

namespace Store.Domain;

public class StateOrProvince
{
    public long Id { get; set; }

    public string? CountryId { get; set; }

    public string? Code { get; set; }

    public string Name { get; set; } = null!;

    public string? Type { get; set; }

    public ICollection<Address> Addresses { get; set; } = [];

    public ICollection<District> Districts { get; set; } = [];

    public Country? Country { get; set; }

    public ICollection<OrderAddress> OrderAddresses { get; set; } = [];

    public ICollection<PriceAndDestination> PriceAndDestinations { get; set; } = [];

    public ICollection<TaxRate> TaxRates { get; set; } = [];
}


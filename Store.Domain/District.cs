using System;
using System.Collections.Generic;

namespace Store.Domain;

public class District
{
    public long Id { get; set; }

    public long StateOrProvinceId { get; set; }

    public string Name { get; set; } = null!;

    public string? Type { get; set; }

    public string? Location { get; set; }

    public ICollection<Address> Addresses { get; set; } = [];

    public ICollection<OrderAddress> OrderAddresses { get; set; } = [];

    public ICollection<PriceAndDestination> PriceAndDestinations { get; set; } = [];

    public StateOrProvince StateOrProvince { get; set; } = null!;
}


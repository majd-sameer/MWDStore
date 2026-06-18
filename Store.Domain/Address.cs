using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Address
{
    public long Id { get; set; }

    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public long? DistrictId { get; set; }

    public long StateOrProvinceId { get; set; }

    public string CountryId { get; set; } = null!;

    public ICollection<UserAddress> UserAddresses { get; set; } = [];

    public Country Country { get; set; } = null!;

    public District? District { get; set; }

    public ICollection<Warehouse> Warehouses { get; set; } = [];

    public StateOrProvince StateOrProvince { get; set; } = null!;
}


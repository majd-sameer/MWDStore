using System;
using System.Collections.Generic;

namespace Store.Domain;

public class PriceAndDestination
{
    public long Id { get; set; }

    public string? CountryId { get; set; }

    public long? StateOrProvinceId { get; set; }

    public long? DistrictId { get; set; }

    public string? ZipCode { get; set; }

    public string? Note { get; set; }

    public decimal MinOrderSubtotal { get; set; }

    public decimal ShippingPrice { get; set; }

    /// <summary>The shipping provider (carrier) this rate belongs to, e.g. "Aramex" / "JordanPost".</summary>
    public string? ShippingProviderId { get; set; }

    public Country? Country { get; set; }

    public ShippingProvider? ShippingProvider { get; set; }

    public District? District { get; set; }

    public StateOrProvince? StateOrProvince { get; set; }
}


using System;
using System.Collections.Generic;

namespace Store.Domain;

public class TaxRate
{
    public long Id { get; set; }

    public long TaxClassId { get; set; }

    public string? CountryId { get; set; }

    public long? StateOrProvinceId { get; set; }

    public decimal Rate { get; set; }

    public string? ZipCode { get; set; }

    public Country? Country { get; set; }

    public StateOrProvince? StateOrProvince { get; set; }

    public TaxClass TaxClass { get; set; } = null!;
}


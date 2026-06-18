using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Warehouse
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public long AddressId { get; set; }

    public long? VendorId { get; set; }

    public Address Address { get; set; } = null!;

    public ICollection<StockHistory> StockHistories { get; set; } = [];

    public ICollection<Stock> Stocks { get; set; } = [];

    public ICollection<Shipment> Shipments { get; set; } = [];

    public Vendor? Vendor { get; set; }
}


using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Shipment
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public string? TrackingNumber { get; set; }

    public long WarehouseId { get; set; }

    public long? VendorId { get; set; }

    public long CreatedById { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public User CreatedBy { get; set; } = null!;

    public Order Order { get; set; } = null!;

    public ICollection<ShipmentItem> ShipmentItems { get; set; } = [];

    public Warehouse Warehouse { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace Store.Domain;

public class ShipmentItem
{
    public long Id { get; set; }

    public long ShipmentId { get; set; }

    public long OrderItemId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public Product Product { get; set; } = null!;

    public Shipment Shipment { get; set; } = null!;
}


using System;
using System.Collections.Generic;

namespace Store.Domain;

public class Order
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    /// <summary>
    /// Public, random 6-digit code customers use to track the order (independent of the sequential
    /// <see cref="Id"/>). Set on the master order only; sub-orders leave it null.
    /// </summary>
    public string? TrackingNumber { get; set; }

    /// <summary>
    /// Contact email for a guest (no-account) order, used as the shared secret on the public track
    /// lookup. Null for signed-in orders, which match on the customer's account email instead.
    /// </summary>
    public string? GuestEmail { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public long LatestUpdatedById { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long CreatedById { get; set; }

    public long? VendorId { get; set; }

    public string? CouponCode { get; set; }

    public string? CouponRuleName { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal SubTotal { get; set; }

    public decimal SubTotalWithDiscount { get; set; }

    public long ShippingAddressId { get; set; }

    public long BillingAddressId { get; set; }

    public int OrderStatus { get; set; }

    public string? OrderNote { get; set; }

    public long? ParentId { get; set; }

    public bool IsMasterOrder { get; set; }

    public string? ShippingMethod { get; set; }

    public decimal ShippingFeeAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal OrderTotal { get; set; }

    public string? PaymentMethod { get; set; }

    public decimal PaymentFeeAmount { get; set; }

    public OrderAddress BillingAddress { get; set; } = null!;

    public User CreatedBy { get; set; } = null!;

    public User Customer { get; set; } = null!;

    public ICollection<Order> InverseParent { get; set; } = [];

    public User LatestUpdatedBy { get; set; } = null!;

    public ICollection<OrderHistory> OrderHistories { get; set; } = [];

    public ICollection<OrderItem> OrderItems { get; set; } = [];

    public Order? Parent { get; set; }

    public ICollection<Payment> Payments { get; set; } = [];

    public ICollection<Shipment> Shipments { get; set; } = [];

    public OrderAddress ShippingAddress { get; set; } = null!;
}


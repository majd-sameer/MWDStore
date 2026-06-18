using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;

namespace Store.Domain;

/// <summary>
/// Application user. Derives from <see cref="IdentityUser{TKey}"/> (matching SimplCommerce's
/// <c>User : IdentityUser&lt;long&gt;</c>); the Identity base supplies UserName, Email, PasswordHash,
/// SecurityStamp, lockout/2FA, etc. Only the SimplCommerce-specific members and navigations live here.
/// </summary>
public class User : IdentityUser<long>
{
    public Guid UserGuid { get; set; }

    public string FullName { get; set; } = null!;

    public long? VendorId { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public DateTimeOffset LatestUpdatedOn { get; set; }

    public long? DefaultShippingAddressId { get; set; }

    public long? DefaultBillingAddressId { get; set; }

    public string? RefreshTokenHash { get; set; }

    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }

    public string? Culture { get; set; }

    public string? ExtensionData { get; set; }

    public ICollection<Product> ProductCreatedBies { get; set; } = [];

    public ICollection<Product> ProductLatestUpdatedBies { get; set; } = [];

    public ICollection<ProductPriceHistory> ProductPriceHistories { get; set; } = [];

    public ICollection<Checkout> CheckoutCreatedBies { get; set; } = [];

    public ICollection<Checkout> CheckoutCustomers { get; set; } = [];

    public ICollection<Page> PageCreatedBies { get; set; } = [];

    public ICollection<Page> PageLatestUpdatedBies { get; set; } = [];

    public ICollection<Comment> Comments { get; set; } = [];

    public ICollection<UserAddress> UserAddresses { get; set; } = [];

    public ICollection<UserClaim> UserClaims { get; set; } = [];

    public ICollection<UserLogin> UserLogins { get; set; } = [];

    public ICollection<UserToken> UserTokens { get; set; } = [];

    public UserAddress? DefaultBillingAddress { get; set; }

    public UserAddress? DefaultShippingAddress { get; set; }

    public ICollection<StockHistory> StockHistories { get; set; } = [];

    public ICollection<NewsItem> NewsItemCreatedBies { get; set; } = [];

    public ICollection<NewsItem> NewsItemLatestUpdatedBies { get; set; } = [];

    public ICollection<Order> OrderCreatedBies { get; set; } = [];

    public ICollection<Order> OrderCustomers { get; set; } = [];

    public ICollection<OrderHistory> OrderHistories { get; set; } = [];

    public ICollection<Order> OrderLatestUpdatedBies { get; set; } = [];

    public ICollection<CartRuleUsage> CartRuleUsages { get; set; } = [];

    public ICollection<ComparingProduct> ComparingProducts { get; set; } = [];

    public ICollection<Reply> Replies { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public ICollection<Shipment> Shipments { get; set; } = [];

    public ICollection<CartItem> CartItems { get; set; } = [];

    public Vendor? Vendor { get; set; }

    public ICollection<WishList> WishLists { get; set; } = [];

    public ICollection<CustomerGroup> CustomerGroups { get; set; } = [];

    public ICollection<UserRole> Roles { get; set; } = [];
}

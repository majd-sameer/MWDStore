using System;
using System.Collections.Generic;

namespace Store.Domain;

public class UserAddress
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public long AddressId { get; set; }

    public int AddressType { get; set; }

    public DateTimeOffset? LastUsedOn { get; set; }

    public Address Address { get; set; } = null!;

    public ICollection<User> UserDefaultBillingAddresses { get; set; } = [];

    public ICollection<User> UserDefaultShippingAddresses { get; set; } = [];

    public User User { get; set; } = null!;
}


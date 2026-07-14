namespace Store.Application.Orders;

/// <summary>
/// The address fields needed to create an order: copied into an immutable <c>OrderAddress</c> and used to
/// resolve the tax rate and shipping availability.
/// </summary>
public sealed class OrderAddressInfo
{
    public string? ContactName { get; set; }

    public string? Phone { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? ZipCode { get; set; }

    public long? DistrictId { get; set; }

    public long StateOrProvinceId { get; set; }

    public string? CountryId { get; set; }
}

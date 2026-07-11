using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>Seeding helpers shared by the cart and order totals tests.</summary>
internal static class CheckoutTestSupport
{
    public const long CustomerId = 1;

    public static Product NewProduct(
        long id, string name, decimal price,
        decimal? specialPrice = null, DateTimeOffset? specialStart = null, DateTimeOffset? specialEnd = null,
        long? taxClassId = null, long? vendorId = null,
        bool stockTracking = false, int stock = 0, bool published = true, bool allowToOrder = true,
        string? nameEn = null) => new()
    {
        Id = id,
        Name = new LocalizedString(name, nameEn),
        Slug = "p" + id,
        Price = price,
        SpecialPrice = specialPrice,
        SpecialPriceStart = specialStart,
        SpecialPriceEnd = specialEnd,
        TaxClassId = taxClassId,
        VendorId = vendorId,
        StockTrackingIsEnabled = stockTracking,
        StockQuantity = stock,
        IsPublished = published,
        IsVisibleIndividually = true,
        IsAllowToOrder = allowToOrder,
        CreatedById = 1,
        LatestUpdatedById = 1
    };

    /// <summary>Adds a checkout with the given (product, quantity) lines and returns its id.</summary>
    public static Guid AddCheckout(
        StoreDbContext db,
        IEnumerable<(Product Product, int Quantity)> lines,
        bool isProductPriceIncludeTax = false,
        string? couponCode = null)
    {
        var checkout = new Checkout
        {
            Id = Guid.NewGuid(),
            CustomerId = CustomerId,
            CreatedById = CustomerId,
            IsProductPriceIncludeTax = isProductPriceIncludeTax,
            CouponCode = couponCode
        };

        foreach (var (product, quantity) in lines)
        {
            checkout.CheckoutItems.Add(new CheckoutItem
            {
                ProductId = product.Id,
                Product = product,
                Quantity = quantity,
                CheckoutId = checkout.Id
            });
        }

        db.Set<Checkout>().Add(checkout);
        db.SaveChanges();
        return checkout.Id;
    }

    /// <summary>A US tax rate acting as a wildcard over state/zip.</summary>
    public static void AddTaxRate(StoreDbContext db, long taxClassId, decimal rate, string countryId = "US")
    {
        db.Set<TaxClass>().Add(new TaxClass { Id = taxClassId, Name = "Class " + taxClassId });
        db.Set<TaxRate>().Add(new TaxRate
        {
            Id = taxClassId,
            TaxClassId = taxClassId,
            CountryId = countryId,
            StateOrProvinceId = null,
            ZipCode = null,
            Rate = rate
        });
        db.SaveChanges();
    }
}

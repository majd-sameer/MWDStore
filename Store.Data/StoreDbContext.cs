using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Store.Domain;

namespace Store.Data;

public class StoreDbContext : IdentityDbContext<User, Role, long, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
{
    public StoreDbContext(DbContextOptions<StoreDbContext> options)
        : base(options)
    {
    }

    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ActivityType> ActivityTypes => Set<ActivityType>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeGroup> ProductAttributeGroups => Set<ProductAttributeGroup>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<ProductLink> ProductLinks => Set<ProductLink>();
    public DbSet<ProductMedium> ProductMedia => Set<ProductMedium>();
    public DbSet<ProductOption> ProductOptions => Set<ProductOption>();
    public DbSet<ProductOptionCombination> ProductOptionCombinations => Set<ProductOptionCombination>();
    public DbSet<ProductOptionValue> ProductOptionValues => Set<ProductOptionValue>();
    public DbSet<ProductPriceHistory> ProductPriceHistories => Set<ProductPriceHistory>();
    public DbSet<ProductTemplate> ProductTemplates => Set<ProductTemplate>();
    public DbSet<Checkout> Checkouts => Set<Checkout>();
    public DbSet<CheckoutItem> CheckoutItems => Set<CheckoutItem>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<Page> Pages => Set<Page>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<ContactArea> ContactAreas => Set<ContactArea>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<CustomerGroup> CustomerGroups => Set<CustomerGroup>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<EntityType> EntityTypes => Set<EntityType>();
    public DbSet<Medium> Media => Set<Medium>();
    // User, Role, UserClaim, UserRole, UserLogin, RoleClaim and UserToken DbSets are provided by
    // IdentityDbContext<...>.
    public DbSet<StateOrProvince> StateOrProvinces => Set<StateOrProvince>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Widget> Widgets => Set<Widget>();
    public DbSet<WidgetInstance> WidgetInstances => Set<WidgetInstance>();
    public DbSet<WidgetZone> WidgetZones => Set<WidgetZone>();
    public DbSet<ProductBackInStockSubscription> ProductBackInStockSubscriptions => Set<ProductBackInStockSubscription>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<StockHistory> StockHistories => Set<StockHistory>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Culture> Cultures => Set<Culture>();
    public DbSet<LocalizedContentProperty> LocalizedContentProperties => Set<LocalizedContentProperty>();
    public DbSet<Resource> Resources => Set<Resource>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<NewsCategory> NewsCategories => Set<NewsCategory>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderAddress> OrderAddresses => Set<OrderAddress>();
    public DbSet<OrderHistory> OrderHistories => Set<OrderHistory>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<CartRule> CartRules => Set<CartRule>();
    public DbSet<CartRuleUsage> CartRuleUsages => Set<CartRuleUsage>();
    public DbSet<CatalogRule> CatalogRules => Set<CatalogRule>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<ComparingProduct> ComparingProducts => Set<ComparingProduct>();
    public DbSet<RecentlyViewedProduct> RecentlyViewedProducts => Set<RecentlyViewedProduct>();
    public DbSet<Reply> Replies => Set<Reply>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Query> Queries => Set<Query>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentItem> ShipmentItems => Set<ShipmentItem>();
    public DbSet<PriceAndDestination> PriceAndDestinations => Set<PriceAndDestination>();
    public DbSet<ShippingProvider> ShippingProviders => Set<ShippingProvider>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<TaxClass> TaxClasses => Set<TaxClass>();
    public DbSet<TaxRate> TaxRates => Set<TaxRate>();
    public DbSet<WishList> WishLists => Set<WishList>();
    public DbSet<WishListItem> WishListItems => Set<WishListItem>();
    public DbSet<EmailAccount> EmailAccounts => Set<EmailAccount>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<QueuedEmail> QueuedEmails => Set<QueuedEmail>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Let Identity configure its entities first, then override table/column/index names back to the
        // existing schema via our IEntityTypeConfiguration classes.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreDbContext).Assembly);
    }
}


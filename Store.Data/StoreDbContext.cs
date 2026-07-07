using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Store.Data.Auditing;
using Store.Domain;

namespace Store.Data;

public class StoreDbContext : IdentityDbContext<User, Role, long, UserClaim, UserRole, UserLogin, RoleClaim, UserToken>
{
    private readonly IAuditContext? _auditContext;

    /// <summary>
    /// The audit context is optional so direct construction (tests, design-time) still works; at
    /// runtime DI supplies the scoped buffer that <c>SaveChanges</c> writes captured changes into.
    /// </summary>
    public StoreDbContext(DbContextOptions<StoreDbContext> options, IAuditContext? auditContext = null)
        : base(options)
    {
        _auditContext = auditContext;
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
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
    public DbSet<NewsCategory> NewsCategories => Set<NewsCategory>();
    public DbSet<NewsItem> NewsItems => Set<NewsItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderAddress> OrderAddresses => Set<OrderAddress>();
    public DbSet<OrderHistory> OrderHistories => Set<OrderHistory>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentProvider> PaymentProviders => Set<PaymentProvider>();
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Let Identity configure its entities first, then override table/column/index names back to the
        // existing schema via our IEntityTypeConfiguration classes.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StoreDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        CaptureAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        CaptureAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Snapshots the pending changes (Added/Modified/Deleted) into the scoped audit buffer before
    /// they are flattened by the save — changed scalar properties only, secrets stripped, and the
    /// audit table itself never re-audited. No-op when no audit context is attached.
    /// </summary>
    private void CaptureAudit()
    {
        if (_auditContext is null)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog)
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var change = new AuditChange
            {
                EntityType = entry.Entity.GetType().Name,
                State = entry.State.ToString(),
                EntityId = ResolveEntityId(entry),
                EntityName = ResolveEntityName(entry),
            };

            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (property.Metadata.IsPrimaryKey() || AuditSecrets.IsSecret(name))
                {
                    continue;
                }

                switch (entry.State)
                {
                    case EntityState.Added:
                        change.NewValues[name] = property.CurrentValue;
                        break;
                    case EntityState.Deleted:
                        change.OldValues[name] = property.OriginalValue;
                        break;
                    case EntityState.Modified when property.IsModified
                        && !Equals(property.OriginalValue, property.CurrentValue):
                        change.OldValues[name] = property.OriginalValue;
                        change.NewValues[name] = property.CurrentValue;
                        break;
                }
            }

            // A "Modified" entry whose only changes were keys/secrets carries no real diff — skip it.
            if (entry.State == EntityState.Modified && change.NewValues.Count == 0)
            {
                continue;
            }

            _auditContext.Add(change);
        }
    }

    private static long? ResolveEntityId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        var keyProperty = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
        if (keyProperty is null)
        {
            return null;
        }

        var value = entry.Property(keyProperty.Name).CurrentValue;
        return value switch
        {
            long l => l,
            int i => i,
            _ => null,
        };
    }

    private static readonly string[] NameProperties =
        ["Name", "Title", "OrderNumber", "Code", "Slug", "Email", "UserName"];

    private static string? ResolveEntityName(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        foreach (var candidate in NameProperties)
        {
            var property = entry.Metadata.FindProperty(candidate);
            if (property is null)
            {
                continue;
            }

            if (entry.Property(candidate).CurrentValue is string { Length: > 0 } value)
            {
                return value;
            }
        }

        return null;
    }
}


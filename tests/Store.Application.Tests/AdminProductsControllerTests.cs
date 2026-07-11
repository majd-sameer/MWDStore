using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Store.Api.Controllers.Admin;
using Store.Api.Infrastructure;
using Store.Api.Models;
using Store.Application.Catalog;
using Store.Data;
using Store.Domain;

namespace Store.Application.Tests;

/// <summary>
/// Admin product save-pipeline behaviours that live in <see cref="AdminProductsController"/>:
/// variation English-name composition (create + update + parent-without-English), soft-delete restore
/// (product and its Super-linked children), and the product-form stock change mirrored onto the
/// per-warehouse <c>Stock</c> rows with a <c>StockHistory</c> audit row.
/// </summary>
public class AdminProductsControllerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 4, 12, 0, 0, TimeSpan.Zero);

    private sealed class FakeMediaStorage : IMediaStorage
    {
        public Task<string> SaveAsync(Stream stream, string originalFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(originalFileName);

        public void Delete(string? fileName) { }

        public string? GetUrl(string? fileName) => fileName;
    }

    private static AdminProductsController NewController(StoreDbContext db, long userId = 1)
    {
        var controller = new AdminProductsController(db, new FixedTimeProvider(Now), new FakeMediaStorage());
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private static async Task<long> CreateAsync(AdminProductsController controller, ProductUpsertRequest request)
    {
        var result = await controller.Create(request, default);
        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        return Assert.IsType<AdminProductDetail>(created.Value).Id;
    }

    // ----- Variation English-name composition ----------------------------------------------------

    [Fact]
    public async Task Create_ComposesVariationEnglishName_FromParentEnglishPlusStrippedSuffix()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var parentId = await CreateAsync(controller, new ProductUpsertRequest
        {
            Name = "Qamis",
            NameEn = "Shirt",
            Slug = "qamis",
            Price = 20m,
            Variations = [new ProductVariationRequest { Name = "Qamis Red", Price = 22m }]
        });

        var child = db.Products.Single(p => p.Id != parentId && !p.IsVisibleIndividually);
        Assert.Equal("Qamis Red", child.Name.Ar);
        Assert.Equal("Shirt Red", child.Name.En);
    }

    [Fact]
    public async Task Create_LeavesVariationEnglishNull_WhenParentHasNoEnglish()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var parentId = await CreateAsync(controller, new ProductUpsertRequest
        {
            Name = "Qamis",
            Slug = "qamis",
            Price = 20m,
            Variations = [new ProductVariationRequest { Name = "Qamis Red", Price = 22m }]
        });

        var child = db.Products.Single(p => p.Id != parentId && !p.IsVisibleIndividually);
        Assert.Equal("Qamis Red", child.Name.Ar);
        Assert.Null(child.Name.En);
    }

    [Fact]
    public async Task Update_RefreshesVariationEnglishName_WhenParentGainsEnglish()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var parentId = await CreateAsync(controller, new ProductUpsertRequest
        {
            Name = "Qamis",
            Slug = "qamis",
            Price = 20m,
            Variations = [new ProductVariationRequest { Name = "Qamis Red", Price = 22m }]
        });

        var childBefore = db.Products.Single(p => p.Id != parentId && !p.IsVisibleIndividually);
        Assert.Null(childBefore.Name.En);

        var update = await controller.Update(parentId, new ProductUpsertRequest
        {
            Name = "Qamis",
            NameEn = "Shirt",
            Slug = "qamis",
            Price = 20m,
            Variations = [new ProductVariationRequest { Name = "Qamis Red", Price = 22m }]
        }, default);
        Assert.IsType<OkObjectResult>(update.Result);

        var child = db.Products.Single(p => p.Id != parentId && !p.IsVisibleIndividually);
        Assert.Equal("Qamis Red", child.Name.Ar);
        Assert.Equal("Shirt Red", child.Name.En);
    }

    // ----- Restore -------------------------------------------------------------------------------

    [Fact]
    public async Task Restore_UnDeletesProductAndSuperLinkedChildren()
    {
        using var db = TestDb.New();

        var parent = new Product
        {
            Id = 1,
            Name = new LocalizedString("Qamis"),
            Slug = "qamis",
            IsVisibleIndividually = true,
            IsDeleted = true,
            CreatedById = 1,
            LatestUpdatedById = 1
        };
        var child = new Product
        {
            Id = 2,
            Name = new LocalizedString("Qamis Red"),
            Slug = "qamis-red",
            IsVisibleIndividually = false,
            IsDeleted = true,
            CreatedById = 1,
            LatestUpdatedById = 1
        };
        db.Products.AddRange(parent, child);
        db.ProductLinks.Add(new ProductLink
        {
            ProductId = parent.Id,
            LinkedProductId = child.Id,
            LinkType = ProductLinkType.Super
        });
        db.SaveChanges();

        var controller = NewController(db);
        var result = await controller.Restore(1, default);

        Assert.IsType<NoContentResult>(result);
        Assert.False(db.Products.Single(p => p.Id == 1).IsDeleted);
        Assert.False(db.Products.Single(p => p.Id == 2).IsDeleted);
    }

    [Fact]
    public async Task Restore_ReturnsNotFound_ForMissingProduct()
    {
        using var db = TestDb.New();
        var controller = NewController(db);

        var result = await controller.Restore(999, default);

        Assert.IsType<NotFoundResult>(result);
    }

    // ----- Warehouse stock mirror ----------------------------------------------------------------

    private static ProductUpsertRequest UpsertFor(Product p, int stock) => new()
    {
        Name = p.Name.Ar!,
        Slug = p.Slug,
        Price = p.Price,
        StockTrackingIsEnabled = p.StockTrackingIsEnabled,
        StockQuantity = stock
    };

    [Fact]
    public async Task Update_MirrorsStockChange_ToSingleWarehouseRow_WithHistory()
    {
        using var db = TestDb.New();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", AddressId = 1 });
        var product = new Product
        {
            Id = 1,
            Name = new LocalizedString("Widget"),
            Slug = "widget",
            Price = 10m,
            StockQuantity = 5,
            StockTrackingIsEnabled = true,
            IsVisibleIndividually = true,
            CreatedById = 1,
            LatestUpdatedById = 1
        };
        db.Products.Add(product);
        db.Stocks.Add(new Stock { Id = 1, ProductId = 1, WarehouseId = 1, Quantity = 5 });
        db.SaveChanges();

        var controller = NewController(db);
        var result = await controller.Update(1, UpsertFor(product, stock: 12), default);
        Assert.IsType<OkObjectResult>(result.Result);

        Assert.Equal(12, db.Stocks.Single().Quantity);          // warehouse row mirrored
        Assert.Equal(12, db.Products.Single().StockQuantity);   // product not double-counted
        var history = Assert.Single(db.StockHistories);
        Assert.Equal(7, history.AdjustedQuantity);
        Assert.Equal(1, history.WarehouseId);
        Assert.Equal(Now, history.CreatedOn);
    }

    [Fact]
    public async Task Update_DoesNotWriteHistory_WhenStockUnchanged()
    {
        using var db = TestDb.New();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", AddressId = 1 });
        var product = new Product
        {
            Id = 1,
            Name = new LocalizedString("Widget"),
            Slug = "widget",
            Price = 10m,
            StockQuantity = 5,
            IsVisibleIndividually = true,
            CreatedById = 1,
            LatestUpdatedById = 1
        };
        db.Products.Add(product);
        db.Stocks.Add(new Stock { Id = 1, ProductId = 1, WarehouseId = 1, Quantity = 5 });
        db.SaveChanges();

        var controller = NewController(db);
        await controller.Update(1, UpsertFor(product, stock: 5), default);

        Assert.Equal(5, db.Stocks.Single().Quantity);
        Assert.Empty(db.StockHistories);
    }

    [Fact]
    public async Task Create_MirrorsInitialStock_ByCreatingWarehouseRow()
    {
        using var db = TestDb.New();
        db.Warehouses.Add(new Warehouse { Id = 1, Name = "Main", AddressId = 1 });
        db.SaveChanges();

        var controller = NewController(db);
        var productId = await CreateAsync(controller, new ProductUpsertRequest
        {
            Name = "Widget",
            Slug = "widget",
            Price = 10m,
            StockQuantity = 8
        });

        var stock = db.Stocks.Single(s => s.ProductId == productId);
        Assert.Equal(1, stock.WarehouseId);
        Assert.Equal(8, stock.Quantity);
        var history = Assert.Single(db.StockHistories);
        Assert.Equal(8, history.AdjustedQuantity);
    }
}

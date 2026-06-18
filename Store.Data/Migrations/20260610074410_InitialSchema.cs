using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityType",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSetting",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Module = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsVisibleInCommonSettingPage = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSetting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Brand",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Brand", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CartRule",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsCouponRequired = table.Column<bool>(type: "bit", nullable: false),
                    RuleToApply = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DiscountStep = table.Column<int>(type: "int", nullable: true),
                    UsageLimitPerCoupon = table.Column<int>(type: "int", nullable: true),
                    UsageLimitPerCustomer = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CatalogRule",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EndOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RuleToApply = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRule", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactArea",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactArea", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Country",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Code3 = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsBillingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsShippingEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsCityEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsZipCodeEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDistrictEnabled = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Country", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Culture",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Culture", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerGroup",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EntityType",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsMenuable = table.Column<bool>(type: "bit", nullable: false),
                    AreaName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RoutingController = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RoutingAction = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntityType", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Media",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Caption = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FileSize = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MediaType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Media", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Menu",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Menu", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NewsCategory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MetaTitle = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsCategory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentProvider",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ConfigureUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    LandingViewComponentName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AdditionalSettings = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProvider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeGroup",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeGroup", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductBackInStockSubscription",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductBackInStockSubscription", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductOption",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOption", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductTemplate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTemplate", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Query",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    QueryText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ResultsCount = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Query", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecentlyViewedProduct",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    LatestViewedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecentlyViewedProduct", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Role",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Role", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShippingProvider",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ConfigureUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ToAllShippingEnabledCountries = table.Column<bool>(type: "bit", nullable: false),
                    OnlyCountryIdsString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ToAllShippingEnabledStatesOrProvinces = table.Column<bool>(type: "bit", nullable: false),
                    OnlyStateOrProvinceIdsString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AdditionalSettings = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ShippingPriceServiceTypeName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingProvider", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxClass",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxClass", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vendor",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendor", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Widget",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ViewComponentName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreateUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EditUrl = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Widget", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WidgetZone",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetZone", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Activity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityTypeId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    EntityTypeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Activity_ActivityType_ActivityTypeId",
                        column: x => x.ActivityTypeId,
                        principalTable: "ActivityType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Coupon",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartRuleId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupon", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Coupon_CartRule_CartRuleId",
                        column: x => x.CartRuleId,
                        principalTable: "CartRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Contact",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FullName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EmailAddress = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Content = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ContactAreaId = table.Column<long>(type: "bigint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contact", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Contact_ContactArea_ContactAreaId",
                        column: x => x.ContactAreaId,
                        principalTable: "ContactArea",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StateOrProvince",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateOrProvince", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StateOrProvince_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LocalizedContentProperty",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CultureId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProperyName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizedContentProperty", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalizedContentProperty_Culture_CultureId",
                        column: x => x.CultureId,
                        principalTable: "Culture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Resource",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CultureId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resource", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Resource_Culture_CultureId",
                        column: x => x.CultureId,
                        principalTable: "Culture",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartRuleCustomerGroup",
                columns: table => new
                {
                    CartRuleId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerGroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartRuleCustomerGroup", x => new { x.CartRuleId, x.CustomerGroupId });
                    table.ForeignKey(
                        name: "FK_CartRuleCustomerGroup_CartRule_CartRuleId",
                        column: x => x.CartRuleId,
                        principalTable: "CartRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartRuleCustomerGroup_CustomerGroup_CustomerGroupId",
                        column: x => x.CustomerGroupId,
                        principalTable: "CustomerGroup",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CatalogRuleCustomerGroup",
                columns: table => new
                {
                    CatalogRuleId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerGroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CatalogRuleCustomerGroup", x => new { x.CatalogRuleId, x.CustomerGroupId });
                    table.ForeignKey(
                        name: "FK_CatalogRuleCustomerGroup_CatalogRule_CatalogRuleId",
                        column: x => x.CatalogRuleId,
                        principalTable: "CatalogRule",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CatalogRuleCustomerGroup_CustomerGroup_CustomerGroupId",
                        column: x => x.CustomerGroupId,
                        principalTable: "CustomerGroup",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Entity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    EntityTypeId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Entity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Entity_EntityType_EntityTypeId",
                        column: x => x.EntityTypeId,
                        principalTable: "EntityType",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Category",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MetaTitle = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IncludeInMenu = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    ThumbnailImageId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Category", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Category_Category_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Category",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Category_Media_ThumbnailImageId",
                        column: x => x.ThumbnailImageId,
                        principalTable: "Media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductAttribute",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    GroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttribute", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttribute_ProductAttributeGroup_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ProductAttributeGroup",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RoleClaim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaim_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WidgetInstance",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PublishStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PublishEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    WidgetId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    WidgetZoneId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HtmlData = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WidgetInstance", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WidgetInstance_WidgetZone_WidgetZoneId",
                        column: x => x.WidgetZoneId,
                        principalTable: "WidgetZone",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WidgetInstance_Widget_WidgetId",
                        column: x => x.WidgetId,
                        principalTable: "Widget",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "District",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StateOrProvinceId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Location = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_District", x => x.Id);
                    table.ForeignKey(
                        name: "FK_District_StateOrProvince_StateOrProvinceId",
                        column: x => x.StateOrProvinceId,
                        principalTable: "StateOrProvince",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TaxRate",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaxClassId = table.Column<long>(type: "bigint", nullable: false),
                    CountryId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    StateOrProvinceId = table.Column<long>(type: "bigint", nullable: true),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ZipCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxRate", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaxRate_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaxRate_StateOrProvince_StateOrProvinceId",
                        column: x => x.StateOrProvinceId,
                        principalTable: "StateOrProvince",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_TaxRate_TaxClass_TaxClassId",
                        column: x => x.TaxClassId,
                        principalTable: "TaxClass",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MenuItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    MenuId = table.Column<long>(type: "bigint", nullable: false),
                    EntityId = table.Column<long>(type: "bigint", nullable: true),
                    CustomLink = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItem_Entity_EntityId",
                        column: x => x.EntityId,
                        principalTable: "Entity",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MenuItem_MenuItem_ParentId",
                        column: x => x.ParentId,
                        principalTable: "MenuItem",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MenuItem_Menu_MenuId",
                        column: x => x.MenuId,
                        principalTable: "Menu",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CartRuleCategory",
                columns: table => new
                {
                    CartRuleId = table.Column<long>(type: "bigint", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartRuleCategory", x => new { x.CartRuleId, x.CategoryId });
                    table.ForeignKey(
                        name: "FK_CartRuleCategory_CartRule_CartRuleId",
                        column: x => x.CartRuleId,
                        principalTable: "CartRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CartRuleCategory_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Category",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductTemplateProductAttribute",
                columns: table => new
                {
                    ProductTemplateId = table.Column<long>(type: "bigint", nullable: false),
                    ProductAttributeId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductTemplateProductAttribute", x => new { x.ProductTemplateId, x.ProductAttributeId });
                    table.ForeignKey(
                        name: "FK_ProductTemplateProductAttribute_ProductAttribute_ProductAttributeId",
                        column: x => x.ProductAttributeId,
                        principalTable: "ProductAttribute",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductTemplateProductAttribute_ProductTemplate_ProductTemplateId",
                        column: x => x.ProductTemplateId,
                        principalTable: "ProductTemplate",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Address",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    City = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DistrictId = table.Column<long>(type: "bigint", nullable: true),
                    StateOrProvinceId = table.Column<long>(type: "bigint", nullable: false),
                    CountryId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Address", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Address_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Address_District_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "District",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Address_StateOrProvince_StateOrProvinceId",
                        column: x => x.StateOrProvinceId,
                        principalTable: "StateOrProvince",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderAddress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ContactName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AddressLine1 = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AddressLine2 = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    City = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DistrictId = table.Column<long>(type: "bigint", nullable: true),
                    StateOrProvinceId = table.Column<long>(type: "bigint", nullable: false),
                    CountryId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderAddress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderAddress_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderAddress_District_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "District",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrderAddress_StateOrProvince_StateOrProvinceId",
                        column: x => x.StateOrProvinceId,
                        principalTable: "StateOrProvince",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PriceAndDestination",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CountryId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    StateOrProvinceId = table.Column<long>(type: "bigint", nullable: true),
                    DistrictId = table.Column<long>(type: "bigint", nullable: true),
                    ZipCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MinOrderSubtotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShippingPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceAndDestination", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PriceAndDestination_Country_CountryId",
                        column: x => x.CountryId,
                        principalTable: "Country",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceAndDestination_District_DistrictId",
                        column: x => x.DistrictId,
                        principalTable: "District",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PriceAndDestination_StateOrProvince_StateOrProvinceId",
                        column: x => x.StateOrProvinceId,
                        principalTable: "StateOrProvince",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Warehouse",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    AddressId = table.Column<long>(type: "bigint", nullable: false),
                    VendorId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouse", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Warehouse_Address_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Address",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Warehouse_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CartItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    VendorId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CartRuleProduct",
                columns: table => new
                {
                    CartRuleId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartRuleProduct", x => new { x.CartRuleId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_CartRuleProduct_CartRule_CartRuleId",
                        column: x => x.CartRuleId,
                        principalTable: "CartRule",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartRuleUsage",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CartRuleId = table.Column<long>(type: "bigint", nullable: false),
                    CouponId = table.Column<long>(type: "bigint", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartRuleUsage", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartRuleUsage_CartRule_CartRuleId",
                        column: x => x.CartRuleId,
                        principalTable: "CartRule",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CartRuleUsage_Coupon_CouponId",
                        column: x => x.CouponId,
                        principalTable: "Coupon",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Checkout",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CouponCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CouponRuleName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ShippingMethod = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsProductPriceIncludeTax = table.Column<bool>(type: "bit", nullable: false),
                    ShippingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    VendorId = table.Column<long>(type: "bigint", nullable: true),
                    ShippingData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrderNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Checkout", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CheckoutItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CheckoutId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CheckoutItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CheckoutItem_Checkout_CheckoutId",
                        column: x => x.CheckoutId,
                        principalTable: "Checkout",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Comment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CommentText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommenterName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EntityTypeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EntityId = table.Column<long>(type: "bigint", nullable: false),
                    ParentId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Comment_Comment_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Comment",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ComparingProduct",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComparingProduct", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CustomerGroupUser",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CustomerGroupId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerGroupUser", x => new { x.UserId, x.CustomerGroupId });
                    table.ForeignKey(
                        name: "FK_CustomerGroupUser_CustomerGroup_CustomerGroupId",
                        column: x => x.CustomerGroupId,
                        principalTable: "CustomerGroup",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NewsItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShortContent = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    FullContent = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ThumbnailImageId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MetaTitle = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedById = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NewsItem_Media_ThumbnailImageId",
                        column: x => x.ThumbnailImageId,
                        principalTable: "Media",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "NewsItemCategory",
                columns: table => new
                {
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    NewsItemId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NewsItemCategory", x => new { x.CategoryId, x.NewsItemId });
                    table.ForeignKey(
                        name: "FK_NewsItemCategory_NewsCategory_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "NewsCategory",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NewsItemCategory_NewsItem_NewsItemId",
                        column: x => x.NewsItemId,
                        principalTable: "NewsItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Order",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<long>(type: "bigint", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    VendorId = table.Column<long>(type: "bigint", nullable: true),
                    CouponCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CouponRuleName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubTotalWithDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ShippingAddressId = table.Column<long>(type: "bigint", nullable: false),
                    BillingAddressId = table.Column<long>(type: "bigint", nullable: false),
                    OrderStatus = table.Column<int>(type: "int", nullable: false),
                    OrderNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ParentId = table.Column<long>(type: "bigint", nullable: true),
                    IsMasterOrder = table.Column<bool>(type: "bit", nullable: false),
                    ShippingMethod = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ShippingFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrderTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PaymentFeeAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Order", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Order_OrderAddress_BillingAddressId",
                        column: x => x.BillingAddressId,
                        principalTable: "OrderAddress",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Order_OrderAddress_ShippingAddressId",
                        column: x => x.ShippingAddressId,
                        principalTable: "OrderAddress",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Order_Order_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Order",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Payment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    GatewayTransactionId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    FailureMessage = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payment_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    OldStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: false),
                    OrderSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderHistory_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: true),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    ProductPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TaxPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItem_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Page",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MetaTitle = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedById = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Page", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Product",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShortDescription = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SpecialPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SpecialPriceStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SpecialPriceEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HasOptions = table.Column<bool>(type: "bit", nullable: false),
                    IsVisibleIndividually = table.Column<bool>(type: "bit", nullable: false),
                    IsFeatured = table.Column<bool>(type: "bit", nullable: false),
                    IsCallForPricing = table.Column<bool>(type: "bit", nullable: false),
                    IsAllowToOrder = table.Column<bool>(type: "bit", nullable: false),
                    StockTrackingIsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    StockQuantity = table.Column<int>(type: "int", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Gtin = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    VendorId = table.Column<long>(type: "bigint", nullable: true),
                    ThumbnailImageId = table.Column<long>(type: "bigint", nullable: true),
                    ReviewsCount = table.Column<int>(type: "int", nullable: false),
                    RatingAverage = table.Column<double>(type: "float", nullable: true),
                    BrandId = table.Column<long>(type: "bigint", nullable: true),
                    TaxClassId = table.Column<long>(type: "bigint", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    MetaTitle = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaKeywords = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    MetaDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedById = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Product", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Product_Brand_BrandId",
                        column: x => x.BrandId,
                        principalTable: "Brand",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Product_Media_ThumbnailImageId",
                        column: x => x.ThumbnailImageId,
                        principalTable: "Media",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Product_TaxClass_TaxClassId",
                        column: x => x.TaxClassId,
                        principalTable: "TaxClass",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductAttributeValue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AttributeId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAttributeValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductAttributeValue_ProductAttribute_AttributeId",
                        column: x => x.AttributeId,
                        principalTable: "ProductAttribute",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductAttributeValue_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductCategory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsFeaturedProduct = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductCategory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductCategory_Category_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Category",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductCategory_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductLink",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    LinkedProductId = table.Column<long>(type: "bigint", nullable: false),
                    LinkType = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductLink", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductLink_Product_LinkedProductId",
                        column: x => x.LinkedProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductLink_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductMedia",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    MediaId = table.Column<long>(type: "bigint", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductMedia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductMedia_Media_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Media",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductMedia_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductOptionCombination",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    OptionId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SortIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionCombination", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductOptionCombination_ProductOption_OptionId",
                        column: x => x.OptionId,
                        principalTable: "ProductOption",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductOptionCombination_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductOptionValue",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OptionId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DisplayType = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SortIndex = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductOptionValue", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductOptionValue_ProductOption_OptionId",
                        column: x => x.OptionId,
                        principalTable: "ProductOption",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProductOptionValue_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Stock",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReservedQuantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Stock_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Stock_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ProductPriceHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    OldPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SpecialPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    SpecialPriceStart = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SpecialPriceEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductPriceHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductPriceHistory_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Reply",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReplierName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reply", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Review",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    ReviewerName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EntityTypeId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    EntityId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Review", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Shipment",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<long>(type: "bigint", nullable: false),
                    TrackingNumber = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    VendorId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shipment_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Order",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Shipment_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ShipmentItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ShipmentId = table.Column<long>(type: "bigint", nullable: false),
                    OrderItemId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentItem_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ShipmentItem_Shipment_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipment",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "StockHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    WarehouseId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedById = table.Column<long>(type: "bigint", nullable: false),
                    AdjustedQuantity = table.Column<long>(type: "bigint", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockHistory_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_StockHistory_Warehouse_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouse",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "User",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    VendorId = table.Column<long>(type: "bigint", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DefaultShippingAddressId = table.Column<long>(type: "bigint", nullable: true),
                    DefaultBillingAddressId = table.Column<long>(type: "bigint", nullable: true),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Culture = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ExtensionData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_User", x => x.Id);
                    table.ForeignKey(
                        name: "FK_User_Vendor_VendorId",
                        column: x => x.VendorId,
                        principalTable: "Vendor",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserAddress",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    AddressId = table.Column<long>(type: "bigint", nullable: false),
                    AddressType = table.Column<int>(type: "int", nullable: false),
                    LastUsedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserAddress", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserAddress_Address_AddressId",
                        column: x => x.AddressId,
                        principalTable: "Address",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserAddress_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserClaim",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaim", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaim_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogin",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogin", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogin_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRole",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    RoleId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRole", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRole_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Role",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_UserRole_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserToken",
                columns: table => new
                {
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserToken", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserToken_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WishList",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SharingCode = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishList", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishList_User_UserId",
                        column: x => x.UserId,
                        principalTable: "User",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WishListItem",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WishListId = table.Column<long>(type: "bigint", nullable: false),
                    ProductId = table.Column<long>(type: "bigint", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LatestUpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WishListItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WishListItem_Product_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Product",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WishListItem_WishList_WishListId",
                        column: x => x.WishListId,
                        principalTable: "WishList",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_Activity_ActivityTypeId",
                table: "Activity",
                column: "ActivityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Address_CountryId",
                table: "Address",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Address_DistrictId",
                table: "Address",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Address_StateOrProvinceId",
                table: "Address",
                column: "StateOrProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCart_CartItem_CustomerId",
                table: "CartItem",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_ShoppingCart_CartItem_ProductId",
                table: "CartItem",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CartRuleCategory_CategoryId",
                table: "CartRuleCategory",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CartRuleCustomerGroup_CustomerGroupId",
                table: "CartRuleCustomerGroup",
                column: "CustomerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CartRuleProduct_ProductId",
                table: "CartRuleProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CartRuleUsage_CartRuleId",
                table: "CartRuleUsage",
                column: "CartRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CartRuleUsage_CouponId",
                table: "CartRuleUsage",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CartRuleUsage_UserId",
                table: "CartRuleUsage",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_CatalogRuleCustomerGroup_CustomerGroupId",
                table: "CatalogRuleCustomerGroup",
                column: "CustomerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Category_ParentId",
                table: "Category",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Category_ThumbnailImageId",
                table: "Category",
                column: "ThumbnailImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_Checkout_CreatedById",
                table: "Checkout",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_Checkout_CustomerId",
                table: "Checkout",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_CheckoutItem_CheckoutId",
                table: "CheckoutItem",
                column: "CheckoutId");

            migrationBuilder.CreateIndex(
                name: "IX_Checkouts_CheckoutItem_ProductId",
                table: "CheckoutItem",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_Comment_ParentId",
                table: "Comment",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_Comment_UserId",
                table: "Comment",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComparison_ComparingProduct_ProductId",
                table: "ComparingProduct",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductComparison_ComparingProduct_UserId",
                table: "ComparingProduct",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Contacts_Contact_ContactAreaId",
                table: "Contact",
                column: "ContactAreaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pricing_Coupon_CartRuleId",
                table: "Coupon",
                column: "CartRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_CustomerGroup_Name",
                table: "CustomerGroup",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Core_CustomerGroupUser_CustomerGroupId",
                table: "CustomerGroupUser",
                column: "CustomerGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_District_StateOrProvinceId",
                table: "District",
                column: "StateOrProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_Entity_EntityTypeId",
                table: "Entity",
                column: "EntityTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Localization_LocalizedContentProperty_CultureId",
                table: "LocalizedContentProperty",
                column: "CultureId");

            migrationBuilder.CreateIndex(
                name: "IX_Cms_MenuItem_EntityId",
                table: "MenuItem",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Cms_MenuItem_MenuId",
                table: "MenuItem",
                column: "MenuId");

            migrationBuilder.CreateIndex(
                name: "IX_Cms_MenuItem_ParentId",
                table: "MenuItem",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_News_NewsItem_CreatedById",
                table: "NewsItem",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_News_NewsItem_LatestUpdatedById",
                table: "NewsItem",
                column: "LatestUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_News_NewsItem_ThumbnailImageId",
                table: "NewsItem",
                column: "ThumbnailImageId");

            migrationBuilder.CreateIndex(
                name: "IX_News_NewsItemCategory_NewsItemId",
                table: "NewsItemCategory",
                column: "NewsItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_BillingAddressId",
                table: "Order",
                column: "BillingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_CreatedById",
                table: "Order",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_CustomerId",
                table: "Order",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_LatestUpdatedById",
                table: "Order",
                column: "LatestUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_ParentId",
                table: "Order",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_ShippingAddressId",
                table: "Order",
                column: "ShippingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderAddress_CountryId",
                table: "OrderAddress",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderAddress_DistrictId",
                table: "OrderAddress",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderAddress_StateOrProvinceId",
                table: "OrderAddress",
                column: "StateOrProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderHistory_CreatedById",
                table: "OrderHistory",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderHistory_OrderId",
                table: "OrderHistory",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderItem_OrderId",
                table: "OrderItem",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderItem_ProductId",
                table: "OrderItem",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Cms_Page_CreatedById",
                table: "Page",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Cms_Page_LatestUpdatedById",
                table: "Page",
                column: "LatestUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Payment_OrderId",
                table: "Payment",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingTableRate_PriceAndDestination_CountryId",
                table: "PriceAndDestination",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingTableRate_PriceAndDestination_DistrictId",
                table: "PriceAndDestination",
                column: "DistrictId");

            migrationBuilder.CreateIndex(
                name: "IX_ShippingTableRate_PriceAndDestination_StateOrProvinceId",
                table: "PriceAndDestination",
                column: "StateOrProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Product_BrandId",
                table: "Product",
                column: "BrandId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Product_CreatedById",
                table: "Product",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Product_LatestUpdatedById",
                table: "Product",
                column: "LatestUpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Product_TaxClassId",
                table: "Product",
                column: "TaxClassId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Product_ThumbnailImageId",
                table: "Product",
                column: "ThumbnailImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductAttribute_GroupId",
                table: "ProductAttribute",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductAttributeValue_AttributeId",
                table: "ProductAttributeValue",
                column: "AttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductAttributeValue_ProductId",
                table: "ProductAttributeValue",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductCategory_CategoryId",
                table: "ProductCategory",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductCategory_ProductId",
                table: "ProductCategory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductLink_LinkedProductId",
                table: "ProductLink",
                column: "LinkedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductLink_ProductId",
                table: "ProductLink",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductMedia_MediaId",
                table: "ProductMedia",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductMedia_ProductId",
                table: "ProductMedia",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductOptionCombination_OptionId",
                table: "ProductOptionCombination",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductOptionCombination_ProductId",
                table: "ProductOptionCombination",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductOptionValue_OptionId",
                table: "ProductOptionValue",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductOptionValue_ProductId",
                table: "ProductOptionValue",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductPriceHistory_CreatedById",
                table: "ProductPriceHistory",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductPriceHistory_ProductId",
                table: "ProductPriceHistory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_ProductTemplateProductAttribute_ProductAttributeId",
                table: "ProductTemplateProductAttribute",
                column: "ProductAttributeId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Reply_ReviewId",
                table: "Reply",
                column: "ReviewId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Reply_UserId",
                table: "Reply",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Localization_Resource_CultureId",
                table: "Resource",
                column: "CultureId");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_Review_UserId",
                table: "Review",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "Role",
                column: "NormalizedName",
                unique: true,
                filter: "([NormalizedName] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Core_RoleClaim_RoleId",
                table: "RoleClaim",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Shipment_CreatedById",
                table: "Shipment",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Shipment_OrderId",
                table: "Shipment",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Shipment_WarehouseId",
                table: "Shipment",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentItem_ProductId",
                table: "ShipmentItem",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShipmentItem_ShipmentId",
                table: "ShipmentItem",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_StateOrProvince_CountryId",
                table: "StateOrProvince",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_Stock_ProductId",
                table: "Stock",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_Stock_WarehouseId",
                table: "Stock",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_StockHistory_CreatedById",
                table: "StockHistory",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_StockHistory_ProductId",
                table: "StockHistory",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_StockHistory_WarehouseId",
                table: "StockHistory",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_TaxRate_CountryId",
                table: "TaxRate",
                column: "CountryId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_TaxRate_StateOrProvinceId",
                table: "TaxRate",
                column: "StateOrProvinceId");

            migrationBuilder.CreateIndex(
                name: "IX_Tax_TaxRate_TaxClassId",
                table: "TaxRate",
                column: "TaxClassId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "User",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Core_User_DefaultBillingAddressId",
                table: "User",
                column: "DefaultBillingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_User_DefaultShippingAddressId",
                table: "User",
                column: "DefaultShippingAddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_User_VendorId",
                table: "User",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "User",
                column: "NormalizedUserName",
                unique: true,
                filter: "([NormalizedUserName] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserAddress_AddressId",
                table: "UserAddress",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserAddress_UserId",
                table: "UserAddress",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserClaim_UserId",
                table: "UserClaim",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserLogin_UserId",
                table: "UserLogin",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_UserRole_RoleId",
                table: "UserRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_Warehouse_AddressId",
                table: "Warehouse",
                column: "AddressId");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_Warehouse_VendorId",
                table: "Warehouse",
                column: "VendorId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_WidgetInstance_WidgetId",
                table: "WidgetInstance",
                column: "WidgetId");

            migrationBuilder.CreateIndex(
                name: "IX_Core_WidgetInstance_WidgetZoneId",
                table: "WidgetInstance",
                column: "WidgetZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_WishList_WishList_UserId",
                table: "WishList",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WishList_WishListItem_ProductId",
                table: "WishListItem",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_WishList_WishListItem_WishListId",
                table: "WishListItem",
                column: "WishListId");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItem_Product_ProductId",
                table: "CartItem",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartItem_User_CustomerId",
                table: "CartItem",
                column: "CustomerId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartRuleProduct_Product_ProductId",
                table: "CartRuleProduct",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CartRuleUsage_User_UserId",
                table: "CartRuleUsage",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Checkout_User_CreatedById",
                table: "Checkout",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Checkout_User_CustomerId",
                table: "Checkout",
                column: "CustomerId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CheckoutItem_Product_ProductId",
                table: "CheckoutItem",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Comment_User_UserId",
                table: "Comment",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ComparingProduct_Product_ProductId",
                table: "ComparingProduct",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ComparingProduct_User_UserId",
                table: "ComparingProduct",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerGroupUser_User_UserId",
                table: "CustomerGroupUser",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItem_User_CreatedById",
                table: "NewsItem",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItem_User_LatestUpdatedById",
                table: "NewsItem",
                column: "LatestUpdatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_User_CreatedById",
                table: "Order",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_User_CustomerId",
                table: "Order",
                column: "CustomerId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Order_User_LatestUpdatedById",
                table: "Order",
                column: "LatestUpdatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderHistory_User_CreatedById",
                table: "OrderHistory",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrderItem_Product_ProductId",
                table: "OrderItem",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Page_User_CreatedById",
                table: "Page",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Page_User_LatestUpdatedById",
                table: "Page",
                column: "LatestUpdatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Product_User_CreatedById",
                table: "Product",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Product_User_LatestUpdatedById",
                table: "Product",
                column: "LatestUpdatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductPriceHistory_User_CreatedById",
                table: "ProductPriceHistory",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reply_Review_ReviewId",
                table: "Reply",
                column: "ReviewId",
                principalTable: "Review",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Reply_User_UserId",
                table: "Reply",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Review_User_UserId",
                table: "Review",
                column: "UserId",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipment_User_CreatedById",
                table: "Shipment",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_StockHistory_User_CreatedById",
                table: "StockHistory",
                column: "CreatedById",
                principalTable: "User",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_User_UserAddress_DefaultBillingAddressId",
                table: "User",
                column: "DefaultBillingAddressId",
                principalTable: "UserAddress",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_User_UserAddress_DefaultShippingAddressId",
                table: "User",
                column: "DefaultShippingAddressId",
                principalTable: "UserAddress",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Address_Country_CountryId",
                table: "Address");

            migrationBuilder.DropForeignKey(
                name: "FK_StateOrProvince_Country_CountryId",
                table: "StateOrProvince");

            migrationBuilder.DropForeignKey(
                name: "FK_Address_District_DistrictId",
                table: "Address");

            migrationBuilder.DropForeignKey(
                name: "FK_Address_StateOrProvince_StateOrProvinceId",
                table: "Address");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAddress_User_UserId",
                table: "UserAddress");

            migrationBuilder.DropTable(
                name: "Activity");

            migrationBuilder.DropTable(
                name: "AppSetting");

            migrationBuilder.DropTable(
                name: "CartItem");

            migrationBuilder.DropTable(
                name: "CartRuleCategory");

            migrationBuilder.DropTable(
                name: "CartRuleCustomerGroup");

            migrationBuilder.DropTable(
                name: "CartRuleProduct");

            migrationBuilder.DropTable(
                name: "CartRuleUsage");

            migrationBuilder.DropTable(
                name: "CatalogRuleCustomerGroup");

            migrationBuilder.DropTable(
                name: "CheckoutItem");

            migrationBuilder.DropTable(
                name: "Comment");

            migrationBuilder.DropTable(
                name: "ComparingProduct");

            migrationBuilder.DropTable(
                name: "Contact");

            migrationBuilder.DropTable(
                name: "CustomerGroupUser");

            migrationBuilder.DropTable(
                name: "LocalizedContentProperty");

            migrationBuilder.DropTable(
                name: "MenuItem");

            migrationBuilder.DropTable(
                name: "NewsItemCategory");

            migrationBuilder.DropTable(
                name: "OrderHistory");

            migrationBuilder.DropTable(
                name: "OrderItem");

            migrationBuilder.DropTable(
                name: "Page");

            migrationBuilder.DropTable(
                name: "Payment");

            migrationBuilder.DropTable(
                name: "PaymentProvider");

            migrationBuilder.DropTable(
                name: "PriceAndDestination");

            migrationBuilder.DropTable(
                name: "ProductAttributeValue");

            migrationBuilder.DropTable(
                name: "ProductBackInStockSubscription");

            migrationBuilder.DropTable(
                name: "ProductCategory");

            migrationBuilder.DropTable(
                name: "ProductLink");

            migrationBuilder.DropTable(
                name: "ProductMedia");

            migrationBuilder.DropTable(
                name: "ProductOptionCombination");

            migrationBuilder.DropTable(
                name: "ProductOptionValue");

            migrationBuilder.DropTable(
                name: "ProductPriceHistory");

            migrationBuilder.DropTable(
                name: "ProductTemplateProductAttribute");

            migrationBuilder.DropTable(
                name: "Query");

            migrationBuilder.DropTable(
                name: "RecentlyViewedProduct");

            migrationBuilder.DropTable(
                name: "Reply");

            migrationBuilder.DropTable(
                name: "Resource");

            migrationBuilder.DropTable(
                name: "RoleClaim");

            migrationBuilder.DropTable(
                name: "ShipmentItem");

            migrationBuilder.DropTable(
                name: "ShippingProvider");

            migrationBuilder.DropTable(
                name: "Stock");

            migrationBuilder.DropTable(
                name: "StockHistory");

            migrationBuilder.DropTable(
                name: "TaxRate");

            migrationBuilder.DropTable(
                name: "UserClaim");

            migrationBuilder.DropTable(
                name: "UserLogin");

            migrationBuilder.DropTable(
                name: "UserRole");

            migrationBuilder.DropTable(
                name: "UserToken");

            migrationBuilder.DropTable(
                name: "WidgetInstance");

            migrationBuilder.DropTable(
                name: "WishListItem");

            migrationBuilder.DropTable(
                name: "ActivityType");

            migrationBuilder.DropTable(
                name: "Coupon");

            migrationBuilder.DropTable(
                name: "CatalogRule");

            migrationBuilder.DropTable(
                name: "Checkout");

            migrationBuilder.DropTable(
                name: "ContactArea");

            migrationBuilder.DropTable(
                name: "CustomerGroup");

            migrationBuilder.DropTable(
                name: "Entity");

            migrationBuilder.DropTable(
                name: "Menu");

            migrationBuilder.DropTable(
                name: "NewsCategory");

            migrationBuilder.DropTable(
                name: "NewsItem");

            migrationBuilder.DropTable(
                name: "Category");

            migrationBuilder.DropTable(
                name: "ProductOption");

            migrationBuilder.DropTable(
                name: "ProductAttribute");

            migrationBuilder.DropTable(
                name: "ProductTemplate");

            migrationBuilder.DropTable(
                name: "Review");

            migrationBuilder.DropTable(
                name: "Culture");

            migrationBuilder.DropTable(
                name: "Shipment");

            migrationBuilder.DropTable(
                name: "Role");

            migrationBuilder.DropTable(
                name: "WidgetZone");

            migrationBuilder.DropTable(
                name: "Widget");

            migrationBuilder.DropTable(
                name: "Product");

            migrationBuilder.DropTable(
                name: "WishList");

            migrationBuilder.DropTable(
                name: "CartRule");

            migrationBuilder.DropTable(
                name: "EntityType");

            migrationBuilder.DropTable(
                name: "ProductAttributeGroup");

            migrationBuilder.DropTable(
                name: "Order");

            migrationBuilder.DropTable(
                name: "Warehouse");

            migrationBuilder.DropTable(
                name: "Brand");

            migrationBuilder.DropTable(
                name: "Media");

            migrationBuilder.DropTable(
                name: "TaxClass");

            migrationBuilder.DropTable(
                name: "OrderAddress");

            migrationBuilder.DropTable(
                name: "Country");

            migrationBuilder.DropTable(
                name: "District");

            migrationBuilder.DropTable(
                name: "StateOrProvince");

            migrationBuilder.DropTable(
                name: "User");

            migrationBuilder.DropTable(
                name: "UserAddress");

            migrationBuilder.DropTable(
                name: "Vendor");

            migrationBuilder.DropTable(
                name: "Address");
        }
    }
}

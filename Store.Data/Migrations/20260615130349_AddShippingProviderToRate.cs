using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShippingProviderToRate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ShippingProviderId",
                table: "PriceAndDestination",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceAndDestination_ShippingProviderId",
                table: "PriceAndDestination",
                column: "ShippingProviderId");

            migrationBuilder.AddForeignKey(
                name: "FK_PriceAndDestination_ShippingProvider_ShippingProviderId",
                table: "PriceAndDestination",
                column: "ShippingProviderId",
                principalTable: "ShippingProvider",
                principalColumn: "Id");

            // Seed the standard providers so checkout works without first visiting the admin page.
            // The two carriers (Aramex, Jordan Post) are enabled and price from their own table-rate
            // rows; the legacy Free / generic Table Rate providers are present but disabled. Idempotent
            // so it is safe alongside the admin controller's lazy seeding.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM [ShippingProvider] WHERE [Id] = N'Aramex')
    INSERT INTO [ShippingProvider] ([Id],[Name],[IsEnabled],[ToAllShippingEnabledCountries],[ToAllShippingEnabledStatesOrProvinces])
    VALUES (N'Aramex', N'Aramex', 1, 0, 0);
IF NOT EXISTS (SELECT 1 FROM [ShippingProvider] WHERE [Id] = N'JordanPost')
    INSERT INTO [ShippingProvider] ([Id],[Name],[IsEnabled],[ToAllShippingEnabledCountries],[ToAllShippingEnabledStatesOrProvinces])
    VALUES (N'JordanPost', N'Jordan Post', 1, 0, 0);
IF NOT EXISTS (SELECT 1 FROM [ShippingProvider] WHERE [Id] = N'Free')
    INSERT INTO [ShippingProvider] ([Id],[Name],[IsEnabled],[ToAllShippingEnabledCountries],[ToAllShippingEnabledStatesOrProvinces],[AdditionalSettings])
    VALUES (N'Free', N'Free Shipping', 0, 0, 0, N'{""MinimumOrderAmount"":0}');
IF NOT EXISTS (SELECT 1 FROM [ShippingProvider] WHERE [Id] = N'TableRate')
    INSERT INTO [ShippingProvider] ([Id],[Name],[IsEnabled],[ToAllShippingEnabledCountries],[ToAllShippingEnabledStatesOrProvinces])
    VALUES (N'TableRate', N'Table Rate', 0, 0, 0);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PriceAndDestination_ShippingProvider_ShippingProviderId",
                table: "PriceAndDestination");

            migrationBuilder.DropIndex(
                name: "IX_PriceAndDestination_ShippingProviderId",
                table: "PriceAndDestination");

            migrationBuilder.DropColumn(
                name: "ShippingProviderId",
                table: "PriceAndDestination");
        }
    }
}

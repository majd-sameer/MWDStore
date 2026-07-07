using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSignature",
                table: "Product",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SignatureSortOrder",
                table: "Product",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Catalog_Product_Signature",
                table: "Product",
                columns: new[] { "IsSignature", "SignatureSortOrder" },
                filter: "[IsSignature] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Catalog_Product_Signature",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "IsSignature",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "SignatureSortOrder",
                table: "Product");
        }
    }
}

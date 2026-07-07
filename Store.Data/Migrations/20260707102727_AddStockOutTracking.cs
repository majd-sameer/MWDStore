using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddStockOutTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "StockHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PerformedById",
                table: "StockHistory",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Reason",
                table: "StockHistory",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientOrRef",
                table: "StockHistory",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_StockHistory_PerformedById",
                table: "StockHistory",
                column: "PerformedById");

            migrationBuilder.CreateIndex(
                name: "IX_Inventory_StockHistory_Reason",
                table: "StockHistory",
                column: "Reason");

            migrationBuilder.AddForeignKey(
                name: "FK_StockHistory_User_PerformedById",
                table: "StockHistory",
                column: "PerformedById",
                principalTable: "User",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockHistory_User_PerformedById",
                table: "StockHistory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_StockHistory_PerformedById",
                table: "StockHistory");

            migrationBuilder.DropIndex(
                name: "IX_Inventory_StockHistory_Reason",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "PerformedById",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "StockHistory");

            migrationBuilder.DropColumn(
                name: "RecipientOrRef",
                table: "StockHistory");
        }
    }
}

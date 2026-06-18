using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderTrackingNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "Order",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                table: "Order",
                type: "nvarchar(6)",
                maxLength: 6,
                nullable: true);

            // Backfill existing master orders with distinct 6-digit codes (sequential from 100001 — well
            // clear of the random codes new orders get, which are uniqueness-checked at creation anyway).
            migrationBuilder.Sql(@"
                WITH numbered AS (
                    SELECT Id, ROW_NUMBER() OVER (ORDER BY Id) AS rn
                    FROM [Order]
                    WHERE ParentId IS NULL AND TrackingNumber IS NULL
                )
                UPDATE o
                SET o.TrackingNumber = CAST(100000 + numbered.rn AS nvarchar(6))
                FROM [Order] o
                INNER JOIN numbered ON o.Id = numbered.Id;");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Order_TrackingNumber",
                table: "Order",
                column: "TrackingNumber",
                unique: true,
                filter: "[TrackingNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Order_TrackingNumber",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "Order");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                table: "Order");
        }
    }
}

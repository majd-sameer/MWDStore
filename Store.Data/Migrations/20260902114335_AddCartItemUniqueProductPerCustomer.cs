using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCartItemUniqueProductPerCustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nothing stopped duplicate (CustomerId, ProductId) rows before this index, and
            // AddToCartAsync/ReturnItemsToCartAsync only ever touched the first of them — so any
            // existing pair is a bag line the shopper cannot see the whole of. Fold each set into its
            // oldest row (summing what the shopper actually chose) before the index makes it a rule.
            migrationBuilder.Sql(@"
UPDATE c
SET    c.Quantity = agg.TotalQuantity
FROM   CartItem c
JOIN  (SELECT CustomerId, ProductId, MIN(Id) AS KeepId, SUM(Quantity) AS TotalQuantity
       FROM   CartItem
       GROUP  BY CustomerId, ProductId
       HAVING COUNT(*) > 1) agg ON c.Id = agg.KeepId;");

            migrationBuilder.Sql(@"
DELETE c
FROM   CartItem c
JOIN  (SELECT CustomerId, ProductId, MIN(Id) AS KeepId
       FROM   CartItem
       GROUP  BY CustomerId, ProductId
       HAVING COUNT(*) > 1) agg
  ON   c.CustomerId = agg.CustomerId AND c.ProductId = agg.ProductId AND c.Id <> agg.KeepId;");

            migrationBuilder.CreateIndex(
                name: "UX_ShoppingCart_CartItem_Customer_Product",
                table: "CartItem",
                columns: new[] { "CustomerId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ShoppingCart_CartItem_Customer_Product",
                table: "CartItem");
        }
    }
}

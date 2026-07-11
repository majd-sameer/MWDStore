using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNewsEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertCtaUrl",
                table: "NewsItem",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AlertExpiresOn",
                table: "NewsItem",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ProductId",
                table: "NewsItem",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_News_NewsItem_ProductId",
                table: "NewsItem",
                column: "ProductId");

            migrationBuilder.AddForeignKey(
                name: "FK_NewsItem_Product_ProductId",
                table: "NewsItem",
                column: "ProductId",
                principalTable: "Product",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_NewsItem_Product_ProductId",
                table: "NewsItem");

            migrationBuilder.DropIndex(
                name: "IX_News_NewsItem_ProductId",
                table: "NewsItem");

            migrationBuilder.DropColumn(
                name: "AlertCtaUrl",
                table: "NewsItem");

            migrationBuilder.DropColumn(
                name: "AlertExpiresOn",
                table: "NewsItem");

            migrationBuilder.DropColumn(
                name: "ProductId",
                table: "NewsItem");
        }
    }
}

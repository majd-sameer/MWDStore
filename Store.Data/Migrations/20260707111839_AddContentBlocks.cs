using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddContentBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContentBlock",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PageKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SectionKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    BlockKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MediumId = table.Column<long>(type: "bigint", nullable: true),
                    LinkUrl = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentBlock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContentBlock_Media_MediumId",
                        column: x => x.MediumId,
                        principalTable: "Media",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cms_ContentBlock_PageKey",
                table: "ContentBlock",
                column: "PageKey");

            migrationBuilder.CreateIndex(
                name: "IX_ContentBlock_MediumId",
                table: "ContentBlock",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "UX_Cms_ContentBlock_Key",
                table: "ContentBlock",
                columns: new[] { "PageKey", "SectionKey", "BlockKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContentBlock");
        }
    }
}

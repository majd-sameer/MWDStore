using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalizedEntityKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EntityKey",
                table: "LocalizedContentProperty",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localization_LocalizedContentProperty_EntityKey",
                table: "LocalizedContentProperty",
                columns: new[] { "EntityType", "EntityKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Localization_LocalizedContentProperty_EntityKey",
                table: "LocalizedContentProperty");

            migrationBuilder.DropColumn(
                name: "EntityKey",
                table: "LocalizedContentProperty");
        }
    }
}

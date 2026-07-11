using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalizeOptionAttributeNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ProductOption",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "ProductAttribute",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            // Backfill: NameEn := the existing en-US LocalizedContentProperty overlay row (pattern (a)
            // from the design doc). Neither ProductOption nor ProductAttribute is one of the 72
            // Arabic-scrub entity types, so there is no "arabic" overlay and no safety-net/72-rule pass
            // needed here (only Product carries that wrinkle — see Migration 4).
            migrationBuilder.Sql(@"
UPDATE o SET o.NameEn = l.[Value]
FROM [ProductOption] o
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'ProductOption' AND l.EntityId = o.Id
  AND l.CultureId  = 'en-US'         AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE a SET a.NameEn = l.[Value]
FROM [ProductAttribute] a
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'ProductAttribute' AND l.EntityId = a.Id
  AND l.CultureId  = 'en-US'            AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ProductOption");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "ProductAttribute");
        }
    }
}

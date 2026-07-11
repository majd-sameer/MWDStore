using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalizeBrandCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "Category",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescriptionEn",
                table: "Category",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaKeywordsEn",
                table: "Category",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitleEn",
                table: "Category",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Category",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "Brand",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Brand",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            // Backfill: <Field>En := the existing en-US LocalizedContentProperty overlay row (pattern
            // (a) from the design doc). Neither Brand nor Category is one of the 72 Arabic-scrub
            // entity types, so there is no "arabic" overlay and no safety-net/72-rule pass needed here
            // (only Product carries that wrinkle — see Migration 4).
            migrationBuilder.Sql(@"
UPDATE c SET c.NameEn = l.[Value]
FROM [Category] c
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Category' AND l.EntityId = c.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE c SET c.DescriptionEn = l.[Value]
FROM [Category] c
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Category' AND l.EntityId = c.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'Description'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE c SET c.MetaTitleEn = l.[Value]
FROM [Category] c
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Category' AND l.EntityId = c.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'MetaTitle'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE c SET c.MetaKeywordsEn = l.[Value]
FROM [Category] c
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Category' AND l.EntityId = c.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'MetaKeywords'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE c SET c.MetaDescriptionEn = l.[Value]
FROM [Category] c
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Category' AND l.EntityId = c.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'MetaDescription'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE b SET b.NameEn = l.[Value]
FROM [Brand] b
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Brand' AND l.EntityId = b.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE b SET b.DescriptionEn = l.[Value]
FROM [Brand] b
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Brand' AND l.EntityId = b.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'Description'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "MetaDescriptionEn",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "MetaKeywordsEn",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "MetaTitleEn",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Category");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "Brand");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Brand");
        }
    }
}

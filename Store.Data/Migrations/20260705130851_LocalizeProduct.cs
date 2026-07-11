using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalizeProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "Product",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescriptionEn",
                table: "Product",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaKeywordsEn",
                table: "Product",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitleEn",
                table: "Product",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Product",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescriptionEn",
                table: "Product",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            // ---------------------------------------------------------------------------------------
            // Backfill (design doc, Decision 3). Product is the ONLY entity carrying the 72-product
            // "arabic" scrub wrinkle, so it runs all three passes IN THIS ORDER, per property:
            //   (a) <Field>En := the 'en-US' overlay row.
            //   (b) SAFETY NET: for a scrub product that has an 'arabic' overlay for this property but
            //       NO 'en-US' row, its base column still holds the old English/mixed text that pass (c)
            //       is about to overwrite — preserve it into <Field>En first so no English is lost.
            //       (Verification V3 predicts this fires 0 times; the guard is defence-in-depth.)
            //   (c) THE 72-PRODUCT RULE: base/Ar column := the 'arabic' overlay (COALESCE via JOIN — only
            //       rows that HAVE an arabic overlay are touched; every other product keeps its base).
            // Pass (a) covers all six localized properties; (b)+(c) cover only the properties that carry
            // 'arabic' rows (Name, ShortDescription, Description). Overlay rows are KEPT this release
            // (rollback window); a next-release cleanup migration deletes them.
            // ---------------------------------------------------------------------------------------

            // ----- Name: (a) -> (b) -> (c) -----
            migrationBuilder.Sql(@"
UPDATE p SET p.NameEn = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US'   AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
            migrationBuilder.Sql(@"
UPDATE p SET p.NameEn = p.Name
FROM [Product] p
WHERE p.NameEn IS NULL
  AND EXISTS (SELECT 1 FROM [LocalizedContentProperty] a
              WHERE a.EntityType = 'Product' AND a.EntityId = p.Id
                AND a.CultureId = 'arabic' AND a.ProperyName = 'Name'
                AND a.[Value] IS NOT NULL AND LTRIM(RTRIM(a.[Value])) <> '');
");
            migrationBuilder.Sql(@"
UPDATE p SET p.Name = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'arabic'  AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            // ----- ShortDescription: (a) -> (b) -> (c) -----
            migrationBuilder.Sql(@"
UPDATE p SET p.ShortDescriptionEn = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US'   AND l.ProperyName = 'ShortDescription'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
            migrationBuilder.Sql(@"
UPDATE p SET p.ShortDescriptionEn = p.ShortDescription
FROM [Product] p
WHERE p.ShortDescriptionEn IS NULL
  AND EXISTS (SELECT 1 FROM [LocalizedContentProperty] a
              WHERE a.EntityType = 'Product' AND a.EntityId = p.Id
                AND a.CultureId = 'arabic' AND a.ProperyName = 'ShortDescription'
                AND a.[Value] IS NOT NULL AND LTRIM(RTRIM(a.[Value])) <> '');
");
            migrationBuilder.Sql(@"
UPDATE p SET p.ShortDescription = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'arabic'  AND l.ProperyName = 'ShortDescription'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            // ----- Description: (a) -> (b) -> (c) -----
            migrationBuilder.Sql(@"
UPDATE p SET p.DescriptionEn = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US'   AND l.ProperyName = 'Description'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
            migrationBuilder.Sql(@"
UPDATE p SET p.DescriptionEn = p.Description
FROM [Product] p
WHERE p.DescriptionEn IS NULL
  AND EXISTS (SELECT 1 FROM [LocalizedContentProperty] a
              WHERE a.EntityType = 'Product' AND a.EntityId = p.Id
                AND a.CultureId = 'arabic' AND a.ProperyName = 'Description'
                AND a.[Value] IS NOT NULL AND LTRIM(RTRIM(a.[Value])) <> '');
");
            migrationBuilder.Sql(@"
UPDATE p SET p.Description = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'arabic'  AND l.ProperyName = 'Description'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            // ----- Meta* : pass (a) only (no 'arabic' scrub rows for these) -----
            migrationBuilder.Sql(@"
UPDATE p SET p.MetaTitleEn = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US'   AND l.ProperyName = 'MetaTitle'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
            migrationBuilder.Sql(@"
UPDATE p SET p.MetaKeywordsEn = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US'   AND l.ProperyName = 'MetaKeywords'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
            migrationBuilder.Sql(@"
UPDATE p SET p.MetaDescriptionEn = l.[Value]
FROM [Product] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Product' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US'   AND l.ProperyName = 'MetaDescription'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "MetaDescriptionEn",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "MetaKeywordsEn",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "MetaTitleEn",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Product");

            migrationBuilder.DropColumn(
                name: "ShortDescriptionEn",
                table: "Product");
        }
    }
}

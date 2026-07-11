using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Store.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalizePageNewsContentBlock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BodyEn",
                table: "Page",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescriptionEn",
                table: "Page",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaKeywordsEn",
                table: "Page",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitleEn",
                table: "Page",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "Page",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FullContentEn",
                table: "NewsItem",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                table: "NewsItem",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortContentEn",
                table: "NewsItem",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkTextEn",
                table: "ContentBlock",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextEn",
                table: "ContentBlock",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                table: "ContentBlock",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Backfill: <Field>En := the existing en-US LocalizedContentProperty overlay row (pattern
            // (a) from the design doc). None of Page/NewsItem/ContentBlock is one of the 72
            // Arabic-scrub entity types, so there is no "arabic" overlay and no safety-net/72-rule
            // pass needed here (only Product carries that wrinkle — see Migration 4).
            migrationBuilder.Sql(@"
UPDATE p SET p.NameEn = l.[Value]
FROM [Page] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Page' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE p SET p.BodyEn = l.[Value]
FROM [Page] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Page' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'Body'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE p SET p.MetaTitleEn = l.[Value]
FROM [Page] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Page' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'MetaTitle'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE p SET p.MetaKeywordsEn = l.[Value]
FROM [Page] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Page' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'MetaKeywords'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE p SET p.MetaDescriptionEn = l.[Value]
FROM [Page] p
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'Page' AND l.EntityId = p.Id
  AND l.CultureId  = 'en-US' AND l.ProperyName = 'MetaDescription'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE n SET n.NameEn = l.[Value]
FROM [NewsItem] n
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'NewsItem' AND l.EntityId = n.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'Name'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE n SET n.ShortContentEn = l.[Value]
FROM [NewsItem] n
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'NewsItem' AND l.EntityId = n.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'ShortContent'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE n SET n.FullContentEn = l.[Value]
FROM [NewsItem] n
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'NewsItem' AND l.EntityId = n.Id
  AND l.CultureId  = 'en-US'    AND l.ProperyName = 'FullContent'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE b SET b.TitleEn = l.[Value]
FROM [ContentBlock] b
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'ContentBlock' AND l.EntityId = b.Id
  AND l.CultureId  = 'en-US'        AND l.ProperyName = 'Title'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE b SET b.TextEn = l.[Value]
FROM [ContentBlock] b
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'ContentBlock' AND l.EntityId = b.Id
  AND l.CultureId  = 'en-US'        AND l.ProperyName = 'Text'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");

            migrationBuilder.Sql(@"
UPDATE b SET b.LinkTextEn = l.[Value]
FROM [ContentBlock] b
JOIN [LocalizedContentProperty] l
  ON  l.EntityType = 'ContentBlock' AND l.EntityId = b.Id
  AND l.CultureId  = 'en-US'        AND l.ProperyName = 'LinkText'
WHERE l.[Value] IS NOT NULL AND LTRIM(RTRIM(l.[Value])) <> '';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BodyEn",
                table: "Page");

            migrationBuilder.DropColumn(
                name: "MetaDescriptionEn",
                table: "Page");

            migrationBuilder.DropColumn(
                name: "MetaKeywordsEn",
                table: "Page");

            migrationBuilder.DropColumn(
                name: "MetaTitleEn",
                table: "Page");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "Page");

            migrationBuilder.DropColumn(
                name: "FullContentEn",
                table: "NewsItem");

            migrationBuilder.DropColumn(
                name: "NameEn",
                table: "NewsItem");

            migrationBuilder.DropColumn(
                name: "ShortContentEn",
                table: "NewsItem");

            migrationBuilder.DropColumn(
                name: "LinkTextEn",
                table: "ContentBlock");

            migrationBuilder.DropColumn(
                name: "TextEn",
                table: "ContentBlock");

            migrationBuilder.DropColumn(
                name: "TitleEn",
                table: "ContentBlock");
        }
    }
}

/* =============================================================================
   12_localize_governorates.sql — bilingual names for the 12 Jordan governorates.

   11_seed_jordan.sql inserted the governorates with ENGLISH base names, but the
   platform convention (see the bilingual rollout) is:
     - base column  StateOrProvince.Name  = Arabic
     - English      LocalizedContentProperty overlay (CultureId = 'en-US'),
       applied by /api/locations when the request asks for English.

   This script flips the base names to Arabic and upserts the en-US overlays.
   Idempotent and additive — safe to re-run. Run against the MyStore database:

       sqlcmd -S localhost -d MyStore -E -i 12_localize_governorates.sql
   ============================================================================= */
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- 0) The overlay culture row (FK target), created on demand like the app does.
IF NOT EXISTS (SELECT 1 FROM Culture WHERE Id = 'en-US')
BEGIN
    INSERT Culture (Id, Name) VALUES ('en-US', N'English');
    PRINT '0) Culture [en-US] inserted.';
END
ELSE PRINT '0) Culture [en-US] already present.';

-- 1) Name table: ISO 3166-2:JO code -> Arabic base + English overlay.
DECLARE @names TABLE (Code varchar(4) PRIMARY KEY, NameAr nvarchar(450), NameEn nvarchar(450));
INSERT @names (Code, NameAr, NameEn) VALUES
    ('AM', N'عمّان',    N'Amman'),
    ('IR', N'إربد',     N'Irbid'),
    ('AZ', N'الزرقاء',  N'Zarqa'),
    ('BA', N'البلقاء',  N'Al-Balqa'),
    ('MD', N'مادبا',    N'Madaba'),
    ('MA', N'المفرق',   N'Mafraq'),
    ('JA', N'جرش',      N'Jerash'),
    ('AJ', N'عجلون',    N'Ajloun'),
    ('KA', N'الكرك',    N'Karak'),
    ('AT', N'الطفيلة',  N'Tafilah'),
    ('MN', N'معان',     N'Ma''an'),
    ('AQ', N'العقبة',   N'Aqaba');

-- 1b) Backfill missing ISO codes: some rows were created through the admin UI
--     without a code (and with the languages inverted: English base + Arabic
--     en-US overlay). Match those by either name so steps 2/3 can fix them.
UPDATE s SET s.Code = n.Code
FROM StateOrProvince s
JOIN @names n ON (s.Name = n.NameEn OR s.Name = n.NameAr)
WHERE s.CountryId = 'JO' AND s.Code IS NULL;
PRINT '1b) ISO codes backfilled: ' + CAST(@@ROWCOUNT AS varchar(12)) + ' row(s).';

-- 2) Base names -> Arabic.
UPDATE s SET s.Name = n.NameAr
FROM StateOrProvince s
JOIN @names n ON n.Code = s.Code
WHERE s.CountryId = 'JO' AND s.Name <> n.NameAr;
PRINT '2) Base names switched to Arabic: ' + CAST(@@ROWCOUNT AS varchar(12)) + ' row(s).';

-- 3) Upsert the en-US Name overlays.
UPDATE p SET p.Value = n.NameEn
FROM LocalizedContentProperty p
JOIN StateOrProvince s ON s.Id = p.EntityId
JOIN @names n ON n.Code = s.Code
WHERE s.CountryId = 'JO'
  AND p.EntityType = 'StateOrProvince' AND p.CultureId = 'en-US'
  AND p.ProperyName = 'Name' AND p.Value <> n.NameEn;
PRINT '3) English overlays updated: ' + CAST(@@ROWCOUNT AS varchar(12)) + ' row(s).';

INSERT LocalizedContentProperty (EntityId, EntityType, CultureId, ProperyName, Value)
SELECT s.Id, 'StateOrProvince', 'en-US', 'Name', n.NameEn
FROM StateOrProvince s
JOIN @names n ON n.Code = s.Code
WHERE s.CountryId = 'JO'
  AND NOT EXISTS (SELECT 1 FROM LocalizedContentProperty p
                  WHERE p.EntityType = 'StateOrProvince' AND p.EntityId = s.Id
                    AND p.CultureId = 'en-US' AND p.ProperyName = 'Name');
PRINT '3) English overlays inserted: ' + CAST(@@ROWCOUNT AS varchar(12)) + ' row(s).';

COMMIT;

-- 4) Verify.
SELECT s.Id, s.Code, s.Name AS NameAr, p.Value AS NameEn
FROM StateOrProvince s
LEFT JOIN LocalizedContentProperty p
       ON p.EntityType = 'StateOrProvince' AND p.EntityId = s.Id
      AND p.CultureId = 'en-US' AND p.ProperyName = 'Name'
WHERE s.CountryId = 'JO'
ORDER BY s.Code;

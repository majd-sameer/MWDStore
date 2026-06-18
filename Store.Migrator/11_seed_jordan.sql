/*  Seeds Jordan locations after 10_wipe_catalog_locations.sql:
      - Country 'JO' (billing/shipping/city/zip enabled, districts off)
      - the 12 governorates as StateOrProvince rows (Type='Governorate', ISO 3166-2:JO codes)
      - one "Main Warehouse" in Amman (Address + Warehouse) so stock-tracked products are orderable.
    Idempotent: every insert is guarded by an existence check.

        sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -v TargetDb="MyStore_MigrationTest" -i 11_seed_jordan.sql
        sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -v TargetDb="MyStore"               -i 11_seed_jordan.sql
*/
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
USE [$(TargetDb)];

IF DB_NAME() IN (N'SimplCommerce', N'master', N'model', N'msdb', N'tempdb')
BEGIN
    RAISERROR('Refusing to run: target resolved to [%s].', 16, 1, N'$(TargetDb)');
    SET NOEXEC ON;
END
PRINT '=== SEED JORDAN LOCATIONS IN [' + DB_NAME() + '] ===';

-------------------------------------------------------------------------------
-- 1) Country
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Country WHERE Id = 'JO')
BEGIN
    INSERT Country (Id, Name, Code3, IsBillingEnabled, IsShippingEnabled, IsCityEnabled, IsZipCodeEnabled, IsDistrictEnabled)
    VALUES ('JO', N'Jordan', 'JOR', 1, 1, 1, 1, 0);
    PRINT '1) Country [JO] inserted.';
END
ELSE PRINT '1) Country [JO] already present.';

-------------------------------------------------------------------------------
-- 2) The 12 governorates
-------------------------------------------------------------------------------
INSERT StateOrProvince (CountryId, Code, Name, Type)
SELECT 'JO', g.Code, g.Name, 'Governorate'
FROM (VALUES
    ('AM', N'Amman'),
    ('IR', N'Irbid'),
    ('AZ', N'Zarqa'),
    ('BA', N'Al-Balqa'),
    ('MD', N'Madaba'),
    ('MA', N'Mafraq'),
    ('JA', N'Jerash'),
    ('AJ', N'Ajloun'),
    ('KA', N'Karak'),
    ('AT', N'Tafilah'),
    ('MN', N'Ma''an'),
    ('AQ', N'Aqaba')
) AS g (Code, Name)
WHERE NOT EXISTS (SELECT 1 FROM StateOrProvince s WHERE s.CountryId = 'JO' AND s.Code = g.Code);
PRINT '2) Governorates inserted: ' + CAST(@@ROWCOUNT AS varchar(12)) + ' (of 12).';

-------------------------------------------------------------------------------
-- 3) Main Warehouse in Amman (Address + Warehouse)
-------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM Warehouse WHERE Name = N'Main Warehouse')
BEGIN
    DECLARE @ammanId bigint = (SELECT Id FROM StateOrProvince WHERE CountryId = 'JO' AND Code = 'AM');
    DECLARE @addressId bigint;

    INSERT Address (ContactName, AddressLine1, City, ZipCode, CountryId, StateOrProvinceId)
    VALUES (N'Main Warehouse', N'Amman', N'Amman', N'11118', 'JO', @ammanId);
    SET @addressId = SCOPE_IDENTITY();

    INSERT Warehouse (Name, AddressId) VALUES (N'Main Warehouse', @addressId);
    PRINT '3) Main Warehouse (Amman) inserted.';
END
ELSE PRINT '3) Main Warehouse already present.';

-------------------------------------------------------------------------------
-- 4) Report
-------------------------------------------------------------------------------
PRINT '=== JORDAN LOCATION REPORT ===';
SELECT c.Id AS country, c.Name,
       (SELECT COUNT(*) FROM StateOrProvince s WHERE s.CountryId = c.Id) AS governorates,
       (SELECT COUNT(*) FROM Warehouse) AS warehouses
FROM Country c WHERE c.Id = 'JO';
SELECT Id, Code, Name, Type FROM StateOrProvince WHERE CountryId = 'JO' ORDER BY Code;
SET NOEXEC OFF;

/*  Wipes MyStore down to identity: deletes every row from every table EXCEPT the keep-list
    (User, Role, RoleClaim, UserClaim, UserLogin, UserRole, UserToken, AppSetting + harmless
    lookups Culture, EntityType, ActivityType). Covers catalog, locations and transactional data.

    Strategy (same shape as 02_migrate.sql):
        0. NULL the kept->wiped FK columns on [User] (Default*AddressId -> Address, VendorId -> Vendor).
        1. Disable all FK constraints.
        2. DELETE from every non-kept table; DBCC CHECKIDENT RESEED 0 so new ids start at 1.
        3. Re-enable every FK WITH CHECK CHECK to validate (reports, does not abort).
        4. Row-count report (kept tables keep their rows, everything else must be 0).

    Parameterised target DB so it can be dry-run against the copy first, then for real:
        sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -v TargetDb="MyStore_MigrationTest" -i 10_wipe_catalog_locations.sql
        sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -v TargetDb="MyStore"               -i 10_wipe_catalog_locations.sql
*/
SET NOCOUNT ON;
-- Identity's User/Role tables carry filtered unique indexes; DML on them requires these ON.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
USE [$(TargetDb)];

IF DB_NAME() IN (N'SimplCommerce', N'master', N'model', N'msdb', N'tempdb')
BEGIN
    RAISERROR('Refusing to run: target resolved to [%s].', 16, 1, N'$(TargetDb)');
    SET NOEXEC ON;
END
PRINT '=== WIPE (keep identity + lookups) IN [' + DB_NAME() + '] ===';

DECLARE @keep TABLE (name sysname PRIMARY KEY);
INSERT @keep (name) VALUES
    ('User'), ('Role'), ('RoleClaim'), ('UserClaim'), ('UserLogin'), ('UserRole'), ('UserToken'),
    ('AppSetting'), ('Culture'), ('EntityType'), ('ActivityType'), ('__EFMigrationsHistory');

DECLARE @sql nvarchar(max), @stmt nvarchar(max);

-------------------------------------------------------------------------------
-- 0) Detach kept rows from tables about to be wiped
-------------------------------------------------------------------------------
UPDATE [User] SET DefaultShippingAddressId = NULL, DefaultBillingAddressId = NULL, VendorId = NULL
WHERE DefaultShippingAddressId IS NOT NULL OR DefaultBillingAddressId IS NOT NULL OR VendorId IS NOT NULL;
PRINT '0) [User] address/vendor references cleared (' + CAST(@@ROWCOUNT AS varchar(12)) + ' rows).';

-------------------------------------------------------------------------------
-- 1) Disable all FK constraints
-------------------------------------------------------------------------------
SET @sql = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
            + N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t;
EXEC sys.sp_executesql @sql;
PRINT '1) FK constraints disabled.';

-------------------------------------------------------------------------------
-- 2) Delete + reseed every non-kept table
-------------------------------------------------------------------------------
DECLARE @tbl sysname, @hasid bit, @wiped int = 0;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT t.name,
           CASE WHEN EXISTS (SELECT 1 FROM sys.identity_columns ic WHERE ic.object_id = t.object_id)
                THEN 1 ELSE 0 END
    FROM sys.tables t
    WHERE t.name NOT IN (SELECT name FROM @keep)
    ORDER BY t.name;
OPEN cur;
FETCH NEXT FROM cur INTO @tbl, @hasid;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @stmt = N'DELETE FROM ' + QUOTENAME(@tbl) + N';';
    IF @hasid = 1 SET @stmt = @stmt + N' DBCC CHECKIDENT (' + QUOTENAME(@tbl, '''') + N', RESEED, 0) WITH NO_INFOMSGS;';
    BEGIN TRY
        EXEC sys.sp_executesql @stmt;
        SET @wiped = @wiped + 1;
    END TRY
    BEGIN CATCH
        PRINT '   !! ERROR wiping [' + @tbl + ']: ' + ERROR_MESSAGE();
    END CATCH
    FETCH NEXT FROM cur INTO @tbl, @hasid;
END
CLOSE cur; DEALLOCATE cur;
PRINT '2) ' + CAST(@wiped AS varchar(12)) + ' tables wiped and reseeded.';

-------------------------------------------------------------------------------
-- 3) Re-enable + validate FK constraints (non-aborting)
-------------------------------------------------------------------------------
DECLARE @fk sysname, @aschema sysname, @ptbl sysname;
DECLARE @violations TABLE (constraint_name sysname, table_name sysname, message nvarchar(2048));
DECLARE fcur CURSOR LOCAL FAST_FORWARD FOR
    SELECT fk.name, OBJECT_SCHEMA_NAME(fk.parent_object_id), OBJECT_NAME(fk.parent_object_id)
    FROM sys.foreign_keys fk;
OPEN fcur;
FETCH NEXT FROM fcur INTO @fk, @aschema, @ptbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        SET @stmt = N'ALTER TABLE ' + QUOTENAME(@aschema) + N'.' + QUOTENAME(@ptbl)
                  + N' WITH CHECK CHECK CONSTRAINT ' + QUOTENAME(@fk) + N';';
        EXEC sys.sp_executesql @stmt;
    END TRY
    BEGIN CATCH
        INSERT @violations VALUES (@fk, @ptbl, ERROR_MESSAGE());
    END CATCH
    FETCH NEXT FROM fcur INTO @fk, @aschema, @ptbl;
END
CLOSE fcur; DEALLOCATE fcur;
PRINT '3) FK constraints re-enabled and validated.';

PRINT '=== REFERENTIAL INTEGRITY VIOLATIONS (expect none) ===';
SELECT constraint_name, table_name, message FROM @violations ORDER BY table_name, constraint_name;
SELECT COUNT(*) AS untrusted_fks_remaining FROM sys.foreign_keys WHERE is_not_trusted = 1;

-------------------------------------------------------------------------------
-- 4) Row-count report (kept tables retain rows; wiped tables must be 0)
-------------------------------------------------------------------------------
PRINT '=== ROW COUNTS AFTER WIPE ===';
SELECT t.name AS table_name, SUM(p.rows) AS rows_now,
       CASE WHEN k.name IS NOT NULL THEN 'KEPT' ELSE 'WIPED' END AS disposition,
       CASE WHEN k.name IS NULL AND SUM(p.rows) > 0 THEN '!! NOT EMPTY' ELSE 'OK' END AS status
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
LEFT JOIN @keep k ON k.name = t.name
GROUP BY t.name, k.name
ORDER BY CASE WHEN k.name IS NULL AND SUM(p.rows) > 0 THEN 0 ELSE 1 END, disposition, t.name;
SET NOEXEC OFF;

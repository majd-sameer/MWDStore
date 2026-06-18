/*  SimplCommerce -> MyStore data migration.
    Schemas are column-identical; only the table names differ (module prefix stripped:
    Core_User -> User, Catalog_Product -> Product, Orders_Order -> Order, ...).

    Strategy (handles the User<->UserAddress FK cycle and the self-referencing
    ParentId trees that make any single linear insert order impossible):
        1. Disable all FK constraints in the target.
        2. For each table: DELETE existing rows, then INSERT...SELECT every shared
           column from the source. IDENTITY_INSERT preserves primary keys so every
           foreign key value still lines up. ASP.NET Identity PasswordHash /
           SecurityStamp / ConcurrencyStamp copy verbatim => old logins keep working.
        3. Re-enable every FK WITH CHECK to validate referential integrity, reporting
           (not aborting on) any violation.
        4. Reconcile source vs target row counts.

    Parameterised target DB so it can be run against the copy first, then for real:
        sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -v TargetDb="MyStore_MigrationTest" -i 02_migrate.sql
        sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -v TargetDb="MyStore"               -i 02_migrate.sql
*/
SET NOCOUNT ON;
-- Identity's User/Role tables carry filtered unique indexes (UserNameIndex, RoleNameIndex);
-- any DML on them requires these ON. sqlcmd defaults QUOTED_IDENTIFIER OFF, so set it explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
USE [$(TargetDb)];

IF DB_NAME() = N'SimplCommerce'
BEGIN
    RAISERROR('Refusing to run: target resolved to the SOURCE database.', 16, 1);
    SET NOEXEC ON;
END
PRINT '=== MIGRATION INTO [' + DB_NAME() + '] FROM [SimplCommerce] ===';

DECLARE @sql nvarchar(max), @stmt nvarchar(max);
DECLARE @src sysname, @tgt sysname, @cols nvarchar(max), @hasid bit;

-------------------------------------------------------------------------------
-- 1) Disable all FK constraints
-------------------------------------------------------------------------------
SET @sql = N'';
SELECT @sql = @sql + N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(t.schema_id)) + N'.' + QUOTENAME(t.name)
            + N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables t
WHERE t.name <> '__EFMigrationsHistory';
EXEC sys.sp_executesql @sql;
PRINT '1) FK constraints disabled.';

-------------------------------------------------------------------------------
-- 2) Clear + load every mapped table
-------------------------------------------------------------------------------
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT s.TABLE_NAME,
           SUBSTRING(s.TABLE_NAME, CHARINDEX('_', s.TABLE_NAME) + 1, 200)
    FROM SimplCommerce.INFORMATION_SCHEMA.TABLES s
    WHERE s.TABLE_TYPE = 'BASE TABLE'
      AND s.TABLE_NAME <> '__EFMigrationsHistory'
      AND CHARINDEX('_', s.TABLE_NAME) > 0;
OPEN cur;
FETCH NEXT FROM cur INTO @src, @tgt;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @cols = NULL;
    SELECT @cols = STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(sc.COLUMN_NAME)), N', ')
                   WITHIN GROUP (ORDER BY sc.ORDINAL_POSITION)
    FROM SimplCommerce.INFORMATION_SCHEMA.COLUMNS sc
    WHERE sc.TABLE_NAME = @src
      AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS tc
                  WHERE tc.TABLE_NAME = @tgt AND tc.COLUMN_NAME = sc.COLUMN_NAME);

    SET @hasid = CASE WHEN EXISTS (SELECT 1 FROM sys.identity_columns ic
                                   WHERE ic.object_id = OBJECT_ID(QUOTENAME(@tgt))) THEN 1 ELSE 0 END;

    SET @stmt = N'DELETE FROM ' + QUOTENAME(@tgt) + N';' + CHAR(10);
    IF @hasid = 1 SET @stmt = @stmt + N'SET IDENTITY_INSERT ' + QUOTENAME(@tgt) + N' ON;' + CHAR(10);
    SET @stmt = @stmt + N'INSERT INTO ' + QUOTENAME(@tgt) + N' (' + @cols + N') SELECT ' + @cols
              + N' FROM SimplCommerce.dbo.' + QUOTENAME(@src) + N';' + CHAR(10);
    IF @hasid = 1 SET @stmt = @stmt + N'SET IDENTITY_INSERT ' + QUOTENAME(@tgt) + N' OFF;' + CHAR(10);

    BEGIN TRY
        EXEC sys.sp_executesql @stmt;
    END TRY
    BEGIN CATCH
        PRINT '   !! ERROR loading [' + @tgt + ']: ' + ERROR_MESSAGE();
    END CATCH

    FETCH NEXT FROM cur INTO @src, @tgt;
END
CLOSE cur; DEALLOCATE cur;
PRINT '2) Load complete.';

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
-- 4) Row-count reconciliation (source vs target)
-------------------------------------------------------------------------------
PRINT '=== ROW-COUNT RECONCILIATION ===';
;WITH s AS (
    SELECT SUBSTRING(t.name, CHARINDEX('_', t.name) + 1, 200) AS tgt, SUM(p.rows) AS src_rows
    FROM SimplCommerce.sys.tables t
    JOIN SimplCommerce.sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
    WHERE t.name <> '__EFMigrationsHistory' AND CHARINDEX('_', t.name) > 0
    GROUP BY SUBSTRING(t.name, CHARINDEX('_', t.name) + 1, 200)
),
d AS (
    SELECT t.name AS tgt, SUM(p.rows) AS tgt_rows
    FROM sys.tables t
    JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
    WHERE t.name <> '__EFMigrationsHistory'
    GROUP BY t.name
)
SELECT s.tgt AS table_name, s.src_rows, d.tgt_rows,
       CASE WHEN s.src_rows = d.tgt_rows THEN 'OK' ELSE 'MISMATCH' END AS status
FROM s JOIN d ON d.tgt = s.tgt
ORDER BY CASE WHEN s.src_rows = d.tgt_rows THEN 1 ELSE 0 END, s.src_rows DESC, s.tgt;

SELECT
    (SELECT COUNT(*) FROM SimplCommerce.sys.tables t
        WHERE t.name <> '__EFMigrationsHistory' AND CHARINDEX('_', t.name) > 0) AS source_tables,
    (SELECT COUNT(*) FROM @violations) AS ri_violations;
SET NOEXEC OFF;

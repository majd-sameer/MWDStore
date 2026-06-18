/*  Creates a full COPY of MyStore as [MyStore_MigrationTest] via COPY_ONLY backup + restore.
    Safe to re-run: drops and recreates the copy each time.
    Run:  sqlcmd -S MSALEH\SQL -U sa -P *** -C -b -i 00_create_copy.sql  */
SET NOCOUNT ON;
USE master;

DECLARE @bak     nvarchar(260) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQL\MSSQL\Backup\MyStore_migtest.bak';
DECLARE @dataDir nvarchar(260) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.SQL\MSSQL\DATA\';
DECLARE @mdf     nvarchar(260) = @dataDir + N'MyStore_MigrationTest.mdf';
DECLARE @ldf     nvarchar(260) = @dataDir + N'MyStore_MigrationTest_log.ldf';

BACKUP DATABASE [MyStore] TO DISK = @bak WITH COPY_ONLY, INIT, FORMAT;

IF DB_ID('MyStore_MigrationTest') IS NOT NULL
BEGIN
    ALTER DATABASE [MyStore_MigrationTest] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [MyStore_MigrationTest];
END

RESTORE DATABASE [MyStore_MigrationTest] FROM DISK = @bak
WITH MOVE 'MyStore'     TO @mdf,
     MOVE 'MyStore_log' TO @ldf,
     REPLACE, RECOVERY;

PRINT 'Copy [MyStore_MigrationTest] created.';

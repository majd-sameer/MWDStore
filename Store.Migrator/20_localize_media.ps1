<#
.SYNOPSIS
Downloads externally hosted product images (Media.FileName = absolute URL, as created by
CatalogSeeder) into Store.Api/user-content and repoints the Media rows at the local copies,
so the store no longer depends on e-shop.psd.gov.jo.

Idempotent: only rows still LIKE 'http%' are processed; files are named m{MediaId}{ext} so a
re-run never collides. Failed downloads keep their external URL (the media URL builders pass
absolute URLs through), are reported at the end, and are retried on the next run.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File Store.Migrator\20_localize_media.ps1
#>
param(
    [string]$Server = "MSALEH\SQL",
    [string]$Database = "MyStore",
    [string]$User = "sa",
    [string]$Password = "Test@1234",
    [string]$OutDir = (Join-Path $PSScriptRoot "..\Store.Api\user-content"),
    [int]$BatchSize = 10
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http # Windows PowerShell 5.1 doesn't load it by default
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$OutDir = [System.IO.Path]::GetFullPath($OutDir)
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir | Out-Null }

$conn = New-Object System.Data.SqlClient.SqlConnection(
    "Data Source=$Server;Initial Catalog=$Database;User ID=$User;Password=$Password;Encrypt=True;TrustServerCertificate=True")
$conn.Open()

# Media rows still pointing at an external host.
$media = New-Object System.Collections.ArrayList
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT Id, FileName FROM Media WHERE FileName LIKE 'http%' ORDER BY Id"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) { [void]$media.Add(@{ Id = $reader.GetInt64(0); Url = $reader.GetString(1) }) }
$reader.Close()
Write-Host "External media rows to localize: $($media.Count)"
if ($media.Count -eq 0) { $conn.Close(); return }

$update = $conn.CreateCommand()
$update.CommandText = "UPDATE Media SET FileName = @name, FileSize = @size WHERE Id = @id"
[void]$update.Parameters.Add("@name", [System.Data.SqlDbType]::NVarChar, 450)
[void]$update.Parameters.Add("@size", [System.Data.SqlDbType]::Int)
[void]$update.Parameters.Add("@id",   [System.Data.SqlDbType]::BigInt)

$client = New-Object System.Net.Http.HttpClient
$client.Timeout = [TimeSpan]::FromSeconds(60)

$ok = 0; $failed = New-Object System.Collections.ArrayList
for ($i = 0; $i -lt $media.Count; $i += $BatchSize) {
    $batch = $media[$i..([Math]::Min($i + $BatchSize, $media.Count) - 1)]
    $tasks = @($batch | ForEach-Object { $client.GetByteArrayAsync($_.Url) })
    try { [System.Threading.Tasks.Task]::WaitAll($tasks) } catch { } # per-task faults handled below

    for ($j = 0; $j -lt $batch.Count; $j++) {
        $item = $batch[$j]; $task = $tasks[$j]
        if ($task.Status -ne [System.Threading.Tasks.TaskStatus]::RanToCompletion) {
            [void]$failed.Add($item.Url)
            continue
        }
        $bytes = $task.Result
        $ext = [System.IO.Path]::GetExtension(([Uri]$item.Url).AbsolutePath)
        if (-not $ext) { $ext = ".jpg" }
        $name = "m$($item.Id)$ext"
        [System.IO.File]::WriteAllBytes((Join-Path $OutDir $name), $bytes)

        $update.Parameters["@name"].Value = $name
        $update.Parameters["@size"].Value = $bytes.Length
        $update.Parameters["@id"].Value = $item.Id
        [void]$update.ExecuteNonQuery()
        $ok++
    }
    if ((($i / $BatchSize) % 10) -eq 0) { Write-Host "  $([Math]::Min($i + $BatchSize, $media.Count)) / $($media.Count) processed..." }
}

$client.Dispose(); $conn.Close()
Write-Host "Done: $ok localized, $($failed.Count) failed (kept their external URL)."
$failed | ForEach-Object { Write-Host "  FAILED: $_" }

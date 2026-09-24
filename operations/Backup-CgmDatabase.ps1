param(
    [Parameter(Mandatory)][string]$ServerInstance,
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string]$BackupDirectory,
    [ValidateSet('FULL','DIFFERENTIAL','LOG')][string]$Type = 'FULL'
)
$ErrorActionPreference = 'Stop'
$resolvedDirectory = [System.IO.Path]::GetFullPath($BackupDirectory)
if (-not (Test-Path -LiteralPath $resolvedDirectory -PathType Container)) { throw "Backup directory does not exist: $resolvedDirectory" }
if ($Database -notmatch '^[A-Za-z0-9_-]+$') { throw 'Database name contains unsupported characters.' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$extension = if ($Type -eq 'LOG') { 'trn' } else { 'bak' }
$destination = Join-Path $resolvedDirectory "$Database-$Type-$stamp.$extension"
$escapedDestination = $destination.Replace("'", "''")
$escapedDatabase = $Database.Replace(']', ']]')
$modifier = switch ($Type) { 'DIFFERENTIAL' { ', DIFFERENTIAL' } 'LOG' { $null } default { ', COPY_ONLY' } }
$statement = if ($Type -eq 'LOG') {
    "BACKUP LOG [$escapedDatabase] TO DISK = N'$escapedDestination' WITH CHECKSUM, COMPRESSION, STATS = 10; RESTORE VERIFYONLY FROM DISK = N'$escapedDestination' WITH CHECKSUM;"
} else {
    "BACKUP DATABASE [$escapedDatabase] TO DISK = N'$escapedDestination' WITH CHECKSUM, COMPRESSION$modifier, STATS = 10; RESTORE VERIFYONLY FROM DISK = N'$escapedDestination' WITH CHECKSUM;"
}
sqlcmd -S $ServerInstance -b -Q $statement
if ($LASTEXITCODE -ne 0) { throw "SQL Server backup failed with exit code $LASTEXITCODE." }
Write-Output $destination

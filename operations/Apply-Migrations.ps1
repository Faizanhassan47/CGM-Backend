param(
    [Parameter(Mandatory)][string]$ServerInstance,
    [Parameter(Mandatory)][string]$Database,
    [Parameter(Mandatory)][string[]]$Script,
    [switch]$Approved
)
$ErrorActionPreference = 'Stop'
if (-not $Approved) { throw 'Migration execution requires the -Approved switch after backup and staging validation.' }
foreach ($file in $Script) {
    $resolved = (Resolve-Path -LiteralPath $file).Path
    sqlcmd -S $ServerInstance -d $Database -b -i $resolved
    if ($LASTEXITCODE -ne 0) { throw "Migration failed: $resolved" }
}

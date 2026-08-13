param(
    [Parameter(Mandatory)]
    [string]$BackupPath,

    [switch]$ConfirmRestore
)

$ErrorActionPreference = "Stop"
if (-not $ConfirmRestore) {
    throw "Restore is destructive. Re-run with -ConfirmRestore after verifying the target environment."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedBackup = (Resolve-Path -LiteralPath $BackupPath).Path
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "backups"))
$allowedPrefix = $allowedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedBackup.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Restore input must come from the repository backups directory mounted into PostgreSQL."
}

$relativeBackup = [System.IO.Path]::GetRelativePath($allowedRoot, $resolvedBackup).Replace('\', '/')
$containerPath = "/backups/$relativeBackup"
docker compose stop web
if ($LASTEXITCODE -ne 0) {
    throw "Unable to stop the Web service before restore."
}

docker compose exec -T postgres pg_restore `
    --username marine_insight `
    --dbname marine_insight `
    --clean `
    --if-exists `
    --no-owner `
    --no-privileges `
    $containerPath
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL restore failed. Web remains stopped for operator inspection."
}

docker compose exec -T postgres psql `
    --username marine_insight `
    --dbname marine_insight `
    --tuples-only `
    --command 'SELECT COUNT(*) FROM "__EFMigrationsHistory";'
if ($LASTEXITCODE -ne 0) {
    throw "Restore completed but migration-history verification failed. Web remains stopped."
}

docker compose start web
if ($LASTEXITCODE -ne 0) {
    throw "Restore verified, but the Web service could not be restarted."
}

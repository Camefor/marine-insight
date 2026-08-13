param(
    [string]$OutputDirectory = "backups"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "backups"))
$allowedPrefix = $allowedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ($resolvedOutput -ne $allowedRoot -and -not $resolvedOutput.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Backup output must remain under the repository backups directory."
}

[System.IO.Directory]::CreateDirectory($resolvedOutput) | Out-Null
$fileName = "marine-insight-{0}.dump" -f [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
$relativeOutput = [System.IO.Path]::GetRelativePath($allowedRoot, $resolvedOutput).Replace('\', '/')
$containerDirectory = if ($relativeOutput -eq ".") { "/backups" } else { "/backups/$relativeOutput" }
$containerPath = "$containerDirectory/$fileName"

docker compose exec -T postgres pg_dump `
    --username marine_insight `
    --dbname marine_insight `
    --format custom `
    --no-owner `
    --no-privileges `
    --file $containerPath
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL backup failed."
}

$backupPath = Join-Path $resolvedOutput $fileName
if (-not (Test-Path -LiteralPath $backupPath) -or (Get-Item -LiteralPath $backupPath).Length -eq 0) {
    throw "PostgreSQL backup did not produce a non-empty file."
}

Write-Output $backupPath

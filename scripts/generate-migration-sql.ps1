param(
    [string]$OutputPath = "deploy/postgresql-migrations.sql"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputPath))
$allowedRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "deploy"))
$allowedPrefix = $allowedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedOutput.StartsWith($allowedPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Migration SQL output must remain under the repository deploy directory."
}

$outputDirectory = Split-Path -Parent $resolvedOutput
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
$env:Database__Provider = "PostgreSql"
$env:ConnectionStrings__MarineInsight = "Host=localhost;Database=marine_insight;Username=marine_insight;Password=design-time"

dotnet ef migrations script --idempotent `
    --project (Join-Path $repositoryRoot "src/MarineInsight.Migrations.PostgreSql/MarineInsight.Migrations.PostgreSql.csproj") `
    --startup-project (Join-Path $repositoryRoot "src/MarineInsight.Web/MarineInsight.Web.csproj") `
    --context MarineInsightDbContext `
    --output $resolvedOutput
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL migration SQL generation failed."
}

Write-Output $resolvedOutput

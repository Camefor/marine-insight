[CmdletBinding()]
param(
    [switch]$Disable
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) "src/MarineInsight.Web/MarineInsight.Web.csproj"

if ($Disable) {
    dotnet user-secrets remove "TideProviders:WorldTides:ApiKey" --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove the WorldTides API key from User Secrets."
    }

    dotnet user-secrets set "TideProviders:WorldTides:Enabled" "false" --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to disable WorldTides in User Secrets."
    }

    Write-Output "WorldTides is disabled and its local API key has been removed."
    return
}

$secureKey = Read-Host "WorldTides API key" -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) {
        throw "WorldTides API key cannot be empty."
    }

    # Pipe JSON through stdin so the credential is not recorded in shell history or process arguments.
    @{
        "TideProviders:WorldTides:ApiKey" = $plainKey
        "TideProviders:WorldTides:Enabled" = "true"
    } | ConvertTo-Json -Compress | dotnet user-secrets set --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to store the WorldTides settings in User Secrets."
    }
}
finally {
    if ($keyPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
    }

    $plainKey = $null
}

Write-Output "WorldTides is enabled and its API key is stored in .NET User Secrets."

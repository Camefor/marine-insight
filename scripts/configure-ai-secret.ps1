[CmdletBinding()]
param(
    [switch]$Disable
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) "src/MarineInsight.Web/MarineInsight.Web.csproj"

if ($Disable) {
    dotnet user-secrets remove "AI:ApiKey" --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove the AI API key from User Secrets."
    }

    dotnet user-secrets set "AI:Enabled" "false" --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to disable AI explanation in User Secrets."
    }

    Write-Output "AI explanation is disabled and its local API key has been removed."
    return
}

$secureKey = Read-Host "AI API key" -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) {
        throw "AI API key cannot be empty."
    }

    # Pipe JSON through stdin so the credential is not recorded in shell history or process arguments.
    @{
        "AI:ApiKey" = $plainKey
        "AI:Enabled" = "true"
    } | ConvertTo-Json -Compress | dotnet user-secrets set --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to store the AI settings in User Secrets."
    }
}
finally {
    if ($keyPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
    }

    $plainKey = $null
}

Write-Output "AI explanation is enabled and its API key is stored in .NET User Secrets."

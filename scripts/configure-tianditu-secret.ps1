[CmdletBinding()]
param(
    [switch]$Disable
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) "src/MarineInsight.Web/MarineInsight.Web.csproj"

if ($Disable) {
    dotnet user-secrets remove "Map:Tianditu:Key" --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove the Tianditu key from User Secrets."
    }

    Write-Output "The Tianditu key has been removed; the map picker degrades to coordinate input."
    return
}

$secureKey = Read-Host "Tianditu browser key" -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) {
        throw "Tianditu key cannot be empty."
    }

    # Pipe JSON through stdin so the key is not recorded in shell history or process arguments.
    @{
        "Map:Tianditu:Key" = $plainKey
    } | ConvertTo-Json -Compress | dotnet user-secrets set --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to store the Tianditu key in User Secrets."
    }
}
finally {
    if ($keyPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
    }

    $plainKey = $null
}

Write-Output "The Tianditu key is stored in .NET User Secrets."

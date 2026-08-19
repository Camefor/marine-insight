[CmdletBinding()]
param(
    [switch]$Disable,
    [switch]$Server
)

$ErrorActionPreference = "Stop"
$projectPath = Join-Path (Split-Path -Parent $PSScriptRoot) "src/MarineInsight.Web/MarineInsight.Web.csproj"
$keyName = if ($Server) { "Map:Tianditu:ServerKey" } else { "Map:Tianditu:Key" }
$keyLabel = if ($Server) { "server-side key" } else { "browser key" }

if ($Disable) {
    dotnet user-secrets remove "Map:Tianditu:Key" --project $projectPath | Out-Null
    dotnet user-secrets remove "Map:Tianditu:ServerKey" --project $projectPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove the Tianditu keys from User Secrets."
    }

    Write-Output "The Tianditu keys have been removed; the map picker degrades to coordinate input."
    return
}

$secureKey = Read-Host "Tianditu $keyLabel" -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
try {
    $plainKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($plainKey)) {
        throw "Tianditu key cannot be empty."
    }

    # Pipe JSON through stdin so the key is not recorded in shell history or process arguments.
    @{
        $keyName = $plainKey
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

Write-Output "The Tianditu $keyLabel is stored in .NET User Secrets."

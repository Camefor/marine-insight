param(
    [string]$BaseUrl = "http://localhost"
)

$ErrorActionPreference = "Stop"
$baseUri = [Uri]$BaseUrl
if ($baseUri.Scheme -notin @("http", "https")) {
    throw "BaseUrl must use HTTP or HTTPS."
}

$live = Invoke-WebRequest -Uri ([Uri]::new($baseUri, "/health/live")) -UseBasicParsing
$ready = Invoke-WebRequest -Uri ([Uri]::new($baseUri, "/health/ready")) -UseBasicParsing
$dashboard = Invoke-WebRequest -Uri ([Uri]::new($baseUri, "/")) -UseBasicParsing
$login = Invoke-WebRequest -Uri ([Uri]::new($baseUri, "/account/login")) -UseBasicParsing

if ($live.StatusCode -ne 200 -or $ready.StatusCode -ne 200) {
    throw "Health checks did not return HTTP 200."
}
if ($dashboard.Content -notmatch "海况 Dashboard" -or $login.Content -notmatch "登录") {
    throw "Dashboard or login smoke content is missing."
}
if ($dashboard.Headers["X-Content-Type-Options"] -ne "nosniff") {
    throw "Reverse proxy security headers are missing."
}

Write-Output "Smoke checks passed for $($baseUri.AbsoluteUri)"

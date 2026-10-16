[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath
)

$ErrorActionPreference = 'Stop'
$package = Get-Item -LiteralPath $PackagePath -ErrorAction Stop
if ($package.PSIsContainer) {
    throw "Package path must identify a file: $PackagePath"
}

$sidecarPath = "$($package.FullName).sha256"
$temporaryPath = "$sidecarPath.$([Guid]::NewGuid().ToString('N')).tmp"
try {
    $hash = (Get-FileHash -LiteralPath $package.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$hash  $($package.Name)" | Set-Content -LiteralPath $temporaryPath -Encoding ascii
    Move-Item -LiteralPath $temporaryPath -Destination $sidecarPath -Force
}
finally {
    if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

Write-Output $sidecarPath

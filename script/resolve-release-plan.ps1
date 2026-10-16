[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion,

    [ValidateSet('PackageCatalog', 'ValidationMatrix')]
    [string]$View = 'PackageCatalog',

    [switch]$AsJson
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'release-package-catalog.ps1')

$result = switch ($View) {
    'PackageCatalog' {
        @(Get-ReleasePackageCatalog -ExpectedVersion $ExpectedVersion)
        break
    }
    'ValidationMatrix' {
        [pscustomobject]@{
            include = @(Get-ReleaseValidationMatrix -ExpectedVersion $ExpectedVersion)
        }
        break
    }
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 5 -Compress
}
else {
    $result
}

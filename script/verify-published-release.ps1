[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseJsonPath,

    [Parameter(Mandatory = $true)]
    [string]$AssetDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedTag
)

$ErrorActionPreference = 'Stop'

function Assert-ExactNameSet {
    param(
        [string[]]$Actual,
        [string[]]$Expected
    )

    $differences = @(Compare-Object -ReferenceObject @($Expected | Sort-Object) `
            -DifferenceObject @($Actual | Sort-Object) -CaseSensitive)
    if ($Actual.Count -ne $Expected.Count -or $differences.Count -ne 0) {
        $description = ($differences | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }) -join '; '
        throw "Published release asset inventory does not match the uploaded set: $description"
    }
}

$releasePath = (Resolve-Path -LiteralPath $ReleaseJsonPath).Path
$assetRoot = (Resolve-Path -LiteralPath $AssetDirectory).Path
$release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json

if ([string]$release.tag_name -cne $ExpectedTag) {
    throw "Published release tag '$($release.tag_name)' does not match expected tag '$ExpectedTag'."
}
if ($release.draft -ne $false) {
    throw 'Published release is still a draft.'
}
if ($release.prerelease -ne $false) {
    throw 'Published release was unexpectedly marked as a prerelease.'
}
if ($release.immutable -ne $true) {
    throw 'Published release is not immutable.'
}

$localAssets = @(Get-ChildItem -LiteralPath $assetRoot -Force -File)
if ($localAssets.Count -eq 0) {
    throw "No assembled release assets were found at $assetRoot."
}

$remoteAssets = @($release.assets)
Assert-ExactNameSet `
    -Actual @($remoteAssets | ForEach-Object { [string]$_.name }) `
    -Expected @($localAssets.Name)

foreach ($localAsset in $localAssets) {
    $matchingAssets = @($remoteAssets | Where-Object { [string]$_.name -ceq $localAsset.Name })
    if ($matchingAssets.Count -ne 1) {
        throw "Expected exactly one published asset named '$($localAsset.Name)'."
    }

    $remoteAsset = $matchingAssets[0]
    if ([long]$remoteAsset.size -ne $localAsset.Length) {
        throw "Published asset '$($localAsset.Name)' size does not match the uploaded file."
    }

    $digest = [string]$remoteAsset.digest
    $match = [Text.RegularExpressions.Regex]::Match(
        $digest,
        '\Asha256:([a-fA-F0-9]{64})\z',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Published asset '$($localAsset.Name)' has no valid GitHub SHA-256 digest."
    }

    $localHash = (Get-FileHash -LiteralPath $localAsset.FullName -Algorithm SHA256).Hash
    if (-not [String]::Equals(
            $localHash,
            $match.Groups[1].Value,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Published asset '$($localAsset.Name)' digest does not match the uploaded file."
    }
}

Write-Output "Verified immutable published release $ExpectedTag with $($localAssets.Count) exact asset(s)."

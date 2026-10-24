[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'

$repository = (Resolve-Path -LiteralPath $RepositoryRoot).Path
& (Join-Path $PSScriptRoot 'validate-release-version.ps1') -RepositoryRoot $repository | Out-Null
$version = (Get-Content -LiteralPath (Join-Path $repository 'version.txt') -Raw).Trim()
$changelog = Get-Content -LiteralPath (Join-Path $repository 'CHANGELOG.md') -Raw -Encoding utf8
$headings = [regex]::Matches($changelog, '(?m)^##[ \t]+[^\r\n]*')
$versionHeading = '^##[ \t]+\[' + [regex]::Escape($version) + '\](?:\([^\r\n)]*\))?(?:[ \t]+.*)?$'
$matchingHeadings = @($headings | Where-Object { $_.Value -match $versionHeading })
if ($matchingHeadings.Count -ne 1) {
    throw "CHANGELOG.md must contain exactly one section for [$version]; found $($matchingHeadings.Count)."
}

$heading = $matchingHeadings[0]
$nextHeading = $headings | Where-Object { $_.Index -gt $heading.Index } | Select-Object -First 1
$end = if ($null -eq $nextHeading) { $changelog.Length } else { $nextHeading.Index }
$notes = $changelog.Substring($heading.Index, $end - $heading.Index).Trim()
$body = $changelog.Substring($heading.Index + $heading.Length, $end - $heading.Index - $heading.Length)
$visibleBody = [regex]::Replace($body, '(?s)<!--.*?-->', '')
$visibleBody = [regex]::Replace($visibleBody, '(?m)^[ \t]*#{1,6}(?:[ \t]+[^\r\n]*)?\r?$', '')
if ([string]::IsNullOrWhiteSpace($visibleBody)) {
    throw "CHANGELOG.md section [$version] has no release notes beyond headings or comments."
}

Write-Output $notes

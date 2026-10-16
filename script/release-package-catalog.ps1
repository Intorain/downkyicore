function Get-ReleasePackageCatalog {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    @(
        [pscustomobject]@{
            Scope = 'Windows'
            Name = "DownKyi-$ExpectedVersion-1.win-x64.zip"
            Kind = 'zip'
            RuntimeIdentifier = 'win-x64'
            Manifest = 'publish-manifest-win-x64.json'
            Transport = $null
        },
        [pscustomobject]@{
            Scope = 'Windows'
            Name = "DownKyi-$ExpectedVersion-1.win-x86.zip"
            Kind = 'zip'
            RuntimeIdentifier = 'win-x86'
            Manifest = 'publish-manifest-win-x86.json'
            Transport = $null
        },
        [pscustomobject]@{
            Scope = 'LinuxX64'
            Name = "DownKyi-${ExpectedVersion}_linux_self-contained.x86_64.AppImage"
            Kind = 'AppImage'
            RuntimeIdentifier = 'linux-x64'
            Manifest = 'publish-manifest-linux-x64-AppImage.json'
            Transport = 'appimage-x64.transport.tar'
        },
        [pscustomobject]@{
            Scope = 'LinuxX64'
            Name = "downkyi_${ExpectedVersion}_linux_self-contained_amd64.deb"
            Kind = 'deb'
            RuntimeIdentifier = 'linux-x64'
            Manifest = 'publish-manifest-linux-x64-deb.json'
            Transport = $null
        },
        [pscustomobject]@{
            Scope = 'LinuxX64'
            Name = "downkyi_${ExpectedVersion}_linux_self-contained.x86_64.rpm"
            Kind = 'rpm'
            RuntimeIdentifier = 'linux-x64'
            Manifest = 'publish-manifest-linux-x64-rpm.json'
            Transport = $null
        },
        [pscustomobject]@{
            Scope = 'LinuxArm64'
            Name = "DownKyi-${ExpectedVersion}_linux_self-contained.aarch64.AppImage"
            Kind = 'AppImage'
            RuntimeIdentifier = 'linux-arm64'
            Manifest = 'publish-manifest-linux-arm64-AppImage.json'
            Transport = 'appimage-arm64.transport.tar'
        },
        [pscustomobject]@{
            Scope = 'LinuxArm64'
            Name = "downkyi_${ExpectedVersion}_linux_self-contained_arm64.deb"
            Kind = 'deb'
            RuntimeIdentifier = 'linux-arm64'
            Manifest = 'publish-manifest-linux-arm64-deb.json'
            Transport = $null
        },
        [pscustomobject]@{
            Scope = 'MacOSX64'
            Name = "DownKyi-$ExpectedVersion-osx-x64.dmg"
            Kind = 'dmg'
            RuntimeIdentifier = 'osx-x64'
            Manifest = 'publish-manifest-osx-x64.json'
            Transport = $null
        },
        [pscustomobject]@{
            Scope = 'MacOSArm64'
            Name = "DownKyi-$ExpectedVersion-osx-arm64.dmg"
            Kind = 'dmg'
            RuntimeIdentifier = 'osx-arm64'
            Manifest = 'publish-manifest-osx-arm64.json'
            Transport = $null
        }
    )
}

function Get-ReleaseValidationMatrix {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExpectedVersion
    )

    $packages = @(Get-ReleasePackageCatalog -ExpectedVersion $ExpectedVersion)
    $validationScopes = @(
        [pscustomobject]@{ Scope = 'Inventory'; Runner = 'ubuntu-22.04' },
        [pscustomobject]@{ Scope = 'Windows'; Runner = 'windows-latest' },
        [pscustomobject]@{ Scope = 'LinuxX64'; Runner = 'ubuntu-22.04' },
        [pscustomobject]@{ Scope = 'LinuxArm64'; Runner = 'ubuntu-24.04-arm' },
        [pscustomobject]@{ Scope = 'MacOSX64'; Runner = 'macos-15-intel' },
        [pscustomobject]@{ Scope = 'MacOSArm64'; Runner = 'macos-15' }
    )

    foreach ($validationScope in $validationScopes) {
        $artifactNames = @(
            if ($validationScope.Scope -ceq 'Inventory') {
                '*'
            }
            else {
                $packages |
                    Where-Object Scope -CEQ $validationScope.Scope |
                    ForEach-Object {
                        if ([string]::IsNullOrWhiteSpace([string]$_.Transport)) {
                            $_.Name
                        }
                        else {
                            $_.Transport -creplace '\.transport\.tar$', '-transport'
                        }
                    }
            }
        )

        if ($artifactNames.Count -eq 0) {
            throw "Release validation scope $($validationScope.Scope) has no package artifacts."
        }
        if ($artifactNames.Count -gt 3) {
            throw "Release validation scope $($validationScope.Scope) exceeds the three supported artifact downloads."
        }

        [pscustomobject]@{
            scope = $validationScope.Scope
            os = $validationScope.Runner
            pattern = $artifactNames[0]
            pattern2 = if ($artifactNames.Count -ge 2) { $artifactNames[1] } else { '' }
            pattern3 = if ($artifactNames.Count -ge 3) { $artifactNames[2] } else { '' }
        }
    }
}

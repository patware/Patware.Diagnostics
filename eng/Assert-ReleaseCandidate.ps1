#requires -Version 7.0
<#
.SYNOPSIS
Verifies the approved candidate before publication or finalization.

.DESCRIPTION
Checks the candidate against the original workflow commit and requested
version, validates package identities, and verifies artifact checksums.
Run from the repository root with REQUESTED_VERSION and GITHUB_SHA set.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path 'artifacts/approved-release').Path
$manifest = Get-Content "$root/manifest.json" -Raw |
ConvertFrom-Json

if ($manifest.schemaVersion -ne 1 -or
  $manifest.version -cne $env:REQUESTED_VERSION -or
  $manifest.commit -ne $env:GITHUB_SHA -or
  $manifest.tag -cne "v$env:REQUESTED_VERSION") {
  throw 'Release manifest identity mismatch.'
}

foreach ($check in @(
    'libraryTests',
    'packageChecks',
    'consumerSmoke'
  )) {
  if ($manifest.validation.$check -ne 'passed') {
    throw "Release validation did not pass: $check"
  }
}

$catalog = Import-PowerShellDataFile './eng/Packages.psd1'
$releasePackages = @($catalog.Packages)

if ($releasePackages.Count -eq 0) {
  throw 'The release package list is empty.'
}

$expectedPaths = @(
  foreach ($package in $releasePackages) {
    foreach ($extension in @('nupkg', 'snupkg')) {
      "packages/$($package.PackageId).$($manifest.version).$extension"
    }
  }
)

$files = @($manifest.files)
$actualPaths = @($files | ForEach-Object { $_.path })

# Compare exact identities, not just a prefix and total count.
# This also rejects duplicates, unexpected files, and missing packages.
if ($files.Count -ne $expectedPaths.Count -or
  @($actualPaths | Sort-Object -Unique).Count -ne $expectedPaths.Count -or
  (($actualPaths | Sort-Object) -join '|') -cne
  (($expectedPaths | Sort-Object) -join '|')) {
  throw 'Candidate files do not match the release package list.'
}

if ($manifest.validation.packageCount -ne $releasePackages.Count -or
  $manifest.validation.symbolCount -ne $releasePackages.Count) {
  throw 'Manifest validation counts do not match the release package list.'
}

foreach ($file in $files) {
  $path = Join-Path $root $file.path
  $item = Get-Item -LiteralPath $path
  $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash

  if ($item.Length -ne $file.bytes -or $hash -ne $file.sha256) {
    throw "Artifact verification failed: $($file.path)"
  }
}

if ($manifest.releaseNotes.path -cne 'release-notes.md') {
  throw 'Unexpected release notes path.'
}

$notesHash = (
  Get-FileHash "$root/release-notes.md" -Algorithm SHA256
).Hash

if ($notesHash -ne $manifest.releaseNotes.sha256) {
  throw 'Release notes checksum mismatch.'
}

#requires -Version 7.0
<#
.SYNOPSIS
Verifies that an existing GitHub release belongs to the approved candidate.

.DESCRIPTION
Checks release identity, any existing version tag, and the exact uploaded
asset contents. Downloads assets into a unique inspection directory.

The approved candidate must first pass Assert-ReleaseCandidate.ps1.
Returns the release metadata without modifying the GitHub release.
#>
[CmdletBinding()]
param(
    [string] $CandidateDirectory = 'artifacts/approved-release',
    [string] $Repository = $env:GITHUB_REPOSITORY
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Repository)) {
    throw 'A GitHub repository is required.'
}

$root = (Resolve-Path -LiteralPath $CandidateDirectory).Path
$manifest = Get-Content (Join-Path $root 'manifest.json') -Raw `
  | ConvertFrom-Json

$releaseJson = & gh release view $manifest.tag `
    --repo $Repository `
    --json tagName,isDraft,isPrerelease,targetCommitish,assets

if ($LASTEXITCODE -ne 0) {
    throw "Cannot inspect the existing GitHub release $($manifest.tag)."
}

$release = ($releaseJson -join "`n") | ConvertFrom-Json

if ($release.tagName -cne $manifest.tag -or $release.isPrerelease) {
    throw 'GitHub release identity does not match the stable candidate.'
}

if ($release.isDraft -and
    $release.targetCommitish -ne $manifest.commit) {
    throw 'The draft release targets a different commit.'
}

# A draft might not have created its tag yet. If a tag exists, resolve it
# to its commit rather than assuming targetCommitish controls that tag.
$refsJson = & gh api "repos/$Repository/git/matching-refs/tags/$($manifest.tag)"

if ($LASTEXITCODE -ne 0) {
    throw 'Cannot inspect existing release tags.'
}

$matchingRefs = @(($refsJson -join "`n") | ConvertFrom-Json)
$exactRefs = @(
    $matchingRefs |
        Where-Object { $_.ref -ceq "refs/tags/$($manifest.tag)" }
)

if ($exactRefs.Count -gt 0) {
    $tagCommit = & gh api `
        "repos/$Repository/commits/$($manifest.tag)" `
        --jq '.sha'

    if ($LASTEXITCODE -ne 0) {
        throw 'Cannot resolve the release tag to a commit.'
    }

    if (($tagCommit -join '').Trim() -ne $manifest.commit) {
        throw 'The existing release tag points to a different commit.'
    }
}
elseif (-not $release.isDraft) {
    throw 'The published release has no matching version tag.'
}

$expectedAssets = @(
    foreach ($file in $manifest.files) {
        [pscustomobject]@{
            Name = [IO.Path]::GetFileName($file.path)
            Path = Join-Path $root $file.path
        }
    }

    [pscustomobject]@{
        Name = 'manifest.json'
        Path = Join-Path $root 'manifest.json'
    }

    [pscustomobject]@{
        Name = 'release-notes.md'
        Path = Join-Path $root 'release-notes.md'
    }
)

$expectedNames = @($expectedAssets | ForEach-Object Name)
$actualNames = @($release.assets | ForEach-Object Name)

if ($actualNames.Count -ne $expectedNames.Count -or
    (($actualNames | Sort-Object) -join '|') -cne
    (($expectedNames | Sort-Object) -join '|')) {
    throw 'GitHub release assets do not match the approved candidate.'
}

$inspectionDirectory = Join-Path $root (
    'github-inspection-' + [Guid]::NewGuid().ToString('N')
)

$null = New-Item -ItemType Directory -Path $inspectionDirectory

$null = & gh release download $manifest.tag `
    --repo $Repository `
    --dir $inspectionDirectory

if ($LASTEXITCODE -ne 0) {
    throw 'Could not download the existing release assets for verification.'
}

foreach ($asset in $expectedAssets) {
    $downloadedPath = Join-Path $inspectionDirectory $asset.Name

    $expectedHash = (
        Get-FileHash -LiteralPath $asset.Path -Algorithm SHA256
    ).Hash

    $downloadedHash = (
        Get-FileHash -LiteralPath $downloadedPath -Algorithm SHA256
    ).Hash

    if ($downloadedHash -ne $expectedHash) {
        throw "GitHub release asset differs from the candidate: $($asset.Name)"
    }
}

Write-Output $release

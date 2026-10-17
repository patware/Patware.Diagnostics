#requires -Version 7.0
[CmdletBinding()]
param(
  [string] $PackageDirectory,
  [string] $ExpectedCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-DotNet {
  & dotnet @args | Out-Host

  if ($LASTEXITCODE -ne 0) {
    throw "dotnet failed with exit code $LASTEXITCODE."
  }
}

function Read-ZipText {
  param(
    [IO.Compression.ZipArchive] $Archive,
    [string] $Name
  )

  $entry = $Archive.GetEntry($Name)

  if ($null -eq $entry -or $entry.Length -eq 0) {
    throw "Missing or empty package entry: $Name"
  }

  $reader = [IO.StreamReader]::new($entry.Open())

  try {
    return $reader.ReadToEnd()
  }
  finally {
    $reader.Dispose()
  }
}

function Assert-ZipEntry {
  param(
    [IO.Compression.ZipArchive] $Archive,
    [string] $Name
  )

  $entry = $Archive.GetEntry($Name)

  if ($null -eq $entry -or $entry.Length -eq 0) {
    throw "Missing or empty package entry: $Name"
  }
}

$root = Split-Path $PSScriptRoot -Parent
$version = (Get-Content (Join-Path $root 'VERSION') -Raw).Trim()

if ([string]::IsNullOrWhiteSpace($PackageDirectory)) {
  $versionDirectory = Join-Path $root "artifacts/packages/v$version"

  if (!(Test-Path -LiteralPath $versionDirectory -PathType Container)) {
    throw "No package builds found for version $version. Run Build.ps1 first."
  }

  $latestDirectory = Get-ChildItem -LiteralPath $versionDirectory -Directory |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 1

  if ($null -eq $latestDirectory) {
    throw "No package attempts found in $versionDirectory."
  }

  $PackageDirectory = $latestDirectory.FullName
}

if (!(Test-Path -LiteralPath $PackageDirectory -PathType Container)) {
  throw "Package directory does not exist: $PackageDirectory"
}

# Resolve explicit relative paths before changing locations or writing
# the package source into the generated NuGet configuration.
$PackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).ProviderPath

$catalog = Import-PowerShellDataFile (Join-Path $PSScriptRoot 'Packages.psd1')
$releasePackages = @($catalog.Packages)

if ($releasePackages.Count -eq 0) {
  throw 'The release package list is empty.'
}

# Resolve project references to package identities. Project filenames,
# assembly names, and package IDs need not be identical.
$projectsByPath = @{}
$assembliesById = @{}

foreach ($package in $releasePackages) {
  $projectPath = (
    Resolve-Path -LiteralPath (Join-Path $root $package.Project)
  ).Path

  if ($projectsByPath.ContainsKey($projectPath) -or
    $assembliesById.ContainsKey($package.PackageId)) {
    throw "Duplicate release project or package: $($package.PackageId)"
  }

  $projectsByPath[$projectPath] = $package.PackageId
  $assembliesById[$package.PackageId] = $package.AssemblyName
}

$dependencies = [ordered]@{}

Push-Location $root

try {
  foreach ($package in $releasePackages) {
    $id = $package.PackageId
    $projectPath = (
      Resolve-Path -LiteralPath (Join-Path $root $package.Project)
    ).Path

    $output = @(& dotnet list $projectPath reference)

    if ($LASTEXITCODE -ne 0) {
      throw "Cannot list project references for $id."
    }

    $references = @(
      foreach ($line in $output) {
        $reference = $line.Trim()

        if ($reference -notmatch '\.csproj$') {
          continue
        }

        $reference = $reference.Replace(
          '\',
          [IO.Path]::DirectorySeparatorChar
        )

        $referencePath = [IO.Path]::GetFullPath(
          $reference,
          (Split-Path $projectPath -Parent)
        )

        $referencePath = (
          Resolve-Path -LiteralPath $referencePath
        ).Path

        if (!$projectsByPath.ContainsKey($referencePath)) {
          throw "$id references a project outside the release list: $referencePath"
        }

        $projectsByPath[$referencePath]
      }
    )

    $dependencies[$id] = @($references | Sort-Object -Unique)
  }
}
finally {
  Pop-Location
}

$packages = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.nupkg')
$symbols = @(Get-ChildItem -LiteralPath $PackageDirectory -Filter '*.snupkg')

if ($packages.Count -ne $dependencies.Count -or
  $symbols.Count -ne $dependencies.Count) {
  throw "Expected $($releasePackages.Count) .nupkg and .snupkg files; found $($packages.Count) and $($symbols.Count)."
}

$releaseUrl = "https://github.com/patware/Patware.Diagnostics/releases/tag/v$version"

foreach ($id in $dependencies.Keys) {
  $assemblyName = $assembliesById[$id]
  $packagePath = Join-Path $PackageDirectory "$id.$version.nupkg"
  $symbolPath = Join-Path $PackageDirectory "$id.$version.snupkg"

  if (!(Test-Path -LiteralPath $packagePath -PathType Leaf) -or
    !(Test-Path -LiteralPath $symbolPath -PathType Leaf)) {
    throw "Missing package or symbols for $id $version."
  }

  Write-Host "Inspecting $id"

  $archive = [IO.Compression.ZipFile]::OpenRead($packagePath)

  try {
    $nuspecs = @($archive.Entries | Where-Object FullName -Like '*.nuspec')

    if ($nuspecs.Count -ne 1) {
      throw "$id must contain exactly one nuspec."
    }

    [xml] $nuspec = Read-ZipText $archive $nuspecs[0].FullName
    $metadata = $nuspec.SelectSingleNode(
      "/*[local-name()='package']/*[local-name()='metadata']"
    )

    if ([string]$metadata.id -cne $id -or
      [string]$metadata.version -cne $version) {
      throw "Unexpected package identity in $packagePath."
    }

    if ([string]$metadata.releaseNotes -cne
      "Release and upgrade notes: $releaseUrl") {
      throw "Incorrect release-note link in $id."
    }

    if ([string]$metadata.license.InnerText -ne 'MIT' -or
      [string]$metadata.license.type -ne 'expression') {
      throw "Expected the MIT license expression in $id."
    }

    if ([string]$metadata.repository.url -ne
      'https://github.com/patware/Patware.Diagnostics') {
      throw "Incorrect repository URL in $id."
    }

    if ($ExpectedCommit -and
      [string]$metadata.repository.commit -ne $ExpectedCommit) {
      throw "Incorrect source commit in $id."
    }

    if ([string]$metadata.readme -ne 'README.md') {
      throw "Incorrect README metadata in $id."
    }

    $null = Read-ZipText $archive 'README.md'
    Assert-ZipEntry $archive "lib/net10.0/$assemblyName.dll"
    Assert-ZipEntry $archive "lib/net10.0/$assemblyName.xml"

    $internalDependencies = @(
      $metadata.SelectNodes(".//*[local-name()='dependency']") |
      Where-Object { $_.GetAttribute('id').StartsWith('Patware.Diagnostics.') }
    )

    $actualIds = @(
      $internalDependencies |
      ForEach-Object { $_.GetAttribute('id') } |
      Sort-Object -Unique
    )

    $expectedIds = @(
      $dependencies[$id] | Sort-Object
    )

    if (($actualIds -join '|') -cne ($expectedIds -join '|')) {
      throw "Incorrect internal dependency set in $id."
    }

    foreach ($dependency in $internalDependencies) {
      $range = $dependency.GetAttribute('version') -replace '\s', ''

      # NuGet may serialize a minimum version as either form below.
      # An exact-version range is also acceptable.
      if ($range -notin @($version, "[$version,)", "[$version]")) {
        throw "Incorrect dependency version in ${id}: $($dependency.OuterXml)"
      }
    }
  }
  finally {
    $archive.Dispose()
  }

  $archive = [IO.Compression.ZipFile]::OpenRead($symbolPath)

  try {
    $nuspecs = @($archive.Entries | Where-Object FullName -Like '*.nuspec')

    if ($nuspecs.Count -ne 1) {
      throw "$id symbols must contain exactly one nuspec."
    }

    [xml] $nuspec = Read-ZipText $archive $nuspecs[0].FullName
    $metadata = $nuspec.SelectSingleNode(
      "/*[local-name()='package']/*[local-name()='metadata']"
    )

    if ([string]$metadata.id -cne $id -or
      [string]$metadata.version -cne $version) {
      throw "Unexpected symbol-package identity for $id."
    }

    Assert-ZipEntry $archive "lib/net10.0/$assemblyName.pdb"
  }
  finally {
    $archive.Dispose()
  }
}

$smokeDirectory = Join-Path $root (
  'artifacts/smoke/' + [Guid]::NewGuid().ToString('N')
)

New-Item -ItemType Directory -Path $smokeDirectory | Out-Null

# Stop the repository's build properties and targets from flowing into
# the generated consumer. It should behave like an external application.
Set-Content (Join-Path $smokeDirectory 'Directory.Build.props') `
  '<Project />' -Encoding utf8

Set-Content (Join-Path $smokeDirectory 'Directory.Build.targets') `
  '<Project />' -Encoding utf8

# Reference Hosting alone to prove that its package dependency supplies Core.
# Referencing both directly would conceal a missing Hosting -> Core dependency.
$hostingId = 'Patware.Diagnostics.ApplicationEvents.Hosting'

if (!$assembliesById.ContainsKey($hostingId)) {
    throw "The consumer smoke test requires $hostingId."
}

$projectText = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="$hostingId" Version="[$version]" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.13" />
  </ItemGroup>
</Project>
"@

Set-Content (Join-Path $smokeDirectory 'Smoke.csproj') `
  $projectText -Encoding utf8

$escapedFeed = [Security.SecurityElement]::Escape($PackageDirectory)

$configText = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <clear />
    <packageSource key="local">
      <package pattern="Patware.Diagnostics.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
  <fallbackPackageFolders>
    <clear />
  </fallbackPackageFolders>
</configuration>
"@

Set-Content (Join-Path $smokeDirectory 'NuGet.Config') `
  $configText -Encoding utf8

$programText = Get-Content (Join-Path $root 'eng/consumer.program.cs') -Raw

$assemblyNames = ConvertTo-Json -InputObject @(
    $releasePackages | ForEach-Object { $_.AssemblyName }
) -Compress

$programText = $programText.Replace('__ASSEMBLIES__', $assemblyNames)

Set-Content (Join-Path $smokeDirectory 'Program.cs') `
  $programText -Encoding utf8

Push-Location $smokeDirectory

try {
  Invoke-DotNet restore 'Smoke.csproj' `
    --configfile 'NuGet.Config' `
    --packages (Join-Path $smokeDirectory 'cache') `
    --no-http-cache

  Invoke-DotNet build 'Smoke.csproj' `
    --configuration Release `
    --no-restore

  Invoke-DotNet run `
    --project 'Smoke.csproj' `
    --configuration Release `
    --no-build `
    --no-restore
}
finally {
  Pop-Location
}

[pscustomobject]@{
  Version          = $version
  PackageDirectory = $PackageDirectory
  PackageCount     = $packages.Count
  SymbolCount      = $symbols.Count
  SmokeDirectory   = $smokeDirectory
}

Start-Process $PackageDirectory

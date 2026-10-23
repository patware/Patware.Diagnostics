#requires -Version 7.0
<#
.SYNOPSIS
Waits for the approved package versions to become downloadable.

.DESCRIPTION
Checks package availability without uploading packages or changing releases.
A timeout means finalization remains pending, not that package upload failed.
This check does not verify symbol indexing.
#>
[CmdletBinding()]
param(
    [ValidateRange(1, 60)]
    [int] $TimeoutMinutes = 60
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifest = Get-Content `
    'artifacts/approved-release/manifest.json' -Raw |
    ConvertFrom-Json

$version = $manifest.version
$pending = @(
    foreach ($file in $manifest.files) {
        if ($file.path.EndsWith('.nupkg')) {
            $name = [IO.Path]::GetFileName($file.path)
            $suffix = ".$version.nupkg"

            $name.Substring(
                0,
                $name.Length - $suffix.Length
            ).ToLowerInvariant()
        }
    }
)

if ($pending.Count -eq 0) {
    throw 'The approved candidate contains no packages to check.'
}

$deadline = [DateTimeOffset]::UtcNow.AddMinutes($TimeoutMinutes)

while ($pending.Count -gt 0) {
    $remaining = @(
        foreach ($id in $pending) {
            $url = "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"

            try {
                $null = Invoke-WebRequest $url `
                    -Method Head -TimeoutSec 20
            }
            catch {
                $response = $_.Exception.Response

                if ($null -eq $response) {
                    throw
                }

                $status = [int]$response.StatusCode

                # Absence can mean processing is still pending.
                # Throttling and temporary server failures are retried within
                # the same bounded wait, without claiming they mean indexing.
                if ($status -ne 404 -and
                    $status -ne 429 -and
                    ($status -lt 500 -or $status -gt 599)) {
                    throw
                }

                Write-Host "$id $version returned HTTP $status."
                $id
            }
        }
    )

    $pending = $remaining

    if ($pending.Count -eq 0) {
        break
    }

    if ([DateTimeOffset]::UtcNow -ge $deadline) {
        throw (
            'Packages were uploaded, but availability was not confirmed ' +
            'before the deadline. Rerun only the finalization job; do not ' +
            "republish packages. Still waiting for: $($pending -join ', ')"
        )
    }

    Write-Host "Waiting for NuGet availability: $($pending -join ', ')"
    Start-Sleep -Seconds 30
}

Write-Host "All approved package versions are downloadable."

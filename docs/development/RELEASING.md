# Contributing changes and publishing releases

First of all... Thank you.

Changes reach users through a topic branch, pull request, validated release
candidate, and maintainer-approved publication.

Contributors prepare and verify changes. Maintainers coordinate release
versions, merge pull requests, and authorize publication.

All commands below run from the repository root using PowerShell.

## 1. Prepare your environment

Install:

- Git.
- A stable .NET 10 SDK compatible with `global.json`.
- PowerShell 7.4 or later, required by the event generator.
- DocFX 2.81.0, matching the CI and release workflows.
- Poshstache 0.1.10 when generating or verifying the event catalog.

For a new installation:

```powershell
dotnet tool install --global docfx --version 2.81.0

Install-Module Poshstache `
    -RequiredVersion 0.1.10 `
    -Scope CurrentUser
```

If DocFX is already installed, use `dotnet tool update` with the same version
instead of installing it again.

Windows is required to run the WPF sample. Linux CI can compile it using
the project's Windows-targeting configuration.

Contributors do not need NuGet publication credentials to build, test,
or prepare local packages.

## 2. Create a topic branch

Start with a clean working tree and an up-to-date `main`:

```powershell
git status --short
git switch main
git pull --ff-only
git switch -c feature/configuration-validation-events
```

Choose a descriptive branch name for your change. Examples include:

- `feature/configuration-validation-events`
- `fix/structured-property-names`
- `docs/release-guide`

Codex-created branches use `codex/<description>`.

If you have existing uncommitted work, preserve it before switching branches.
Do not discard changes merely to obtain a clean working tree.

## 3. Implement the change

Keep changes focused and include:

- Implementation and relevant tests.
- XML documentation for public types and members.
- Sample updates when behavior needs demonstration.
- User-facing documentation and navigation updates.
- A changelog entry describing the effect on consumers.

Follow the repository's coding standards. Unit tests use
`// Arrange`, `// Act`, and `// Assert`.

For event changes, edit the catalog and templates rather than generated
outputs:

```powershell
./catalog/ApplicationEvents/Generate-Events.ps1
./catalog/ApplicationEvents/Generate-Events.ps1 -Verify
```

Commit both the source changes and the generated outputs:

- `src/ApplicationEvents/ApplicationEvents.Core/Generated/EventLoggingExtensions.g.cs`
- `docs/architecture/EVENT-CATALOG.md`
- `docs/events/index.md`, `docs/events/toc.yml`, and the generated
  `Event_{EventId}.md` and `Category_{CategoryName}.md` pages.

The old catalog is a compatibility page preserving existing event anchors.
The Events section contains the current index, category diagrams, event details,
and API cross-references. Do not edit generated pages directly. Generation removes
stale marked event/category pages; `-Verify` reports missing, outdated, and stale
outputs without writing files. Authored files in that directory are preserved.

Published event IDs and their meanings must remain compatible.
Review event-contract changes before assigning or repurposing an ID.

## 4. Validate locally

Verify generated events and whitespace:

```powershell
./catalog/ApplicationEvents/Generate-Events.ps1 -Verify
git diff --check
```

Run the repository build:

```powershell
$packages = ./Build.ps1
```

The current build script:

1. Builds the release libraries.
2. Runs the library test projects.
3. Creates NuGet packages and symbol packages.
4. Builds all sample applications.
5. Generates DocFX metadata and builds the documentation site.

The script displays progress and returns one value: the package directory.

Validate package contents and the consumer smoke test:

```powershell
$commit = (git rev-parse HEAD).Trim()

./eng/Test-Packages.ps1 `
    -PackageDirectory $packages `
    -ExpectedCommit $commit
```

Local validation can run while developing. A package built from uncommitted
changes is not a release candidate with a fully committed source identity.

Exercise affected sample scenarios manually. Compilation does not establish
that interactive behavior works.

The current build script does not run the event generator's `-Verify` check,
so run that check explicitly.

## 5. Review, commit, and push

Review the intended changes:

```powershell
git status --short
git diff
```

Stage the intended source, test, documentation, and generated catalog files.
Check the staged diff before committing:

```powershell
git diff --cached
git diff --cached --check
git commit -m "Add configuration validation events"
git push -u origin HEAD
```

Do not include:

- Credentials or sensitive configuration.
- `bin`, `obj`, local package output, or generated documentation-site output.
- Simulation files such as `degraded.txt`, `stop.txt`, or `exception.txt`.

The authored `docs/api/index.md` is source documentation and should be tracked.
Generated API metadata under `docs/api` should remain ignored.

These commands assume `origin` is a remote you can push to. External
contributors can push to their fork and open a pull request against the
upstream repository's `main` branch.

## 6. Open and merge the pull request

Open a pull request targeting `main`.

Describe:

- The problem and resulting behavior.
- Public API or event-contract changes.
- Tests and sample scenarios actually verified.
- Documentation and package checks performed.
- Any remaining limitations.

The CI workflow builds, tests, packages, builds samples, generates
documentation, and validates packages and consumer behavior.

Address review comments and failing checks before merge. Maintainers choose
the merge method and enforce the repository's review requirements.

Pushing a branch or merging a pull request does not publish a package.

## 7. Prepare release metadata

Maintainers choose the release version and prepare the release changes on
a branch. This can be a separate release PR after feature PRs have merged.

Update:

- `VERSION`, which supplies the shared package version.
- `CHANGELOG.md`, with exactly one section matching that version.
- Package documentation and examples where version requirements changed.
- Event `Since` metadata for newly introduced events.

For example:

```markdown
## 0.0.4 - Unpublished

### Added

- Configuration validation events with correlated attempt identifiers.
```

The release-note extractor accepts this heading, but copies it into the
release notes verbatim. Use the intended publication date before final
approval if the published notes should contain a date.

Verify extraction:

```powershell
./eng/Get-ReleaseNotes.ps1 -Version 0.0.4
```

The GitHub Release workflow currently accepts stable `MAJOR.MINOR.PATCH`
versions only, even though some local scripts support prerelease suffixes.

Merge release metadata into `main` after review and successful CI.

## 8. Prepare a local candidate when needed

After committing all intended changes, verify that the working tree is clean:

```powershell
git status --short
```

There must be no output. Untracked files also block release preparation.

Run:

```powershell
$releaseDirectory = ./eng/Prepare-Release.ps1

Get-Content "$releaseDirectory/manifest.json"
Get-Content "$releaseDirectory/release-notes.md"
```

The script builds and validates the candidate and writes it under:

```text
artifacts/releases/v<version>/<attempt>/
```

The directory contains:

- `packages/`: NuGet packages and symbol packages.
- `release-notes.md`: the matching changelog section.
- `manifest.json`: version, commit, validation results, and checksums.

The manifest is written last. Its absence means preparation did not finish
successfully.

Local preparation does not push commits, create a release tag, or publish
packages. The GitHub workflow builds its own candidate.

## 9. Inspect a GitHub release candidate

After the release PR is merged and CI passes on the intended `main` commit:

1. Open the repository's **Actions** page.
2. Select the **Release** workflow.
3. Choose **Run workflow**.
4. Select branch `main`.
5. Enter the version exactly as recorded in `VERSION`.
6. Leave `publish` disabled.

The workflow rejects runs from other branches or with a mismatched version.

Download the artifact named:

```text
release-candidate-<run-id>-<attempt>
```

Check:

- Version and source commit in `manifest.json`.
- Release notes.
- Expected package IDs and versions.
- Package and symbol files.
- Successful validation results.

The release package list and dependency order are defined in
`eng/Packages.psd1`. Currently it contains Core followed by Hosting.

A successful candidate-only run does not publish anything.

## 10. Publish the release

Publication is a maintainer operation.

The repository's `release` environment must have the intended approval
protection configured. Naming an environment in workflow YAML does not,
by itself, establish a required-reviewer gate.

NuGet trusted publishing must also be configured for the workflow, with
the `NUGET_USER` secret available to the publication job.

To publish:

1. Run **Release** again from `main`.
2. Enter the matching version.
3. Enable `publish`.
4. Wait for candidate preparation to complete.
5. Inspect the candidate artifact from this publication run.
6. Confirm its commit, version, notes, and package contents.
7. Approve the `release` environment when the candidate is accepted.

The workflow separates publication from finalization.

The approved publication job:

1. Verifies candidate identity and checksums.
2. Rejects an existing tag, GitHub release, or package version.
3. Authenticates with NuGet.
4. Creates a draft GitHub release with candidate assets.
5. Publishes packages and matching symbols in dependency order.

The finalization job:

1. Downloads the same candidate from the same workflow run.
2. Verifies candidate identity and checksums.
3. Checks the existing GitHub release, assets, and any existing version tag.
4. Waits up to 60 minutes for the package versions to become downloadable.
5. Rechecks the GitHub release and publishes the matching draft.

An already-published matching release is accepted as completed.
Finalization does not rebuild or upload packages and has no NuGet credentials.

## 11. Confirm publication

After the workflow succeeds:

- Confirm the GitHub release is public and references the intended commit.
- Confirm every expected package version is available on NuGet.
- Verify installation and basic use from a separate consumer when practical.
- Check symbol publication separately if diagnosing a symbol-related issue.

Retain the candidate manifest and artifacts for troubleshooting.
The workflow's NuGet availability check covers packages, not successful
symbol indexing.

## Publication failures

### Preparation failed

Before remote publication begins, fix the cause and prepare a new candidate
through the normal review process.

### Package publication failed

If the publication job created a draft or uploaded any package, inspect the
partial publication before retrying. Its duplicate checks intentionally
reject existing release state.

Record the workflow run, commit, candidate manifest, uploaded packages and
symbols, existing GitHub release, and exact failing step.

Preserve the original candidate. Do not rebuild different bytes under the
same package version or bypass duplicate checks without investigating.

### Finalization failed

If the publication job succeeded and only Finalize published candidate
failed, inspect the error and rerun only that job in the same workflow run.

Do not rerun all jobs or start a new publication run for that version.
The finalization job reuses the original candidate and never pushes packages.

An availability timeout means package availability was not confirmed within
the waiting period. It does not mean package upload failed.

A candidate, asset, or tag mismatch requires investigation before retrying.
Do not bypass verification to finalize a different release.

Workflow artifacts are retained for 30 days. If the original artifact has
expired, automatic finalization cannot recover it; maintainer investigation
is required.

### Runs created before finalization was separated

Old runs retain their original workflow. Rerunning them does not acquire
this recovery behavior.

When packages from such a run are already available, verify the original
candidate, existing draft, assets, and target commit before manually
publishing that draft. Do not push the packages again.

## Common problems

| Symptom | Check |
| --- | --- |
| Release preparation rejects the working tree | Commit intended files and remove or ignore local simulation artifacts. |
| No changelog section found | Ensure exactly one versioned section matches `VERSION`. |
| Requested version does not match | Run the workflow with the version committed on the selected `main`. |
| `PackageDirectory` is an array rather than a string | Route native-command output in `Build.ps1` through `Out-Host`; return only the package directory. |
| WPF build fails on Linux | Confirm `EnableWindowsTargeting` remains enabled and inspect the underlying SDK error. |
| DocFX command is unavailable | Install the version used by the workflows. |
| Generated event files are outdated | Regenerate, review, and commit all generated outputs, including page additions and deletions. |
| Tag, release, or package already exists | Inspect previous publication state before attempting recovery. |
| Finalization timed out after successful uploads | Inspect NuGet availability, then rerun only Finalize published candidate in the original run. |
| Finalization reports an asset or tag mismatch | Investigate the existing release against the original candidate; do not bypass verification. |

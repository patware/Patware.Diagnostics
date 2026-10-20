#Requires -Version 7.4

<#
.SYNOPSIS
Validates and generates application event APIs and documentation.

.DESCRIPTION
Validates the catalog before rendering either output.
Rendering completes in memory before files are written.
Only the two explicitly owned output files are updated.

.PARAMETER Verify
Checks whether generated files match the catalog and templates.
Does not create or modify files.

.PARAMETER ValidateOnly
Validates the catalog without loading the rendering engine or writing files.
#>

[CmdletBinding()]
param(
  [switch]$Verify,
  [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$catalogPath = Join-Path $PSScriptRoot 'events.json'
$schemaPath = Join-Path $PSScriptRoot 'events.schema.json'

$json = Get-Content -LiteralPath $catalogPath -Raw

if (-not (Test-Json -Json $json -SchemaFile $schemaPath)) {
  throw 'The event catalog does not satisfy events.schema.json.'
}


$catalog = ConvertFrom-Json -InputObject $json -AsHashtable

<#
  ---------- Validation Stage ----------
#>

Write-Host "Validating $($catalog.Ids.Count) application events."

$problems = [System.Collections.Generic.List[string]]::new()

$eventNames = [System.Collections.Generic.HashSet[string]]::new(
  [System.StringComparer]::Ordinal
)

$methodNames = [System.Collections.Generic.HashSet[string]]::new(
  [System.StringComparer]::Ordinal
)

foreach ($key in $catalog.Categories.Keys) {
  if ($key -cne $catalog.Categories[$key].Name) {
    $problems.Add("Category key '$key' does not match its Name.")
  }
}

foreach ($key in $catalog.LogClass.Keys) {
  if ($key -cne $catalog.LogClass[$key].Name) {
    $problems.Add("Event-class key '$key' does not match its Name.")
  }
}

foreach ($key in ($catalog.Ids.Keys | Sort-Object { [int]$_ })) {
  $eve = $catalog.Ids[$key]
  $label = "Event $key"

  if ([string]$eve.EventId -cne $key) {
    $problems.Add("$label has a different EventId: $($eve.EventId).")
  }

  if (-not $eventNames.Add($eve.EventName)) {
    $problems.Add("$label duplicates EventName '$($eve.EventName)'.")
  }

  $methodIdentity = "$($eve.Category).$($eve.Method)"

  if (-not $methodNames.Add($methodIdentity)) {
    $problems.Add("$label duplicates method '$methodIdentity'.")
  }

  if ($eve.Category -cnotin @($catalog.Categories.Keys)) {
    $problems.Add("$label references unknown category '$($eve.Category)'.")
  }

  if ($eve.EventClass -cnotin @($catalog.LogClass.Keys)) {
    $problems.Add("$label references unknown class '$($eve.EventClass)'.")
  }
  else {
    $range = [int][Math]::Floor($eve.EventId / 100.0)
    $expectedRange = $catalog.LogClass[$eve.EventClass].No

    if ($range -ne $expectedRange) {
      $problems.Add("$label is outside its event-class range.")
    }
  }

  $argumentNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase
  )

  foreach ($argument in $eve.Arguments) {
    if (-not $argumentNames.Add($argument.Name)) {
      $problems.Add(
        "$label has duplicate argument '$($argument.Name)' ignoring case."
      )
    }
  }

  # A single Exception argument is passed through ILogger's exception channel.
  # It is not a structured message-template property.
  $exceptionArguments = @(
    $eve.Arguments | Where-Object { $_.ValueType -ceq 'Exception' }
  )

  if ($exceptionArguments.Count -gt 1) {
    $problems.Add("$label supports at most one Exception argument.")
  }

  # Initially support simple named placeholders only.
  # Reject unsupported syntax instead of silently interpreting it incorrectly.
  $placeholderPattern = '\{(?<name>[A-Z][A-Za-z0-9]*)\}'
  $matches = [regex]::Matches($eve.Message, $placeholderPattern)
  $remainingText = [regex]::Replace(
    $eve.Message,
    $placeholderPattern,
    ''
  )

  if ($remainingText.Contains('{') -or $remainingText.Contains('}')) {
    $problems.Add(
      "$label must use simple PascalCase placeholders such as {Reason}."
    )
  }

  $placeholderNames = [System.Collections.Generic.HashSet[string]]::new(
    [System.StringComparer]::OrdinalIgnoreCase
  )

  foreach ($match in $matches) {
    $name = $match.Groups['name'].Value
    [void]$placeholderNames.Add($name)

    if (-not $argumentNames.Contains($name)) {
      $problems.Add("$label placeholder '$name' has no argument.")
    }
  }

  foreach ($argument in $eve.Arguments) {
    if ($argument.ValueType -ceq 'Exception') {
      if ($placeholderNames.Contains($argument.Name)) {
        $problems.Add(
          "$label Exception argument '$($argument.Name)' must not have a placeholder."
        )
      }

      continue
    }

    if (-not $placeholderNames.Contains($argument.Name)) {
      $problems.Add(
        "$label argument '$($argument.Name)' has no placeholder."
      )
    }
  }
}

foreach ($pair in $catalog.Related.Ing_Ed) {
  if ($pair.Ing -eq $pair.Ed) {
    $problems.Add("Related pair '$($pair.Ing)' references itself.")
  }

  $references = @($pair.Ing, $pair.Ed) + @($pair.Fails)

  foreach ($reference in $references) {
    if (-not $catalog.Ids.Contains($reference)) {
      $problems.Add("Related pair references unknown event '$reference'.")
    }
  }
}

foreach ($lifecycle in $catalog.Related.Lifecycle) {
  foreach ($reference in $lifecycle.States) {
    if (-not $catalog.Ids.Contains($reference)) {
      $problems.Add(
        "Lifecycle '$($lifecycle.Name)' references unknown event '$reference'."
      )
    }
  }
}

if ($problems.Count -gt 0) {
  throw ("Catalog validation failed:`n - " + ($problems -join "`n - "))
}

Write-Host "Validated $($catalog.Ids.Count) application events."

if ($ValidateOnly) {
  return
}


<#
  ---------- Generation Stage ----------
#>


# Pin the rendering dependency for reproducible generation.
Import-Module Poshstache -RequiredVersion 0.1.10 -ErrorAction Stop

# The Poshstache wrapper can return rendering failures as text.
# Calling its bundled engine directly preserves exception propagation.
$moduleRoot = Split-Path (Get-Module Poshstache).Path
Add-Type -Path (Join-Path $moduleRoot 'binary/Stubble.Core.dll')
$renderer = [Stubble.Core.Builders.StubbleBuilder]::new().Build()

function ConvertTo-XmlText {
  param([string]$Value)

  # Documentation is plain text; normalize embedded line breaks so every
  # generated XML comment remains on a correctly prefixed source line.
  $singleLine = [regex]::Replace($Value, '\s+', ' ').Trim()
  return [System.Security.SecurityElement]::Escape($singleLine)
}

function ConvertTo-MarkdownText {
  param([string]$Value)

  $singleLine = [regex]::Replace($Value, '\s+', ' ').Trim()

  # Escape Markdown punctuation before HTML encoding so the escaping
  # cannot corrupt numeric character references such as &#39;.
  $escaped = [regex]::Replace(
    $singleLine,
    '([\\`*_{}\[\]()#+.!|>-])',
    '\$1'
  )

  return [System.Net.WebUtility]::HtmlEncode($escaped)
}

function ConvertTo-CSharpLiteral {
  param([string]$Value)

  $builder = [System.Text.StringBuilder]::new()
  [void]$builder.Append('"')

  foreach ($character in $Value.ToCharArray()) {
    $code = [int]$character

    if ($code -eq 34) {
      [void]$builder.Append('\"')
    }
    elseif ($code -eq 92) {
      [void]$builder.Append('\\')
    }
    elseif ($code -lt 32 -or $code -gt 126) {
      [void]$builder.Append(('\u{0:x4}' -f $code))
    }
    else {
      [void]$builder.Append($character)
    }
  }

  [void]$builder.Append('"')
  return $builder.ToString()
}

function Normalize-GeneratedText {
  param([string]$Value)

  return $Value.Replace("`r`n", "`n").Replace("`r", "`n").TrimEnd() + "`n"
}

function New-EventRelationship {
  param(
    [string]$TargetId,
    [string]$Description
  )

  $target = $catalog.Ids[$TargetId]
  $label = "$TargetId — $($target.EventName)"

  # Retired events remain in the catalog but have no generated public API.
  # Render their identity as text rather than creating an unresolved cref.
  @{
    DescriptionXml = ConvertTo-XmlText $Description
    DescriptionMd  = ConvertTo-MarkdownText $Description
    LabelXml       = ConvertTo-XmlText $label
    LabelMd        = ConvertTo-MarkdownText $label
    Anchor         = "event-$TargetId"
    HasPublicApi   = $target.Status -ne 'Retired'
    CrefXml        = ConvertTo-XmlText (
      'Patware.Diagnostics.ApplicationEvents.{0}Events.{1}' -f (
        $target.Category,
        $target.Method
      )
    )
  }
}

function Get-EventRelationships {
  param([string]$EventId)

  foreach ($pair in (
    $catalog.Related.Ing_Ed | Sort-Object { [int]$_.Ing }
  )) {
    if ($pair.Ing -eq $EventId) {
      New-EventRelationship `
        -TargetId $pair.Ed `
        -Description 'Corresponding completion:'

      foreach ($failureId in ($pair.Fails | Sort-Object { [int]$_ })) {
        New-EventRelationship `
          -TargetId $failureId `
          -Description 'Possible failure outcome:'
      }
    }

    if ($pair.Ed -eq $EventId) {
      New-EventRelationship `
        -TargetId $pair.Ing `
        -Description 'Corresponding beginning:'
    }

    if ($EventId -in $pair.Fails) {
      New-EventRelationship `
        -TargetId $pair.Ing `
        -Description 'Beginning associated with this failure:'
    }
  }

  foreach ($lifecycle in $catalog.Related.Lifecycle) {
    $states = @($lifecycle.States)

    for ($index = 0; $index -lt $states.Count; $index++) {
      if ($states[$index] -ne $EventId) {
        continue
      }

      if ($index -gt 0) {
        New-EventRelationship `
          -TargetId $states[$index - 1] `
          -Description (
            "Expected preceding event in '$($lifecycle.Name)':"
          )
      }

      if ($index + 1 -lt $states.Count) {
        New-EventRelationship `
          -TargetId $states[$index + 1] `
          -Description (
            "Expected following event in '$($lifecycle.Name)':"
          )
      }
    }
  }
}

$orderedEvents = @(
  $catalog.Ids.Values | Sort-Object EventId
)

$relationshipsById = @{}

foreach ($eve in $orderedEvents) {
  $eventId = [string]$eve.EventId

  $relationshipsById[$eventId] = @(
    Get-EventRelationships -EventId $eventId
  )
}

$categoryModels = @()
$categoryNames = [string[]]@($catalog.Categories.Keys)
[Array]::Sort($categoryNames, [StringComparer]::Ordinal)

foreach ($categoryName in $categoryNames) {
  $methodModels = @()

  foreach ($eve in $orderedEvents) {
    if ($eve.Category -cne $categoryName -or $eve.Status -eq 'Retired') {
      continue
    }

    $argumentModels = @(
      foreach ($argument in $eve.Arguments) {
        # Internal logging parameters use PascalCase without verbatim prefixes.
        # This avoids reserved keywords and prevents Microsoft's fallback
        # generator from exposing an '@' prefix in structured property names.
        # Public method parameters retain their existing names and order.
        $internalName =
          $argument.Name.Substring(0, 1).ToUpperInvariant() +
          $argument.Name.Substring(1)

        @{
          Declaration = ", $($argument.ValueType) $internalName"
          Name = $argument.Name
          SummaryXml = ConvertTo-XmlText $argument.Summary
          IsException = $argument.ValueType -ceq 'Exception'
        }
      }
    )

    $methodModels += @{
      EventId          = $eve.EventId
      MethodName       = $eve.Method
      ParameterList    = (
        @($eve.Arguments | ForEach-Object {
            "$($_.ValueType) @$($_.Name)"
          }) -join ', '
      )

      ForwardArguments = (
        @($eve.Arguments | ForEach-Object {
            ", @$($_.Name)"
          }) -join ''
      )
      EventNameLiteral = ConvertTo-CSharpLiteral $eve.EventName
      MessageLiteral   = ConvertTo-CSharpLiteral $eve.Message
      LogLevel         = $eve.LogLevel
      SummaryXml       = ConvertTo-XmlText $eve.Summary
      TriggerXml       = ConvertTo-XmlText $eve.Trigger
      Status           = $eve.Status
      Since            = if ($null -eq $eve.Since) { 'Unpublished' } else { $eve.Since }
      Arguments        = $argumentModels
      Remarks          = @(
        foreach ($remark in $eve.Remarks) {
          @{ TextXml = ConvertTo-XmlText $remark }
        }
      )
      Relationships = $relationshipsById[[string]$eve.EventId]
      HasRelationships = (
        $relationshipsById[[string]$eve.EventId].Count -gt 0
      )

    }
  }

  if ($methodModels.Count -gt 0) {
    $categoryModels += @{
      CategoryName     = $categoryName
      GroupTypeName    = "${categoryName}Events"
      MessagesTypeName = "${categoryName}EventMessages"
      SummaryXml       = ConvertTo-XmlText $catalog.Categories[$categoryName].Summary
      Methods          = $methodModels
    }
  }
}

$documentationEvents = @(
  foreach ($eve in $orderedEvents) {
    @{
      EventId      = $eve.EventId
      EventName    = $eve.EventName
      Category     = $eve.Category
      EventClass   = $eve.EventClass
      LogLevel     = $eve.LogLevel
      Status       = $eve.Status
      Since        = if ($null -eq $eve.Since) { 'Unpublished' } else { $eve.Since }
      Summary      = ConvertTo-MarkdownText $eve.Summary
      Trigger      = ConvertTo-MarkdownText $eve.Trigger
      Message      = ConvertTo-MarkdownText $eve.Message
      Arguments    = @(
        foreach ($argument in $eve.Arguments) {
          @{
            Name      = $argument.Name
            ValueType = $argument.ValueType
            Summary   = ConvertTo-MarkdownText $argument.Summary
          }
        }
      )
      Remarks      = @(
        foreach ($remark in $eve.Remarks) {
          @{ Text = ConvertTo-MarkdownText $remark }
        }
      )
      HasPublicApi = $eve.Status -ne 'Retired'

      Usage        = 'logger.{0}.{1}({2});' -f (
        $eve.Category,
        $eve.Method,
        (
          @($eve.Arguments | ForEach-Object {
              "@$($_.Name)"
            }) -join ', '
        )
      )

      Relationships = $relationshipsById[[string]$eve.EventId]
      HasRelationships = (
        $relationshipsById[[string]$eve.EventId].Count -gt 0
      )
    }
  }
)

$pairModels = @(
  foreach ($pair in ($catalog.Related.Ing_Ed | Sort-Object { [int]$_.Ing })) {
    @{
      Starting  = "$($pair.Ing) — $($catalog.Ids[$pair.Ing].EventName)"
      Completed = "$($pair.Ed) — $($catalog.Ids[$pair.Ed].EventName)"
      Failures  = @(
        foreach ($failureId in ($pair.Fails | Sort-Object { [int]$_ })) {
          @{ Text = "$failureId — $($catalog.Ids[$failureId].EventName)" }
        }
      )
    }
  }
)

$lifecycleModels = @(
  foreach ($lifecycle in $catalog.Related.Lifecycle) {
    $states = @($lifecycle.States)

    @{
      Name = ConvertTo-MarkdownText $lifecycle.Name

      States = @(
        foreach ($stateId in $states) {
          @{
            Text = "$stateId — $($catalog.Ids[$stateId].EventName)"
          }
        }
      )

      Nodes = @(
        foreach ($stateId in $states) {
          @{
            NodeId  = "event$stateId"
            EventId = $stateId
            # EventName is already constrained to identifier characters.
            EventName = $catalog.Ids[$stateId].EventName
          }
        }
      )

      Edges = @(
        for ($index = 0; $index + 1 -lt $states.Count; $index++) {
          @{
            From = "event$($states[$index])"
            To   = "event$($states[$index + 1])"
          }
        }
      )
    }
  }
)

$model = @{
  Namespace  = 'Patware.Diagnostics.ApplicationEvents'
  Categories = $categoryModels
  Events     = $documentationEvents
  Pairs      = $pairModels
  Lifecycles = $lifecycleModels
}

$repositoryRoot = [System.IO.Path]::GetFullPath(
  (Join-Path $PSScriptRoot '../..')
)

$outputDefinitions = @(
  @{
    Template     = 'LoggingExtensions.cs.mustache'
    RelativePath = 'src/ApplicationEvents/ApplicationEvents.Core/Generated/EventLoggingExtensions.g.cs'
    Marker       = '// <auto-generated />'
  },
  @{
    Template     = 'Catalog.md.mustache'
    RelativePath = 'docs/architecture/EVENT-CATALOG.md'
    Marker       = '<!-- Generated by Generate-Events.ps1. Do not edit. -->'
  }
)

# Finish all rendering before considering any output writes.
$renderedOutputs = @(
  foreach ($definition in $outputDefinitions) {
    $templatePath = Join-Path $PSScriptRoot "Templates/$($definition.Template)"
    $templateText = Get-Content -LiteralPath $templatePath -Raw

    if ([string]::IsNullOrWhiteSpace($templateText)) {
      throw "Template is empty: $templatePath"
    }

    $content = Normalize-GeneratedText (
      $renderer.Render($templateText, $model)
    )

    if (-not $content.StartsWith($definition.Marker, [StringComparison]::Ordinal)) {
      throw "Template output is missing its ownership marker: $templatePath"
    }

    @{
      Path    = Join-Path $repositoryRoot $definition.RelativePath
      Content = $content
      Marker  = $definition.Marker
    }
  }
)

$changedOutputs = @(
  foreach ($output in $renderedOutputs) {
    if (Test-Path -LiteralPath $output.Path) {
      $existingText = [System.IO.File]::ReadAllText($output.Path)

      if (-not $existingText.StartsWith($output.Marker, [StringComparison]::Ordinal)) {
        throw "Refusing to replace a file without a generated marker: $($output.Path)"
      }

      if ((Normalize-GeneratedText $existingText) -ceq $output.Content) {
        continue
      }
    }

    $output
  }
)

if ($Verify) {
  if ($changedOutputs.Count -gt 0) {
    $paths = $changedOutputs.Path -join "`n - "
    throw "Generated files are missing or outdated:`n - $paths"
  }

  Write-Host 'Generated files are current.'
  return
}

foreach ($output in $changedOutputs) {
  $directory = Split-Path $output.Path
  [void][System.IO.Directory]::CreateDirectory($directory)

  [System.IO.File]::WriteAllText(
    $output.Path,
    $output.Content,
    [System.Text.UTF8Encoding]::new($false)
  )

  Write-Host "Generated $($output.Path)"
}

if ($changedOutputs.Count -eq 0) {
  Write-Host 'Generated files are already current.'
}

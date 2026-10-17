<#
.SYNOPSIS
Builds the monsterbox sound list from a folder of sound files, writes it into app.config.yaml, and
copies the files onto the MP3 module's SD card in the matching order.

.DESCRIPTION
The YX5300 plays a card by physical order: the file copied Nth is track N. The remote sends a
"file number" and the firmware plays track (file number + 1), so the Nth file copied must be the
sound with file number N - 1. This script makes the three things agree:

  1. It reads every .mp3 / .wav in -SourceFolder and decides the order (see -Order). Sounds are
     numbered 0, 1, 2, ... with no gaps; the creatures (names starting with "Creature") come last.
  2. It rewrites the "Sounds:" section of app.config.yaml (the list GET /sounds returns and the
     remote builds its buttons from). Everything else in the file is left alone.
  3. If -Destination is given, it copies the files to the card one at a time in number order as
     NNN_Name.mp3 (for example 007_Raven.mp3), so the name on the card matches the config.

The folder is the source of truth: a sound in the config but not in the folder is dropped (and
the numbers after it shift), and a new file in the folder is added. The script prints what moved.

Rules it enforces:
  * The card must be empty (and freshly quick-formatted FAT32). Deleting files from a card does not
    reliably reset the order, so an empty, freshly formatted card is the only safe starting point.
  * At most 255 sounds (file numbers 0-254).
  * No two files may have the same name (ignoring case, spaces, underscores, dashes and a leading
    NNN_ number), and a name cannot be empty.

How a sound's name is chosen: a name already in the config keeps its spelling ("Door creek" for
door_creek.mp3). A new file gets its file name without the extension and without a leading "NNN_",
with "_" and "-" turned into spaces and the first letter capitalised (evil_laugh.mp3 -> "Evil laugh").
Characters that Meadow's config parser can't take in a name (":" and "#") are removed.

.PARAMETER SourceFolder
Folder holding the .mp3 / .wav files (not searched recursively).

.PARAMETER Destination
The card's root, for example E:\ . Leave it out to only update the config.

.PARAMETER ConfigPath
Path to app.config.yaml. Defaults to the one in the monsterbox project.

.PARAMETER Order
Keep (default): sounds already in the config keep their order (so the remote's buttons don't move),
new files are added after them. Alphabetical: ignore the config and sort by name (numbers inside
names sort naturally, so "Creature 2" comes before "Creature 10"). Either way the creatures go last.

.PARAMETER AllowNonEmptyDestination
Skip the empty-card check. The order is only guaranteed on an empty card.

.EXAMPLE
.\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\ -WhatIf
Shows the new list, the config change and every file it would copy, and changes nothing.

.EXAMPLE
.\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\
Updates app.config.yaml and copies the files to the freshly formatted card at E:\.

.EXAMPLE
.\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds
Only updates app.config.yaml.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)] [string] $SourceFolder,
    [string] $Destination,
    [string] $ConfigPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'app.config.yaml'),
    [ValidateSet('Keep', 'Alphabetical')] [string] $Order = 'Keep',
    [switch] $AllowNonEmptyDestination
)

$ErrorActionPreference = 'Stop'
$MaxFileNumber = 254          # MP3Controller.MaxFileNumber: the module is sent number + 1 in one byte
$AudioExtensions = @('.mp3', '.wav')
$CreaturePrefix = 'Creature'  # SoundCatalogClient.CreaturePrefix: the remote puts these under CREATURES

function Get-SoundsFromConfig([string] $path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Config file not found: $path" }

    $sounds = @()
    $inSounds = $false
    foreach ($line in Get-Content -LiteralPath $path) {
        if ($line -match '^\s*#' -or $line.Trim() -eq '') { continue }

        if ($line -match '^\S') {                      # a top-level key
            $inSounds = ($line -match '^Sounds\s*:\s*(#.*)?$')
            continue
        }

        if ($inSounds -and $line -match '^\s+(?<name>[^:#]+?)\s*:\s*(?<number>\d+)\s*(#.*)?$') {
            $sounds += [pscustomobject]@{ Name = $Matches['name']; Number = [int]$Matches['number'] }
        }
    }
    return @($sounds | Sort-Object Number)
}

function ConvertTo-Key([string] $text) { return ($text.ToLowerInvariant() -replace '[^a-z0-9]', '') }

# "007_Raven" -> "Raven"; "evil_laugh" -> "Evil laugh"
function ConvertTo-DisplayName([string] $stem) {
    $name = $stem -replace '^\d{1,3}[_\- ]+', ''
    $name = ($name -replace '[_\-]+', ' ') -replace '[:#]', ''
    $name = ($name -replace '\s+', ' ').Trim()
    if ($name.Length -gt 0) { $name = $name.Substring(0, 1).ToUpperInvariant() + $name.Substring(1) }
    return $name
}

# sorts "Creature 2" before "Creature 10"
function ConvertTo-NaturalKey([string] $text) {
    return [regex]::Replace($text.ToLowerInvariant(), '\d+', { param($m) $m.Value.PadLeft(10, '0') })
}

function ConvertTo-FileName([string] $text) {
    $safe = ($text -replace '\s+', '_') -replace '[^A-Za-z0-9_\-]', ''
    if ($safe -eq '') { return 'Sound' }
    return $safe
}

# ---- read the folder and decide the order --------------------------------------------------------
if (-not (Test-Path -LiteralPath $SourceFolder -PathType Container)) { throw "Source folder not found: $SourceFolder" }
if ($Destination -and -not (Test-Path -LiteralPath $Destination -PathType Container)) { throw "Destination not found: $Destination" }

$existing = @(Get-SoundsFromConfig $ConfigPath)
$existingIndex = @{}
for ($i = 0; $i -lt $existing.Count; $i++) { $existingIndex[(ConvertTo-Key $existing[$i].Name)] = $i }

$files = @(Get-ChildItem -LiteralPath $SourceFolder -File | Where-Object { $AudioExtensions -contains $_.Extension.ToLowerInvariant() })
if ($files.Count -eq 0) { throw "No .mp3 or .wav files found in $SourceFolder" }

$entries = @()
foreach ($file in $files) {
    $key = ConvertTo-Key (ConvertTo-DisplayName $file.BaseName)
    $display = ConvertTo-DisplayName $file.BaseName
    if ($key -eq '') { throw "Cannot make a sound name from the file name '$($file.Name)'." }

    if ($existingIndex.ContainsKey($key)) {
        $display = $existing[$existingIndex[$key]].Name            # keep the spelling already in the config
        $position = $existingIndex[$key]
    } else {
        $position = [int]::MaxValue
    }

    $entries += [pscustomobject]@{
        File       = $file
        Key        = $key
        Name       = $display
        Position   = $position
        IsCreature = ($display.StartsWith($CreaturePrefix, [System.StringComparison]::OrdinalIgnoreCase))
    }
}

$duplicates = @($entries | Group-Object Key | Where-Object { $_.Count -gt 1 })
if ($duplicates.Count -gt 0) {
    $lines = $duplicates | ForEach-Object { "  " + (($_.Group | ForEach-Object { $_.File.Name }) -join ' / ') }
    throw ("Two files make the same sound name:`n" + ($lines -join "`n"))
}
if ($entries.Count -gt ($MaxFileNumber + 1)) { throw "Too many sounds ($($entries.Count)); the most the module supports is $($MaxFileNumber + 1)." }

if ($Order -eq 'Keep') {
    $sortKeys = @({ $_.IsCreature }, { $_.Position }, { ConvertTo-NaturalKey $_.Name })
} else {
    $sortKeys = @({ $_.IsCreature }, { ConvertTo-NaturalKey $_.Name })
}
$ordered = @($entries | Sort-Object -Property $sortKeys)

$plan = @()
for ($number = 0; $number -lt $ordered.Count; $number++) {
    $item = $ordered[$number]
    $plan += [pscustomobject]@{
        Number = $number
        Name   = $item.Name
        Source = $item.File.FullName
        Target = ('{0:000}_{1}{2}' -f $number, (ConvertTo-FileName $item.Name), $item.File.Extension.ToLowerInvariant())
    }
}

# ---- the card must be empty (checked before anything is changed) ------------------------------------
if ($Destination) {
    $onCard = @(Get-ChildItem -LiteralPath $Destination -Force | Where-Object { $_.Name -ne 'System Volume Information' -and $_.Name -ne '$RECYCLE.BIN' })
    if ($onCard.Count -gt 0 -and -not $AllowNonEmptyDestination) {
        throw "The card at $Destination is not empty ($($onCard.Count) item(s)). Quick-format it as FAT32 first so the copy order is the play order, or pass -AllowNonEmptyDestination. Nothing was changed."
    }
}

# ---- what changes compared with the current config ------------------------------------------------
$oldByKey = @{}
foreach ($sound in $existing) { $oldByKey[(ConvertTo-Key $sound.Name)] = $sound.Number }

Write-Host ''
Write-Host "Sounds (file number N is the (N + 1)th file copied to the card):"
foreach ($item in $plan) {
    $key = ConvertTo-Key $item.Name
    if (-not $oldByKey.ContainsKey($key)) { $change = 'new' }
    elseif ($oldByKey[$key] -ne $item.Number) { $change = "was $($oldByKey[$key])" }
    else { $change = '' }
    Write-Host ('  {0,3}  {1,-18} {2,-26} {3}' -f $item.Number, $item.Name, $item.Target, $change)
}

$newKeys = @{}
foreach ($item in $plan) { $newKeys[(ConvertTo-Key $item.Name)] = $true }
$removed = @($existing | Where-Object { -not $newKeys.ContainsKey((ConvertTo-Key $_.Name)) })
foreach ($sound in $removed) { Write-Warning "'$($sound.Name)' (was $($sound.Number)) is in the config but not in $SourceFolder, so it is removed." }

# ---- rewrite the Sounds section of the config -----------------------------------------------------
$raw = [System.IO.File]::ReadAllText($ConfigPath)
$newline = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }
$lines = New-Object System.Collections.Generic.List[string]
foreach ($l in ($raw -split "\r?\n")) { $lines.Add($l) }
if ($lines.Count -gt 0 -and $lines[$lines.Count - 1] -eq '') { $lines.RemoveAt($lines.Count - 1) }   # the final newline

$soundLines = @($plan | ForEach-Object { '    {0}: {1}' -f $_.Name, $_.Number })
$start = -1
for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '^Sounds\s*:\s*(#.*)?$') { $start = $i; break } }

if ($start -ge 0) {
    $end = $start + 1                                   # the section is the indented lines under "Sounds:"
    while ($end -lt $lines.Count -and $lines[$end] -match '^\s+\S') { $end++ }
    $lines.RemoveRange($start + 1, $end - $start - 1)
    $lines.InsertRange($start + 1, [string[]]$soundLines)
} else {
    $lines.Add('')
    $lines.Add('# The sounds on the MP3 module''s SD card: "Name: file number" (see tools\Copy-SoundsToCard.ps1).')
    $lines.Add('Sounds:')
    $lines.AddRange([string[]]$soundLines)
}
$newText = ($lines -join $newline) + $newline

if ($newText -eq $raw) {
    Write-Host ''
    Write-Host "app.config.yaml already matches; no change."
} elseif ($PSCmdlet.ShouldProcess($ConfigPath, 'Rewrite the Sounds section')) {
    [System.IO.File]::WriteAllText($ConfigPath, $newText, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ''
    Write-Host "Updated $ConfigPath"
    Write-Host ''
    Write-Host "The remote's built-in fallback list (SoundCatalogClient.Defaults) is separate. To match, use:"
    foreach ($item in $plan) { Write-Host ('            new SoundEntry({0}, "{1}"),' -f $item.Number, $item.Name) }
}

# ---- copy to the card, one file at a time in order --------------------------------------------------
if (-not $Destination) {
    Write-Host ''
    Write-Host 'No -Destination given, so nothing was copied to a card.'
    return
}

$copied = 0
foreach ($item in $plan) {
    $target = Join-Path $Destination $item.Target
    if ($PSCmdlet.ShouldProcess($target, "Copy $($item.Source)")) {
        Copy-Item -LiteralPath $item.Source -Destination $target
        if ((Get-Item -LiteralPath $target).Length -ne (Get-Item -LiteralPath $item.Source).Length) {
            throw "Copy of $($item.Source) to $target is not the same size; stopping."
        }
        $copied++
        Write-Host ('Copied {0}' -f $item.Target)
    }
}

# ask Windows to write the card's cache out before it is pulled (Windows only)
if ($copied -gt 0 -and (Get-Command Write-VolumeCache -ErrorAction SilentlyContinue) -and $Destination -match '^[A-Za-z]:') {
    try { Write-VolumeCache -DriveLetter $Destination.Substring(0, 1) } catch { Write-Warning "Could not flush the card: $($_.Exception.Message)" }
}

Write-Host ''
Write-Host "Copied $copied of $($plan.Count) file(s). Eject the card in Windows before removing it."

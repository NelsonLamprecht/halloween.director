<#
.SYNOPSIS
Copies the monsterbox sound files onto the MP3 module's SD card, one at a time, in file-number order.

.DESCRIPTION
The YX5300 plays a card by physical order: the file copied Nth is track N. The remote sends a
"file number" and the firmware plays track (file number + 1), so the Nth file copied must be the
sound with file number N - 1. This script reads the Sounds section of ..\app.config.yaml (the same
list GET /sounds returns), finds each sound in the source folder by name, and copies them to the
card in file-number order as NNN_Name.mp3 (for example 007_Raven.mp3), so the name on the card
matches the number in the config.

Rules it enforces:
  * The card must be empty (and freshly quick-formatted FAT32). Deleting files from a card does not
    reliably reset the order, so an empty, freshly formatted card is the only safe starting point.
  * File numbers should run 0, 1, 2, ... with no gaps. If one is missing the card order would
    shift, so either give -FillerFile (for example a short silent mp3), which is copied wherever a
    number is missing, or fix the numbers.
  * Every sound in the config must be found in the source folder before anything is copied.

How a source file is matched to a sound: its name (without extension) equals the sound's name
ignoring case, spaces, underscores and dashes ("Door creek" matches door_creek.mp3), or it already
starts with the 3-digit number and an underscore (007_anything.mp3).

.PARAMETER SourceFolder
Folder holding the original .mp3 / .wav files.

.PARAMETER Destination
The card's root, for example E:\

.PARAMETER ConfigPath
Path to app.config.yaml. Defaults to the one in the monsterbox project.

.PARAMETER FillerFile
A file copied for any file number missing from the config (only needed if the numbers have a gap).

.PARAMETER AllowNonEmptyDestination
Skip the empty-card check. The order is only guaranteed on an empty card.

.EXAMPLE
.\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\ -WhatIf

.EXAMPLE
.\Copy-SoundsToCard.ps1 -SourceFolder C:\Halloween\Sounds -Destination E:\
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)] [string] $SourceFolder,
    [Parameter(Mandatory = $true)] [string] $Destination,
    [string] $ConfigPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'app.config.yaml'),
    [string] $FillerFile,
    [switch] $AllowNonEmptyDestination
)

$ErrorActionPreference = 'Stop'
$MaxFileNumber = 254          # MP3Controller.MaxFileNumber: the module is sent number + 1 in one byte
$AudioExtensions = @('.mp3', '.wav')

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

        if ($inSounds) {
            if ($line -match '^\s+(?<name>[^:#]+?)\s*:\s*(?<number>\d+)\s*(#.*)?$') {
                $sounds += [pscustomobject]@{ Name = $Matches['name']; Number = [int]$Matches['number'] }
            } else {
                throw "Unrecognised line in the Sounds section (expected 'Name: number'): $line"
            }
        }
    }

    if ($sounds.Count -eq 0) { throw "No sounds found in a 'Sounds:' section of $path" }
    return ($sounds | Sort-Object Number)
}

function ConvertTo-Key([string] $text) { return ($text.ToLowerInvariant() -replace '[^a-z0-9]', '') }

function ConvertTo-SafeFileName([string] $text) {
    $safe = ($text -replace '\s+', '_') -replace '[^A-Za-z0-9_\-]', ''
    if ($safe -eq '') { return 'Sound' }
    return $safe
}

# ---- read and validate the config -------------------------------------------------------------
$sounds = @(Get-SoundsFromConfig $ConfigPath)

$duplicates = @($sounds | Group-Object Number | Where-Object { $_.Count -gt 1 })
if ($duplicates.Count -gt 0) {
    throw ('File number used more than once: ' + (($duplicates | ForEach-Object { $_.Name }) -join ', '))
}
$tooBig = @($sounds | Where-Object { $_.Number -gt $MaxFileNumber })
if ($tooBig.Count -gt 0) {
    throw "File numbers above $MaxFileNumber are not supported: $(($tooBig | ForEach-Object { $_.Name }) -join ', ')"
}

if (-not (Test-Path -LiteralPath $SourceFolder -PathType Container)) { throw "Source folder not found: $SourceFolder" }
if (-not (Test-Path -LiteralPath $Destination -PathType Container)) { throw "Destination not found: $Destination" }

# ---- build the copy plan: one entry per file number, 0..highest --------------------------------
$sourceFiles = @(Get-ChildItem -LiteralPath $SourceFolder -File | Where-Object { $AudioExtensions -contains $_.Extension.ToLowerInvariant() })
$plan = @()
$problems = @()
$highest = ($sounds | Measure-Object Number -Maximum).Maximum

for ($number = 0; $number -le $highest; $number++) {
    $sound = $sounds | Where-Object { $_.Number -eq $number }
    $prefix = '{0:000}' -f $number

    if ($null -eq $sound) {
        if ([string]::IsNullOrEmpty($FillerFile)) {
            $problems += "File number $number is not in the config, and the card order needs a file there. Fix the numbers, or pass -FillerFile (for example a short silent mp3)."
            continue
        }
        if (-not (Test-Path -LiteralPath $FillerFile -PathType Leaf)) { throw "Filler file not found: $FillerFile" }
        $filler = Get-Item -LiteralPath $FillerFile
        $plan += [pscustomobject]@{ Number = $number; Name = '(filler)'; Source = $filler.FullName; Target = ('{0}_Filler{1}' -f $prefix, $filler.Extension.ToLowerInvariant()) }
        continue
    }

    $key = ConvertTo-Key $sound.Name
    $found = @($sourceFiles | Where-Object {
        (ConvertTo-Key $_.BaseName) -eq $key -or $_.Name -like ($prefix + '_*')
    })

    if ($found.Count -eq 0) {
        $problems += "No file found in $SourceFolder for '$($sound.Name)' (number $number). Name it '$($sound.Name).mp3' or '${prefix}_something.mp3'."
        continue
    }
    if ($found.Count -gt 1) {
        $problems += "More than one file matches '$($sound.Name)' (number $number): $(($found | ForEach-Object { $_.Name }) -join ', ')"
        continue
    }

    $plan += [pscustomobject]@{
        Number = $number
        Name   = $sound.Name
        Source = $found[0].FullName
        Target = ('{0}_{1}{2}' -f $prefix, (ConvertTo-SafeFileName $sound.Name), $found[0].Extension.ToLowerInvariant())
    }
}

if ($problems.Count -gt 0) {
    $problems | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "Nothing was copied: fix the $($problems.Count) problem(s) above first."
}

# ---- the card must be empty --------------------------------------------------------------------
$existing = @(Get-ChildItem -LiteralPath $Destination -Force | Where-Object { $_.Name -ne 'System Volume Information' -and $_.Name -ne '$RECYCLE.BIN' })
if ($existing.Count -gt 0 -and -not $AllowNonEmptyDestination) {
    throw "The card at $Destination is not empty ($($existing.Count) item(s)). Quick-format it as FAT32 first so the copy order is the play order, or pass -AllowNonEmptyDestination."
}

# ---- show the plan, then copy one file at a time in order -----------------------------------------
Write-Host ''
Write-Host 'Copy plan (the card plays by copy order: file number N is the (N + 1)th file copied):'
foreach ($item in $plan) {
    Write-Host ('  track {0,3}  file #{1,3}  {2,-14} -> {3}' -f ($item.Number + 1), $item.Number, $item.Name, $item.Target)
}
Write-Host ''

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

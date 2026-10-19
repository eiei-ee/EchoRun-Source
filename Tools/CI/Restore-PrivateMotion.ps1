[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Workspace,
    [string]$SourceRoot = $env:ECHORUN_PRIVATE_MOTION_ROOT,
    [string]$ManifestPath = (Join-Path $PSScriptRoot 'private-motion-manifest.json')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-NoReparsePoint([string]$Path) {
    $entry = Get-Item -LiteralPath $Path -Force
    while ($null -ne $entry) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Private motion paths must not traverse links or junctions: $($entry.FullName)"
        }
        $entry = if ($entry -is [IO.DirectoryInfo]) { $entry.Parent } else { $entry.Directory }
    }
}
function Is-InDirectory([string]$Path, [string]$Directory) {
    return $Path.Equals($Directory, [StringComparison]::OrdinalIgnoreCase) -or
        $Path.StartsWith($Directory.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)
}

if ([string]::IsNullOrWhiteSpace($SourceRoot)) {
    throw 'Set ECHORUN_PRIVATE_MOTION_ROOT to a licensed source bundle outside the runner workspace. See docs/RepositoryWorkflow.md.'
}
$project = (Resolve-Path -LiteralPath $Workspace).Path.TrimEnd('\', '/')
$source = (Resolve-Path -LiteralPath $SourceRoot).Path.TrimEnd('\', '/')
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -PathType Leaf)) {
    throw 'Workspace is not a Tuanjie project.'
}
Assert-NoReparsePoint $project
Assert-NoReparsePoint $source
if (Is-InDirectory $source $project) { throw 'Private source bundle must be outside the project checkout.' }
if ($env:GITHUB_WORKSPACE -and (Is-InDirectory $source ([IO.Path]::GetFullPath($env:GITHUB_WORKSPACE)))) {
    throw 'Private source bundle must be outside GITHUB_WORKSPACE so checkout cannot clean it.'
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
$expected = @('HumanM@Jump01.fbx', 'HumanM_Model.fbx', 'Human Body Full Mask.mask')
if ($manifest.schemaVersion -ne 1 -or @($manifest.files).Count -ne 3 -or
    @(Compare-Object $expected @($manifest.files.name)).Count -ne 0) {
    throw 'Manifest must contain exactly the three approved motion source filenames.'
}
$destination = Join-Path $project 'Assets/Animations/HumanMotion/KevinBasicPrivate'
$ancestor = $destination
while (-not (Test-Path -LiteralPath $ancestor)) { $ancestor = Split-Path -Parent $ancestor }
Assert-NoReparsePoint $ancestor
$copyPlan = @()
foreach ($asset in $manifest.files) {
    if ($asset.sha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw "Invalid SHA-256 for $($asset.name)." }
    foreach ($name in @($asset.name, ($asset.name + '.meta'))) {
        $inputFile = Join-Path $source $name
        if (-not (Test-Path -LiteralPath $inputFile -PathType Leaf)) { throw "Required licensed dependency missing: $name" }
        Assert-NoReparsePoint $inputFile
        $hash = (Get-FileHash -LiteralPath $inputFile -Algorithm SHA256).Hash
        if ($name -eq $asset.name -and $hash -ne $asset.sha256) { throw "Private source version mismatch: $name" }
        if ($name.EndsWith('.meta')) {
            $meta = Get-Content -LiteralPath $inputFile -Raw
            if ($meta -notmatch '(?m)^guid:\s*\S+' -or $meta -notmatch '(?m)^(ModelImporter|NativeFormatImporter):') {
                throw "Original importer metadata is missing or invalid: $name"
            }
        }
        $outputFile = Join-Path $destination $name
        if (Test-Path -LiteralPath $outputFile) {
            Assert-NoReparsePoint $outputFile
            if ((Get-FileHash -LiteralPath $outputFile -Algorithm SHA256).Hash -ne $hash) {
                throw "Refusing to overwrite different local private data: $outputFile"
            }
        } else {
            $copyPlan += [pscustomobject]@{ Source = $inputFile; Destination = $outputFile; Hash = $hash }
        }
    }
}
# Validate every input and destination before the first write. Never move/delete the source.
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($copy in $copyPlan) {
    Copy-Item -LiteralPath $copy.Source -Destination $copy.Destination
    if ((Get-FileHash -LiteralPath $copy.Destination -Algorithm SHA256).Hash -ne $copy.Hash) {
        throw 'Copied private dependency did not retain its SHA-256.'
    }
}
Write-Host 'Verified and restored three private source assets with importer metadata. Generated clips will be rebuilt locally.'

# Synthetic files only: this check never downloads/copies licensed assets or launches an editor.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$scriptPath = Join-Path $PSScriptRoot 'Restore-PrivateMotion.ps1'
$parsed = 0
foreach ($script in Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.ps1') {
    $tokens = $null
    $errors = $null
    [void][Management.Automation.Language.Parser]::ParseFile($script.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count) { throw ($errors | Out-String) }
    $parsed++
}
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('EchoRunCIScripts-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
$passed = 0
function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}
function New-Fixture([string]$Name) {
    $folder = Join-Path $fixtureRoot $Name
    $source = Join-Path $folder 'external-source'
    $project = Join-Path $folder 'checkout with spaces'
    New-Item -ItemType Directory -Path $source, (Join-Path $project 'ProjectSettings') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -Value 'fixture only'
    $files = foreach ($name in @('HumanM@Jump01.fbx', 'HumanM_Model.fbx', 'Human Body Full Mask.mask')) {
        Set-Content -LiteralPath (Join-Path $source $name) -Value ('synthetic ' + $name)
        Set-Content -LiteralPath (Join-Path $source ($name + '.meta')) -Value "guid: 1234`nModelImporter:"
        [pscustomobject]@{ name = $name; sha256 = (Get-FileHash -LiteralPath (Join-Path $source $name)).Hash }
    }
    $manifest = Join-Path $folder 'fixture-manifest.json'
    @{ schemaVersion = 1; files = @($files) } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifest
    return @{ Workspace = $project; SourceRoot = $source; ManifestPath = $manifest }
}
function Must-Reject([hashtable]$Fixture, [string]$Expected) {
    $message = ''
    try { & $scriptPath @Fixture } catch { $message = $_.Exception.Message }
    Assert ($message -like "*$Expected*") "Expected rejection '$Expected'; got '$message'."
}

$good = New-Fixture 'complete'
Set-Content -LiteralPath (Join-Path $good.SourceRoot 'unrelated-private-file.txt') -Value 'must not copy'
$before = @(Get-ChildItem -LiteralPath $good.SourceRoot -File | Get-FileHash | Select-Object -ExpandProperty Hash)
& $scriptPath @good
$destination = Join-Path $good.Workspace 'Assets/Animations/HumanMotion/KevinBasicPrivate'
Assert (@(Get-ChildItem -LiteralPath $destination -File).Count -eq 6) 'Restore must copy exactly six allowlisted files.'
$passed++
& $scriptPath @good
Assert (@(Get-ChildItem -LiteralPath $destination -File).Count -eq 6) 'Second restoration should be idempotent.'
$passed++
$after = @(Get-ChildItem -LiteralPath $good.SourceRoot -File | Get-FileHash | Select-Object -ExpandProperty Hash)
Assert (@(Compare-Object $before $after).Count -eq 0) 'Restoration modified the external source bundle.'
$passed++

$badHash = New-Fixture 'bad-hash'
Set-Content -LiteralPath (Join-Path $badHash.SourceRoot 'HumanM_Model.fbx') -Value 'changed'
Must-Reject $badHash 'version mismatch'
Assert (-not (Test-Path -LiteralPath (Join-Path $badHash.Workspace 'Assets'))) 'Validation failure must happen before copying any file.'
$passed++
$missing = New-Fixture 'missing-meta'
Remove-Item -LiteralPath (Join-Path $missing.SourceRoot 'HumanM_Model.fbx.meta')
Must-Reject $missing 'dependency missing'
$passed++
$badMeta = New-Fixture 'bad-meta'
Set-Content -LiteralPath (Join-Path $badMeta.SourceRoot 'HumanM_Model.fbx.meta') -Value 'no importer'
Must-Reject $badMeta 'metadata is missing or invalid'
$passed++
$escape = New-Fixture 'traversal'
$manifest = Get-Content -LiteralPath $escape.ManifestPath -Raw | ConvertFrom-Json
$manifest.files[0].name = '../outside.fbx'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $escape.ManifestPath
Must-Reject $escape 'exactly the three approved'
$passed++
$nested = New-Fixture 'nested-source'
$nested.SourceRoot = $nested.Workspace
Must-Reject $nested 'outside the project'
$passed++
Set-Content -LiteralPath (Join-Path $destination 'HumanM_Model.fbx') -Value 'local data to preserve'
Must-Reject $good 'Refusing to overwrite'
Assert ((Get-Content -LiteralPath (Join-Path $destination 'HumanM_Model.fbx') -Raw).Trim() -eq 'local data to preserve') 'Existing private data was overwritten.'
$passed++

Write-Host "Parsed $parsed PowerShell scripts; $passed restore safety checks passed. Synthetic fixtures: $fixtureRoot"

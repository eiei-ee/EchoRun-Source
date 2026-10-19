[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet('Prepare', 'EditMode', 'PlayMode', 'WebGL', 'Windows', 'Android')][string]$Mode,
    [Parameter(Mandatory = $true)][string]$Workspace,
    [string]$Editor = $env:TUANJIE_EDITOR
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = (Resolve-Path -LiteralPath $Workspace).Path
if (-not (Test-Path -LiteralPath $Editor -PathType Leaf)) { throw "Tuanjie Editor not found: $Editor" }
if (-not (Test-Path -LiteralPath (Join-Path $project 'ProjectSettings/ProjectVersion.txt') -PathType Leaf)) {
    throw 'Workspace is not a Tuanjie project.'
}
if (Get-Process -Name Tuanjie, Unity -ErrorAction SilentlyContinue) {
    throw 'An editor is already running on this runner. CI requires a dedicated editor session.'
}
$artifactDirectory = Join-Path $project 'artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null

function Quote-Argument([string]$Value) {
    if ($Value.IndexOf('"') -ge 0 -or $Value.IndexOf("`n") -ge 0 -or $Value.EndsWith('\')) {
        throw 'Unsupported editor argument. Use a full path without a trailing slash.'
    }
    return '"' + $Value + '"'
}
function Invoke-Editor([string]$Label, [string[]]$Extra, [string]$Marker = '') {
    $logPath = Join-Path $artifactDirectory ($Label + '.log')
    # Never accept old evidence from a previous invocation.
    if (Test-Path -LiteralPath $logPath) { throw "CI log already exists; use a clean checkout: $logPath" }
    $arguments = @('-batchmode', '-projectPath', $project, '-logFile', $logPath) + $Extra
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $Editor
    $info.WorkingDirectory = $project
    $info.UseShellExecute = $false
    $info.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $info.CreateNoWindow = $true
    $info.EnvironmentVariables['ALLUSERSPROFILE'] = [Environment]::GetFolderPath('CommonApplicationData')
    $info.Arguments = ($arguments | ForEach-Object { Quote-Argument $_ }) -join ' '
    $process = [Diagnostics.Process]::Start($info)
    $process.WaitForExit()
    if (Test-Path -LiteralPath $logPath) {
        if ($Mode -eq 'Prepare') {
            Write-Output "$Label finished; its source-review log remains local."
        } else {
            # Actions console output is public too, independently of artifact uploads.
            Get-Content -LiteralPath $logPath -Tail 100 | Where-Object {
                $_ -notmatch '(?i)Licensing::|LicenseService|Authorization|access[_-]?token|secret[_-]?key|password|credential'
            }
        }
    }
    if ($process.ExitCode -ne 0) { throw "$Label failed with exit code $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $logPath)) { throw "$Label did not produce its log." }
    if ($Marker -and -not (Select-String -LiteralPath $logPath -SimpleMatch $Marker -Quiet)) {
        throw "$Label exited without its completion marker: $Marker"
    }
}

if ($Mode -eq 'Prepare') {
    & (Join-Path $PSScriptRoot 'Restore-PrivateMotion.ps1') -Workspace $project
    Invoke-Editor 'prepare-source' @('-quit', '-executeMethod', 'RunnerBasicMotionReview.InspectCandidates') 'BASIC_SOURCE_REVIEW_CAPTURED'
    Invoke-Editor 'prepare-clips' @('-quit', '-executeMethod', 'RunnerFlowJumpAuthor.BuildFittedCandidates') 'FLOW_LANDING_CONTACT_FITTED'
    Invoke-Editor 'prepare-bindings' @('-quit', '-executeMethod', 'RunnerFlowPreviewInstaller.InstallReviewedLocal') 'RUNNER_FLOW_LOCAL_PREVIEW_INSTALLED'
    foreach ($clip in @('RunnerFlowFitJump', 'RunnerFlowFitLand')) {
        if (-not (Test-Path -LiteralPath (Join-Path $project "Assets/Animations/HumanMotion/KevinBasicPrivate/Generated/$clip.anim") -PathType Leaf)) {
            throw "Preparation did not generate $clip."
        }
    }
} elseif ($Mode -in @('EditMode', 'PlayMode')) {
    $resultPath = Join-Path $artifactDirectory ($Mode + '-results.xml')
    if (Test-Path -LiteralPath $resultPath) { throw 'Old test result exists; use a clean CI checkout.' }
    # GPU rendering is needed by source review and the PlayMode visual checks: no -nographics.
    Invoke-Editor $Mode @('-runTests', '-testPlatform', $Mode, '-testResults', $resultPath)
    if (-not (Test-Path -LiteralPath $resultPath)) { throw 'Editor did not produce test XML.' }
    [xml]$results = Get-Content -LiteralPath $resultPath -Raw
    $run = $results.'test-run'
    if ($null -eq $run -or [int]$run.total -le 0 -or [int]$run.failed -ne 0 -or $run.result -ne 'Passed') {
        throw "Test suite is not green. Inspect $resultPath."
    }
} else {
    Invoke-Editor ('build-' + $Mode) @('-quit', '-executeMethod', ('BuildConfig.Build' + $Mode))
    $outputDirectory = Join-Path $project ('Builds/' + $Mode)
    if (-not (Test-Path -LiteralPath $outputDirectory) -or
        @(Get-ChildItem -LiteralPath $outputDirectory -File -Recurse).Count -eq 0) {
        throw "Build output is missing or empty: $outputDirectory"
    }
}

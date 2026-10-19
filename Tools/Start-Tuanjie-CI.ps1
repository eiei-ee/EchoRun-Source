$ErrorActionPreference = "Stop"

$runnerDirectory = "C:\actions-runner-templerun"
$runnerCommand = Join-Path $runnerDirectory "run.cmd"

if (-not (Test-Path -LiteralPath $runnerCommand)) {
    throw "TempleRun GitHub Runner is not installed at $runnerDirectory."
}

Write-Host "Starting the TempleRun Tuanjie CI runner..."
Write-Host "Keep this window open while CI is running. Press Ctrl+C to stop it."

Push-Location $runnerDirectory
try {
    & $runnerCommand
}
finally {
    Pop-Location
}

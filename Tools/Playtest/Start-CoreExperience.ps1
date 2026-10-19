param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[DS][0-9]{2}$')]
    [string]$Participant
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$suiteRoot = Join-Path $projectRoot 'TestResults/CoreExperienceV1'
$playerPath = Join-Path $suiteRoot 'Windows/EchoRun.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw "Build missing: $playerPath" }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$evidenceRoot = Join-Path $suiteRoot "Participants/$Participant/$stamp"
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
Write-Host '使用独立试玩档。正式开始前确认已重置学习数据，首页为“开始第一局”。'
Write-Host '两局之间不要重置；不启用固定身份。每局结束自动导出，实际路径见 Player.log。'
Write-Host "本次日志：$evidenceRoot"
& $playerPath '-echo-playtest-export' '-screen-width' '1920' '-screen-height' '1080' '-screen-fullscreen' '0' '-logFile' (Join-Path $evidenceRoot 'Player.log')

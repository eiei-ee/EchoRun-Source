param([Parameter(Mandatory = $true)][string]$Workspace)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path -LiteralPath $Workspace).Path
$source = Join-Path $project 'artifacts'
$destination = Join-Path $project 'artifacts-public'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($file in Get-ChildItem -LiteralPath $source -File) {
    $target = Join-Path $destination $file.Name
    if ($file.Name -like '*-results.xml') {
        Copy-Item -LiteralPath $file.FullName -Destination $target
    } elseif ($file.Name -match '^(EditMode|PlayMode|build-[A-Za-z]+)\.log$') {
        # Keep preparation/source-review logs local. Vendor licensing protocol
        # messages include account/license identifiers and authorization fields.
        $lines = Get-Content -LiteralPath $file.FullName | Where-Object {
            $_ -notmatch '(?i)Licensing::|LicenseService|Authorization|access[_-]?token|secret[_-]?key|password|credential'
        }
        $lines | Set-Content -LiteralPath $target -Encoding UTF8
    }
}
Write-Output 'Exported test XML and filtered runtime/build logs; preparation logs remain local.'

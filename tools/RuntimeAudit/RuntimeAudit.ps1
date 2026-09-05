param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
    [string]$QaDatabase = '',
    [string]$CaptureDate = ''
)

$ErrorActionPreference = 'Stop'
$chinaZone = [TimeZoneInfo]::FindSystemTimeZoneById('China Standard Time')
$captureDateValue = if ([string]::IsNullOrWhiteSpace($CaptureDate)) {
    [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, $chinaZone).ToString('yyyy-MM-dd')
} else {
    $CaptureDate
}
$exe = Join-Path $ProjectRoot 'publish\QimiaoDaily.exe'
$screenshotRoot = Join-Path $ProjectRoot 'artifacts\screenshots\final'
$captureRoot = Join-Path $ProjectRoot 'artifacts\phase-original-audit-runtime'
if ([string]::IsNullOrWhiteSpace($QaDatabase)) {
    $QaDatabase = Join-Path $ProjectRoot 'artifacts\phase-final-qa\data\qimiao.db'
}

if (-not (Test-Path -LiteralPath $exe)) { throw "发布物不存在：$exe" }
if (-not (Test-Path -LiteralPath $QaDatabase)) { throw "QA 数据库不存在：$QaDatabase" }
if (-not [System.IO.Path]::GetFullPath($captureRoot).StartsWith([System.IO.Path]::GetFullPath((Join-Path $ProjectRoot 'artifacts')), [StringComparison]::OrdinalIgnoreCase)) {
    throw "捕获根目录超出 artifacts 范围：$captureRoot"
}
$exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash

New-Item -ItemType Directory -Force -Path $screenshotRoot, $captureRoot | Out-Null

$captures = @(
    @{ Name = '01-overview-final.png'; Page = '概览' },
    @{ Name = '02-game-final.png'; Page = '游戏活动' },
    @{ Name = '03-game-time.png'; Page = '游戏活动' },
    @{ Name = '04-game-evidence.png'; Page = '游戏活动'; Evidence = $true },
    @{ Name = '05-game-refresh.png'; Page = '游戏活动' },
    @{ Name = '06-artwork-final.png'; Page = '美图分享' },
    @{ Name = '07-birthday-final.png'; Calendar = $true; Birthday = $true },
    @{ Name = '08-calendar-final.png'; Calendar = $true },
    @{ Name = '09-bgi-final.png'; Page = 'BGI' },
    @{ Name = '10-report-final.png'; Page = '概览' },
    @{ Name = '11-source-health-final.png'; Page = '来源健康' },
    @{ Name = '12-scheduler-final.png'; Page = '任务调度' },
    @{ Name = '13-settings-final.png'; Page = '设置' },
    @{ Name = '14-publish-final.png'; Page = '概览' }
)

$results = [System.Collections.Generic.List[object]]::new()
try {
foreach ($capture in $captures) {
    $safeName = [System.IO.Path]::GetFileNameWithoutExtension($capture.Name)
    $dataRoot = Join-Path $captureRoot $safeName
    if (Test-Path -LiteralPath $dataRoot) { Remove-Item -LiteralPath $dataRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Join-Path $dataRoot 'data') | Out-Null
    Copy-Item -LiteralPath $QaDatabase -Destination (Join-Path $dataRoot 'data\qimiao.db')

    $target = Join-Path $screenshotRoot $capture.Name
    if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
    $startedAt = Get-Date
    $env:QIMIAO_DATA_ROOT = $dataRoot
    $env:QIMIAO_CAPTURE_PATH = $target
    $env:QIMIAO_CAPTURE_DATE = $captureDateValue
    Remove-Item Env:QIMIAO_CAPTURE_PAGE, Env:QIMIAO_CAPTURE_CALENDAR, Env:QIMIAO_CAPTURE_BIRTHDAY, Env:QIMIAO_CAPTURE_EVIDENCE, Env:QIMIAO_CAPTURE_EVIDENCE_TITLE -ErrorAction SilentlyContinue
    if ($capture.Calendar) {
        $env:QIMIAO_CAPTURE_CALENDAR = '1'
        if ($capture.Birthday) { $env:QIMIAO_CAPTURE_BIRTHDAY = '1' }
    } else {
        # Keep page selection independent of the host PowerShell code page.
        $env:QIMIAO_CAPTURE_PAGE = switch -Regex ($capture.Name) {
            '^01-' { 'overview'; break }
            '^0[2-5]-' { 'game'; break }
            '^06-' { 'artwork'; break }
            '^09-' { 'bgi'; break }
            '^10-' { 'report'; break }
            '^11-' { 'source-health'; break }
            '^12-' { 'scheduler'; break }
            '^13-' { 'settings'; break }
            '^14-' { 'overview'; break }
            default { $capture.Page }
        }
        if ($capture.Evidence) { $env:QIMIAO_CAPTURE_EVIDENCE = '1' }
    }

    $process = Start-Process -FilePath $exe -PassThru
    $fresh = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        Start-Sleep -Milliseconds 500
        if (Test-Path -LiteralPath $target) {
            $file = Get-Item -LiteralPath $target
            if ($file.Length -gt 1024 -and $file.LastWriteTime -gt $startedAt) { $fresh = $true; break }
        }
    }
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id -Force }
    try { $process.WaitForExit(5000) } catch { }
    if (-not $fresh) { throw "截图未在限定时间内生成：$($capture.Name)" }
    $hash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    $results.Add([pscustomobject]@{
        Name = $capture.Name
        Bytes = (Get-Item -LiteralPath $target).Length
        Sha256 = $hash
        DataRoot = $dataRoot
        Executable = $exe
        ExecutableSha256 = $exeHash
        QaDatabase = $QaDatabase
        CapturedAt = (Get-Item -LiteralPath $target).LastWriteTime.ToString('o')
    })
}
}
finally {
Remove-Item Env:QIMIAO_DATA_ROOT, Env:QIMIAO_UI_DEMO, Env:QIMIAO_CAPTURE_PATH, Env:QIMIAO_CAPTURE_DATE, Env:QIMIAO_CAPTURE_PAGE, Env:QIMIAO_CAPTURE_CALENDAR, Env:QIMIAO_CAPTURE_BIRTHDAY, Env:QIMIAO_CAPTURE_EVIDENCE, Env:QIMIAO_CAPTURE_EVIDENCE_TITLE -ErrorAction SilentlyContinue
}
$manifestPath = Join-Path $captureRoot 'runtime-manifest.json'
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
$results | ForEach-Object { "$($_.Sha256)  $($_.Name)" } | Set-Content -LiteralPath (Join-Path $captureRoot 'SHA256SUMS.txt') -Encoding ASCII
Write-Output "Manifest=$manifestPath"
$results | Format-Table Name, Bytes, Sha256 -AutoSize

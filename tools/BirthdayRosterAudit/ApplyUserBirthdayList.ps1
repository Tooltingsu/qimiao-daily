param(
    [string]$Existing = "$(Join-Path $PSScriptRoot '../../artifacts/manual-data-pivot/birthday-cover-import.json')",
    [string]$Output = "$(Join-Path $PSScriptRoot '../../artifacts/manual-data-pivot/birthday-user-confirmed-import-20260820.json')",
    [string]$UnknownOutput = "$(Join-Path $PSScriptRoot '../../artifacts/birthday-roster-review-20260820/birthday-user-unknown-review.json')"
)
$ErrorActionPreference = 'Stop'

function ParseKnown([string]$text) {
    foreach ($line in ($text -split "`n")) {
        $line = $line.Trim(); if (!$line) { continue }
        $p = $line -split '\|', 4
        [pscustomobject]@{ Game=$p[0]; Character=$p[1]; Month=[int]$p[2]; Day=[int]$p[3]; Aliases=$p[1] }
    }
}
function ParseUnknown([string]$text) {
    foreach ($line in ($text -split "`n")) {
        $line = $line.Trim(); if (!$line) { continue }
        $p = $line -split '\|', 3
        [pscustomobject]@{ Game=$p[0]; Character=$p[1]; Aliases=$p[2]; Status='UNKNOWN/PENDING_REVIEW' }
    }
}

$genshin = ParseKnown @'
GENSHIN|塔利雅|5|25
GENSHIN|菈乌玛|3|1
GENSHIN|奈芙尔|5|9
GENSHIN|雅珂达|1|5
GENSHIN|哥伦比娅|3|7
GENSHIN|奥黛塔|2|20
GENSHIN|阿罗夏|2|9
GENSHIN|布伦妮|11|20
GENSHIN|莉奈娅|5|23
'@

$hi3 = ParseKnown @'
HI3|比安卡·幽兰黛尔·阿塔吉娜|1|1
HI3|符华|2|9
HI3|丽塔·洛丝薇瑟|3|1
HI3|德丽莎·阿波卡利斯|3|28
HI3|李素裳|4|3
HI3|雷电芽衣|4|13
HI3|梅比乌斯|4|30
HI3|维尔薇|5|5
HI3|阿波尼亚|5|25
HI3|菲谢尔|5|27
HI3|无量塔姬子|6|11
HI3|娜塔莎·希奥拉（渡鸦）|6|19
HI3|西琳|6|23
HI3|帕朵菲莉丝|7|11
HI3|八重樱|7|22
HI3|萝莎莉娅·阿琳|7|6
HI3|莉莉娅·阿琳|7|6
HI3|布洛妮娅·扎伊切克|8|18
HI3|卡萝尔·佩珀|9|23
HI3|希儿·芙乐艾|10|18
HI3|爱衣·休伯利安 Λ|10|24
HI3|伊甸|10|31
HI3|爱莉希雅|11|11
HI3|格蕾修|11|28
HI3|琪亚娜·卡斯兰娜|12|7
HI3|普罗米修斯|12|25
HI3|卡莲·卡斯兰娜|6|21
'@

$nte = ParseKnown @'
NTE|卡厄斯|1|1
NTE|翳|1|10
NTE|达芙蒂尔|1|19
NTE|小吱|2|28
NTE|海月|3|9
NTE|哈尼娅|3|27
NTE|薄荷|6|1
NTE|安魂曲|6|26
NTE|九原|7|24
NTE|真红|8|13
NTE|娜娜莉|8|20
NTE|哈索尔|8|29
NTE|阿德勒|9|25
NTE|埃德嘉|10|7
NTE|法蒂娅|10|31
NTE|早雾|11|7
NTE|残虹|11|13
NTE|白藏|11|23
NTE|浔|12|20
NTE|伊洛伊|12|21
'@

$unknown = ParseUnknown @'
GENSHIN|杜林|Durin
GENSHIN|法尔伽|Varka
GENSHIN|莉奈娅|Linnea
'@

$old = Get-Content -Raw -Encoding UTF8 $Existing | ConvertFrom-Json
$rows = [System.Collections.Generic.List[object]]::new()
foreach ($row in @($old.birthdays | Where-Object { $_.game -eq 'GENSHIN' })) { $rows.Add($row) }
foreach ($row in @($genshin)) {
    $existingRow = @($rows | Where-Object { $_.game -eq $row.Game -and ($_.character -eq $row.Character -or $_.aliases -eq $row.Character) }) | Select-Object -First 1
    if ($existingRow) { $existingRow.character=$row.Character; $existingRow.month=$row.Month; $existingRow.day=$row.Day; $existingRow.aliases = if ($existingRow.aliases) { $existingRow.aliases } else { $row.Aliases }; $existingRow.notes='用户确认生日清单 2026-08-20' }
    else { $rows.Add([pscustomobject]@{ id="birthday-$($row.Game.ToLower())-$($row.Character)"; game=$row.Game; character=$row.Character; month=$row.Month; day=$row.Day; aliases=$row.Aliases; notes='用户确认生日清单 2026-08-20' }) }
}
foreach ($row in @($hi3 + $nte)) { $rows.Add([pscustomobject]@{ id="birthday-$($row.Game.ToLower())-$($row.Character)"; game=$row.Game; character=$row.Character; month=$row.Month; day=$row.Day; aliases=$row.Aliases; notes='用户确认生日清单 2026-08-20' }) }
$payload = [ordered]@{ schemaVersion=1; sourceName='用户确认生日清单 2026-08-20'; events=@(); banners=@(); versions=@(); birthdays=@($rows); anniversaries=@() }
$payload | ConvertTo-Json -Depth 8 | Set-Content -Encoding UTF8 $Output
$unknown | ConvertTo-Json -Depth 5 | Out-File -Encoding UTF8 $UnknownOutput
Write-Output ('known={0}; unknown={1}; output={2}' -f $rows.Count,$unknown.Count,$Output)

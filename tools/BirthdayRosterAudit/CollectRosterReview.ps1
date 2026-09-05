param(
    [string]$DatabaseRosterJson = "$(Join-Path $PSScriptRoot '../../artifacts/birthday-roster-review-20260820/birthday-roster.json')",
    [string]$OutputDirectory = "$(Join-Path $PSScriptRoot '../../artifacts/birthday-roster-review-20260820')"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$utf8 = New-Object System.Text.UTF8Encoding($false)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Get-Json($url) {
    $client = [System.Net.Http.HttpClient]::new()
    try {
        $bytes = $client.GetByteArrayAsync($url).GetAwaiter().GetResult()
        return ([System.Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json)
    } finally { $client.Dispose() }
}

function Normalize($value) {
    if ($null -eq $value) { return '' }
    return (($value.ToString()).Trim() -replace '\s+', ' ')
}

$parsedBirthdayRows = Get-Content -Raw $DatabaseRosterJson | ConvertFrom-Json
$birthdayRows = @($parsedBirthdayRows | ForEach-Object { $_ })
$birthdayByName = @{}
foreach ($row in $birthdayRows) {
    $keys = @($row.Character, $row.ChineseName) + (($row.Aliases -split '[,;|]') | ForEach-Object { $_.Trim() })
    foreach ($key in $keys) {
        $k = (Normalize $key).ToLowerInvariant()
        if ($k -and -not $birthdayByName.ContainsKey($row.Franchise + '|' + $k)) { $birthdayByName[$row.Franchise + '|' + $k] = $row }
    }
}

function Find-Birthday([string]$franchise, [string[]]$names) {
    foreach ($name in @($names)) {
        $key = $franchise + '|' + (Normalize $name).ToLowerInvariant()
        if ($birthdayByName.ContainsKey($key)) { return $birthdayByName[$key] }
    }
    return $null
}

$records = [System.Collections.Generic.List[object]]::new()

# Genshin roster: public Playable Characters category. This is a roster source,
# not a birthday source; birthday evidence is joined from the local review DB.
$genshinUrl = 'https://genshin-impact.fandom.com/api.php?action=query&list=categorymembers&cmtitle=Category:Playable_Characters&cmlimit=500&format=json'
$genshin = Get-Json $genshinUrl
foreach ($member in @($genshin.query.categorymembers)) {
    $english = Normalize $member.title
    if (-not $english -or $english.StartsWith('Category:', [System.StringComparison]::OrdinalIgnoreCase)) { continue }
    $birthday = Find-Birthday 'GENSHIN' @($english)
    if ($birthday -is [array]) { $birthday = $birthday[0] }
    if ($birthday) {
        $display = if ($birthday.ChineseName) { $birthday.ChineseName } else { $birthday.Character }
        $month = [int]$birthday.Month; $day = [int]$birthday.Day
        $status = if ($month -gt 0 -and $day -gt 0) { 'KNOWN' } else { 'UNKNOWN' }
        $enabled = [bool]$birthday.Enabled
        $source = $birthday.Source
    } else {
        $display = $english; $month = 0; $day = 0; $status = 'UNKNOWN'; $enabled = $false; $source = 'NO_BIRTHDAY_RECORD'
    }
    $records.Add([pscustomobject]@{ Game='GENSHIN'; Franchise='GENSHIN'; RosterName=$english; ChineseName=$display; Month=$month; Day=$day; BirthdayStatus=$status; Enabled=$enabled; ReviewStatus=if($status -eq 'UNKNOWN'){'UNKNOWN/PENDING_REVIEW'}else{'PENDING_USER_CONFIRMATION'}; RosterSource=$genshinUrl; BirthdaySource=$source; RecordClass='PLAYABLE_CHARACTER' })
}

# HI3 roster: official Valkyries API. Ignore the API's placeholder '-' entry.
$hi3Url = 'https://sg-public-api-static.hoyoverse.com/content_v2_user/app/5fcd2aa439ca4aea/getContentList?iChanId=520&iPageSize=200&iPage=1&sLangKey=zh-cn&isPreview=0'
$hi3 = Get-Json $hi3Url
foreach ($item in @($hi3.data.list)) {
    if (-not $item.sExt) { continue }
    $ext = $item.sExt | ConvertFrom-Json
    $english = Normalize $ext.'520_1'; $chinese = Normalize $ext.'520_0'
    if (-not $english -or $english -eq '-') { continue }
    $birthday = Find-Birthday 'HI3' @($english, $chinese)
    if ($birthday -is [array]) { $birthday = $birthday[0] }
    if ($birthday) {
        $display = if ($birthday.ChineseName) { $birthday.ChineseName } else { $chinese }
        $month = [int]$birthday.Month; $day = [int]$birthday.Day; $status = if ($month -gt 0 -and $day -gt 0) {'KNOWN'} else {'UNKNOWN'}; $enabled = [bool]$birthday.Enabled; $source = $birthday.Source
    } else { $display = $chinese; $month = 0; $day = 0; $status = 'UNKNOWN'; $enabled = $false; $source = 'NO_BIRTHDAY_RECORD' }
    $records.Add([pscustomobject]@{ Game='HI3'; Franchise='HI3'; RosterName=$english; ChineseName=$display; Month=$month; Day=$day; BirthdayStatus=$status; Enabled=$enabled; ReviewStatus=if($status -eq 'UNKNOWN'){'UNKNOWN/PENDING_REVIEW'}else{'PENDING_USER_CONFIRMATION'}; RosterSource=$hi3Url; BirthdaySource=$source; RecordClass='OFFICIAL_VALKYRIE' })
}

# NTE roster: official role slots on the official page. The page exposes slot
# ids and name artwork, but not birthdays. Keep unnamed slots explicit.
$nteUrl = 'https://nte.perfectworld.com/cn/main.html'
$nteHtml = (Invoke-WebRequest -Uri $nteUrl -UseBasicParsing -TimeoutSec 45).Content
$slots = @('canhong','yi','zhen','ka','an','xun','zero-male','zero-female','mint','nanally','xiaozhi','jiuyuan','hasuoer','baicang','fadiya','dfde','zaowu')
$slotNames = @{}
$slotBirthdayAliases = @{
    'yi'='Iroi'; 'zhen'='Shinku'; 'mint'='Mint'; 'nanally'='Nanally'; 'xiaozhi'='Xiaozhi'; 'jiuyuan'='Jiuyuan'; 'hasuoer'='Hathor'; 'baicang'='Baicang'; 'fadiya'='Fadia'; 'dfde'='Daffodill'; 'zaowu'='Zaowu'
}
$index = 0
foreach ($slot in $slots) {
    $index++
    $slotName = if ($slotNames.ContainsKey($slot)) {$slotNames[$slot]} else {"UNKNOWN_SLOT_{0:D2}" -f $index}
    $birthdayNames = @($slotName, $slot)
    if ($slotBirthdayAliases.ContainsKey($slot)) { $birthdayNames += $slotBirthdayAliases[$slot] }
    $birthday = Find-Birthday 'NTE' $birthdayNames
    if ($birthday -is [array]) { $birthday = $birthday[0] }
    if ($birthday) {
        $display = if ($birthday.ChineseName) {$birthday.ChineseName} else {$slotName}; $month=[int]$birthday.Month; $day=[int]$birthday.Day; $status=if($month -gt 0 -and $day -gt 0){'KNOWN'}else{'UNKNOWN'}; $enabled=[bool]$birthday.Enabled; $source=$birthday.Source
    } else { $display=$slotName; $month=0; $day=0; $status='UNKNOWN'; $enabled=$false; $source='NO_BIRTHDAY_RECORD' }
    $records.Add([pscustomobject]@{ Game='NTE'; Franchise='NTE'; RosterName=$slot; ChineseName=$display; Month=$month; Day=$day; BirthdayStatus=$status; Enabled=$enabled; ReviewStatus=if($status -eq 'UNKNOWN'){'UNKNOWN/PENDING_REVIEW'}else{'PENDING_USER_CONFIRMATION'}; RosterSource=$nteUrl; BirthdaySource=$source; RecordClass=if($slotName.StartsWith('UNKNOWN_SLOT')){'OFFICIAL_SLOT_UNNAMED'}else{'OFFICIAL_ROLE_PAGE'} })
}

$records = @($records | Sort-Object Game, ChineseName, RosterName)
$records | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'character-roster-review.json')
$csv = @('game,roster_name,chinese_name,birthday_month,birthday_day,birthday_status,report_enabled,review_status,roster_source,birthday_source,record_class')
foreach ($r in $records) {
    $values = @($r.Game,$r.RosterName,$r.ChineseName,$r.Month,$r.Day,$r.BirthdayStatus,($(if($r.Enabled){'YES'}else{'NO'})),$r.ReviewStatus,$r.RosterSource,$r.BirthdaySource,$r.RecordClass)
    $csv += (($values | ForEach-Object { '"' + (($_ -as [string]) -replace '"','""') + '"' }) -join ',')
}
$csv -join "`r`n" | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'character-roster-review.csv')

$summary = $records | Group-Object Game | ForEach-Object { [pscustomobject]@{Game=$_.Name;Total=$_.Count;Known=(@($_.Group|Where-Object BirthdayStatus -eq 'KNOWN').Count);Unknown=(@($_.Group|Where-Object BirthdayStatus -eq 'UNKNOWN').Count);Enabled=(@($_.Group|Where-Object Enabled).Count)} }
$md = [System.Collections.Generic.List[string]]::new()
$md.Add('# Character roster and birthday review')
$md.Add('')
$md.Add('This file follows the required order: establish the character roster first, then verify birthdays one by one. Roster sources prove that a character exists; birthday sources require separate review. UNKNOWN/PENDING_REVIEW rows remain listed, disabled, and excluded from the report.')
$md.Add('')
$md.Add('| Game | Roster total | Birthday found | UNKNOWN/PENDING_REVIEW | Enabled |')
$md.Add('|---|---:|---:|---:|---:|')
foreach($s in $summary){$md.Add("| $($s.Game) | $($s.Total) | $($s.Known) | $($s.Unknown) | $($s.Enabled) |")}
$md.Add('')
$md.Add('## Full roster')
$md.Add('')
foreach ($gameGroup in @($records | Group-Object Game)) {
    $md.Add("### $($gameGroup.Name)")
    $md.Add('')
    $md.Add('| Roster name | Chinese/display name | Birthday | Status | Enabled |')
    $md.Add('|---|---|---|---|---|')
    foreach ($r in @($gameGroup.Group | Sort-Object RosterName)) {
        $birthdayText = if ($r.Month -gt 0 -and $r.Day -gt 0) { '{0:00}-{1:00}' -f $r.Month,$r.Day } else { 'UNKNOWN' }
        $md.Add("| $($r.RosterName) | $($r.ChineseName) | $birthdayText | $($r.ReviewStatus) | $(if($r.Enabled){'YES'}else{'NO'}) |")
    }
    $md.Add('')
}
$md.Add('')
$md.Add('## User confirmation rules')
$md.Add('')
$md.Add('- Confirm that all three rosters are complete. Category pages are not characters.')
$md.Add('- UNKNOWN/PENDING_REVIEW rows are intentionally highlighted and cannot be guessed, enabled, or published.')
$md.Add('- This script creates review artifacts only. It does not modify SQLite or formal birthday data.')
$md.Add('')
$md.Add('## Sources')
$md.Add('')
$md.Add("- Genshin roster: $genshinUrl")
$md.Add("- HI3 roster: $hi3Url")
$md.Add("- NTE roster: $nteUrl")
$md | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'CHARACTER_ROSTER_REVIEW.md')

# Chinese review artifact for manual confirmation. Existing birthday values are
# intentionally labelled as legacy records until the user confirms their sources.
$zh = [System.Collections.Generic.List[string]]::new()
$zh.Add('# 角色名册与生日逐项审阅（中文）')
$zh.Add('')
$zh.Add('本清单严格按“先确认已实装角色名册，再逐个核验生日”的顺序生成。名册来源只证明角色/官方槽位存在；生日必须另有证据。所有旧库生日记录均处于待用户确认状态。')
$zh.Add('')
$zh.Add('| 游戏 | 名册总数 | 有旧库生日 | 未找到生日 | 当前启用 |')
$zh.Add('|---|---:|---:|---:|---:|')
foreach($s in $summary){
    $gameName = switch ($s.Game) { 'GENSHIN' {'原神'} 'HI3' {'崩坏三'} 'NTE' {'异环'} default {$s.Game} }
    $zh.Add("| $gameName | $($s.Total) | $($s.Known) | $($s.Unknown) | $($s.Enabled) |")
}
$zh.Add('')
$zh.Add('## 状态说明')
$zh.Add('')
$zh.Add('- `待用户确认`：生日来自当前本地旧记录，但尚未完成逐项来源核验；确认前不视为最终可信。')
$zh.Add('- **⚠ 待补生日**：名册中存在角色/官方槽位，但未找到生日；已禁用，不进入日报，不能猜测。')
$zh.Add('- 异环的“官方角色槽位 XX”表示官网当前可识别的角色槽位，中文名尚未从官方页面可靠解析。')
$zh.Add('')
$zh.Add('## 完整名册与生日')
$zh.Add('')
foreach ($gameGroup in @($records | Group-Object Game)) {
    $gameName = switch ($gameGroup.Name) { 'GENSHIN' {'原神'} 'HI3' {'崩坏三'} 'NTE' {'异环'} default {$gameGroup.Name} }
    $zh.Add("### $gameName")
    $zh.Add('')
    $zh.Add('| 名册名称 | 中文显示名 | 生日 | 审阅状态 | 日报启用 |')
    $zh.Add('|---|---|---|---|---|')
    foreach ($r in @($gameGroup.Group | Sort-Object RosterName)) {
        $birthdayText = if ($r.Month -gt 0 -and $r.Day -gt 0) { '{0:00}-{1:00}' -f $r.Month,$r.Day } else { '⚠ 待补生日' }
        $statusText = if ($r.Month -gt 0 -and $r.Day -gt 0) { '待用户确认（旧库记录）' } else { '**⚠ UNKNOWN/PENDING_REVIEW**' }
        $enabledText = if($r.Enabled){'是'}else{'否'}
        $zh.Add("| $($r.RosterName) | $($r.ChineseName) | $birthdayText | $statusText | $enabledText |")
    }
    $zh.Add('')
}
$zh.Add('## 用户确认项')
$zh.Add('')
$zh.Add('1. 确认原神、崩坏三、异环三份名册是否完整；分类页不算角色。')
$zh.Add('2. 对所有“⚠ 待补生日”项提供生日或确认继续保持禁用。')
$zh.Add('3. 对已有生日记录确认保留，或指出需要修改的日期。')
$zh.Add('4. 确认前本脚本不会修改 SQLite，也不会把任何待确认记录写入日报。')
$zh.Add('')
$zh.Add('## 名册来源')
$zh.Add('')
$zh.Add("- 原神：$genshinUrl")
$zh.Add("- 崩坏三：$hi3Url")
$zh.Add("- 异环：$nteUrl")
$zh -join "`r`n" | Set-Content -Encoding utf8 (Join-Path $OutputDirectory '角色名册与生日审阅_中文.md')
Write-Output ("Rows={0}; Output={1}" -f $records.Count,$OutputDirectory)

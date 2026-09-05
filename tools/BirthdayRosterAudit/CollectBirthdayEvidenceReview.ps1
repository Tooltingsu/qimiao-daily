param(
    [string]$RosterPath = "$(Join-Path $PSScriptRoot '../../artifacts/birthday-roster-review-20260820/character-roster-review.json')",
    [string]$HoYoWikiPath = "$(Join-Path $PSScriptRoot '../../artifacts/birthday-roster-review-20260820/hoyowiki-birthday-fetch.json')",
    [string]$OutputDirectory = "$(Join-Path $PSScriptRoot '../../artifacts/birthday-roster-review-20260820')"
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

function Normalize([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return '' }
    return (($value.Trim() -replace '\s+', ' ').ToLowerInvariant())
}

function Get-FandomBatch([string[]]$titles) {
    $encoded = [Uri]::EscapeDataString(($titles -join '|'))
    $url = "https://genshin-impact.fandom.com/api.php?action=query&prop=revisions&titles=$encoded&rvprop=content&rvslots=main&format=json"
    try {
        return Invoke-RestMethod -Uri $url -Headers @{ 'User-Agent' = 'QimiaoDaily/3.0 birthday evidence audit' } -TimeoutSec 45
    } catch {
        return $null
    }
}

function Parse-FandomPage($page) {
    if ($null -eq $page.revisions -or $page.revisions.Count -eq 0) { return $null }
    $text = [string]$page.revisions[0].slots.main.'*'
    $birthdayMatch = [Regex]::Match($text, '(?im)^\|birthday\s*=\s*([^\r\n]+)')
    $birthday = if ($birthdayMatch.Success) { $birthdayMatch.Groups[1].Value.Trim() } else { '' }
    $month = 0; $day = 0
    $date = [Regex]::Match($birthday, '(?i)(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})')
    if ($date.Success) {
        $month = [DateTime]::ParseExact($date.Groups[1].Value, 'MMMM', [Globalization.CultureInfo]::InvariantCulture).Month
        $day = [int]$date.Groups[2].Value
    }
    $zhMatch = [Regex]::Match($text, '(?im)^\|zhs\s*=\s*([^\r\n|]+)')
    [pscustomobject]@{ Month = $month; Day = $day; BirthdayText = $birthday; ChineseName = if ($zhMatch.Success) { $zhMatch.Groups[1].Value.Trim() } else { '' } }
}

$roster = Get-Content -Raw -Encoding UTF8 $RosterPath | ConvertFrom-Json
$hoyo = @{}
if (Test-Path $HoYoWikiPath) {
    foreach ($row in (Get-Content -Raw -Encoding UTF8 $HoYoWikiPath | ConvertFrom-Json)) {
        $hoyo[(Normalize $row.Character)] = $row
    }
}

$genshinTitles = @($roster | Where-Object { $_.Game -eq 'GENSHIN' } | ForEach-Object { $_.RosterName } | Sort-Object -Unique)
$fandom = @{}
for ($i = 0; $i -lt $genshinTitles.Count; $i += 25) {
    $batch = @($genshinTitles[$i..([Math]::Min($i + 24, $genshinTitles.Count - 1))])
    $response = Get-FandomBatch $batch
    if ($response -and $response.query.pages) {
        foreach ($property in $response.query.pages.PSObject.Properties) {
            $page = $property.Value
            $parsed = Parse-FandomPage $page
            if ($parsed) { $fandom[(Normalize $page.title)] = $parsed }
        }
    }
}

$rows = foreach ($item in $roster) {
    $official = if ($item.Game -eq 'GENSHIN' -and $hoyo.ContainsKey((Normalize $item.RosterName))) { $hoyo[(Normalize $item.RosterName)] } else { $null }
    $thirdParty = if ($item.Game -eq 'GENSHIN' -and $fandom.ContainsKey((Normalize $item.RosterName))) { $fandom[(Normalize $item.RosterName)] } else { $null }
    $officialDate = if ($official -and $official.Birthday -match '^(\d{1,2})/(\d{1,2})$') { "$($Matches[1].PadLeft(2,'0'))-$($Matches[2].PadLeft(2,'0'))" } else { '' }
    $fandomDate = if ($thirdParty -and $thirdParty.Month -gt 0) { '{0:00}-{1:00}' -f $thirdParty.Month,$thirdParty.Day } else { '' }
    $status = if ($officialDate) { 'VERIFIED_OFFICIAL' } elseif ($fandomDate) { 'FOUND_THIRD_PARTY_PENDING_REVIEW' } else { 'UNKNOWN' }
    $display = if ($thirdParty -and $thirdParty.ChineseName) { $thirdParty.ChineseName } else { $item.ChineseName }
    [pscustomobject]@{
        Game = $item.Game; RosterName = $item.RosterName; ChineseName = $display
        ExistingBirthday = if ($item.Month -gt 0) { '{0:00}-{1:00}' -f $item.Month,$item.Day } else { '' }
        OfficialHoYoWikiBirthday = $officialDate; FandomBirthday = $fandomDate
        BirthdayStatus = $status
        OfficialSource = if ($official) { $official.Url } else { '' }
        ThirdPartySource = if ($thirdParty) { 'https://genshin-impact.fandom.com/wiki/' + [Uri]::EscapeDataString($item.RosterName) } else { '' }
        Evidence = if ($officialDate) { ('HoYoWiki official Birthday={0}' -f $officialDate) } elseif ($fandomDate) { ('Fandom birthday={0}; single third-party source; review required' -f $fandomDate) } else { 'Birthday field not found' }
    }
}

$jsonPath = Join-Path $OutputDirectory 'birthday-evidence-review.json'
$csvPath = Join-Path $OutputDirectory 'birthday-evidence-review.csv'
$mdPath = Join-Path $OutputDirectory '生日证据逐项审阅.md'
$rows | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 $jsonPath
$csv = @('game,roster_name,chinese_name,existing_birthday,official_hoyowiki_birthday,fandom_birthday,birthday_status,official_source,third_party_source,evidence')
function CsvCell([object]$value) {
    $text = if ($null -eq $value) { '' } else { [string]$value }
    $quote = [char]34
    return ($quote + ($text -replace [regex]::Escape($quote.ToString()), ($quote.ToString() + $quote.ToString())) + $quote)
}
foreach ($row in $rows) {
    $values = @($row.Game,$row.RosterName,$row.ChineseName,$row.ExistingBirthday,$row.OfficialHoYoWikiBirthday,$row.FandomBirthday,$row.BirthdayStatus,$row.OfficialSource,$row.ThirdPartySource,$row.Evidence)
    $csv += (($values | ForEach-Object { CsvCell $_ }) -join ',')
}
$csv -join [Environment]::NewLine | Set-Content -Encoding UTF8 $csvPath

$md = [System.Collections.Generic.List[string]]::new()
$md.Add('# 生日证据逐项审阅')
$md.Add('')
$md.Add('Evidence only; no SQLite writes. Official HoYoWiki dates are VERIFIED_OFFICIAL. Fandom-only dates require review. Missing dates are UNKNOWN.')
$md.Add('')
$md.Add('| Game | Roster total | Official | Third-party pending | UNKNOWN |')
$md.Add('|---|---:|---:|---:|---:|')
foreach ($group in @($rows | Group-Object Game)) {
    $officialCount = @($group.Group | Where-Object { $_.BirthdayStatus -eq 'VERIFIED_OFFICIAL' }).Count
    $thirdPartyCount = @($group.Group | Where-Object { $_.BirthdayStatus -eq 'FOUND_THIRD_PARTY_PENDING_REVIEW' }).Count
    $unknownCount = @($group.Group | Where-Object { $_.BirthdayStatus -eq 'UNKNOWN' }).Count
    $md.Add(('| {0} | {1} | {2} | {3} | {4} |' -f $group.Name,$group.Count,$officialCount,$thirdPartyCount,$unknownCount))
}
$md.Add('')
foreach ($group in @($rows | Group-Object Game)) {
    $md.Add(('## {0}' -f $group.Name)); $md.Add(''); $md.Add('| Roster | Chinese name | Existing | Official | Third-party | Status |'); $md.Add('|---|---|---|---|---|---|')
    foreach ($row in @($group.Group | Sort-Object RosterName)) { $md.Add(('| {0} | {1} | {2} | {3} | {4} | {5} |' -f $row.RosterName,$row.ChineseName,$row.ExistingBirthday,$row.OfficialHoYoWikiBirthday,$row.FandomBirthday,$row.BirthdayStatus)) }
    $md.Add('')
}
$md.Add('## Review rules'); $md.Add(''); $md.Add('- VERIFIED_OFFICIAL still requires user confirmation before report enablement.'); $md.Add('- FOUND_THIRD_PARTY_PENDING_REVIEW needs a second source or user confirmation.'); $md.Add('- UNKNOWN must remain disabled and cannot be guessed.')
$md -join [Environment]::NewLine | Set-Content -Encoding UTF8 $mdPath
Write-Output ('Rows={0}; Output={1}' -f $rows.Count,$OutputDirectory)

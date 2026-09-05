param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'
$path = Join-Path $ProjectRoot 'docs\audit\ORIGINAL_REQUIREMENTS_TRACEABILITY.md'
$allowed = @('PASS', 'PARTIAL', 'FAIL', 'NOT_IMPLEMENTED', 'UNKNOWN')
$rows = Get-Content -LiteralPath $path | Where-Object { $_ -match '^\|\s*OR-\d+' }
$ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$counts = @{}

foreach ($row in $rows) {
    $parts = $row.Trim('|').Split('|')
    if ($parts.Count -ne 9) { throw "Traceability row has $($parts.Count) columns: $row" }
    $id = $parts[0].Trim()
    if (-not $ids.Add($id)) { throw "Duplicate traceability ID: $id" }
    $status = $parts[6].Trim()
    if ($status -notin $allowed) { throw "Invalid PASS/FAIL status '$status' for $id" }
    if (-not $counts.ContainsKey($status)) { $counts[$status] = 0 }
    $counts[$status]++
}

[pscustomobject]@{
    Rows = $rows.Count
    UniqueIds = $ids.Count
    StatusCounts = (($counts.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join ';')
    Valid = $true
}

# ARCH-DOC-023 compensating verification - faithful replica of DocsMetadataTests
# (tests/UniClaw.Kernel.Tests/DocsMetadataTests.cs) four rules.
# Reason: this environment cannot restore nuget packages (nuget.org unreachable
# via proxy; TLS schannel blocked in sandbox), so `dotnet test` cannot run.
# This script replicates the same directory scope and regex logic.
# NOTE: keep this file ASCII-only: PS5.1 parses BOM-less UTF-8 scripts as ANSI,
# and CJK comment bytes can swallow newlines and comment out code lines.
$ErrorActionPreference = 'Stop'
$root = "D:\space-x\uni-agent\uni_claw"
$violations = @()

# ---- Rule 1: analysis/design header Status/Authority + forbidden words ----
foreach ($dir in @('analysis', 'design')) {
    Get-ChildItem -LiteralPath (Join-Path $root "docs\$dir") -Filter *.md -Recurse -File |
    Where-Object { $_.Name -ne 'README.md' } | ForEach-Object {
        $lines = [System.IO.File]::ReadAllLines($_.FullName)   # .NET default = UTF-8
        $first40 = ($lines | Select-Object -First 40) -join "`n"
        $rel = $_.FullName.Substring($root.Length + 1)
        $statusLine = ($lines | Select-Object -First 40 | Where-Object { $_ -match '^>\s*Status[:\uff1a]' } | Select-Object -First 1)
        $authLine   = ($lines | Select-Object -First 40 | Where-Object { $_ -match '^>\s*Authority[:\uff1a]' } | Select-Object -First 1)
        if (-not $statusLine) { $violations += "$rel missing '> Status:'" }
        if (-not $authLine)   { $violations += "$rel missing '> Authority:'" }
        elseif ($authLine -notmatch '\bNONE\b') { $violations += "$rel Authority not NONE" }
        if ($statusLine -and ($statusLine -match '\bFROZEN\b' -or $statusLine -match '\bCLOSED\b')) {
            $violations += "$rel status line contains FROZEN/CLOSED: $statusLine"
        }
    }
}
"Rule1 analysis/design headers: $(if ($violations.Count -eq 0) { 'PASS (0 violations)' } else { "FAIL ($($violations.Count))" })"

# ---- Rule 2: architecture docs must contain FROZEN/CLOSED/COMPONENT-BASELINE ----
$before = $violations.Count
Get-ChildItem -LiteralPath (Join-Path $root 'docs\architecture') -Filter *.md -Recurse -File |
Where-Object { $_.Name -ne 'README.md' } | ForEach-Object {
    $t = [System.IO.File]::ReadAllText($_.FullName)
    if ($t -notmatch 'FROZEN' -and $t -notmatch 'CLOSED' -and $t -notmatch 'COMPONENT-BASELINE') {
        $violations += "architecture/$($_.Name): no FROZEN/CLOSED/COMPONENT-BASELINE"
    }
}
"Rule2 architecture frozen words: $(if ($violations.Count -eq $before) { 'PASS' } else { 'FAIL' })"

# ---- Rule 3: adr filenames ^\d{4}- ----
$before = $violations.Count
Get-ChildItem -LiteralPath (Join-Path $root 'docs\adr') -Filter *.md -File |
Where-Object { $_.Name -ne 'README.md' -and $_.Name -notmatch '^\d{4}-' } | ForEach-Object {
    $violations += "adr filename: $($_.Name)"
}
"Rule3 ADR filenames: $(if ($violations.Count -eq $before) { 'PASS' } else { 'FAIL' })"

# ---- Rule 4 (SourceReadmeOwnership no-regression proxy) ----
# This change reworded README prose only; verify no .cs stem registration was lost:
# every removed diff line containing a .cs mention must still have its stem present.
$before = $violations.Count
Set-Location $root
$diff = git diff -U0 -- 'src/*.md' 'src/*/*.md' 'src/*/*/*.md'
$removedCsLines = @($diff | Where-Object { $_ -like '-*' -and $_ -notlike '-*-*' -and $_ -match '\.cs' })
foreach ($line in $removedCsLines) {
    if ($line -match '([A-Za-z0-9_.]+\.cs)') {
        $stem = $Matches[1]
        $still = git grep -l -F $stem -- 'src/*.md' 'src/*/*.md' 'src/*/*/*.md'
        if (-not $still) { $violations += "stem removed from all READMEs: $stem" }
    }
}
"Rule4 README .cs registration no-regression: $(if ($violations.Count -eq $before) { "PASS (removed-with-.cs lines: $($removedCsLines.Count))" } else { 'FAIL' })"

""
if ($violations.Count -eq 0) { "RESULT: ALL PASS" } else { "RESULT: FAIL"; $violations | ForEach-Object { " - $_" } }

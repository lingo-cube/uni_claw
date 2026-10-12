# ARCH-DOC-023 compensating verification 2/2 - faithful replica of SourceReadmeOwnershipTests
# (tests/UniClaw.Kernel.Tests/SourceReadmeOwnershipTests.cs, ARCH-DOC-022 enforcement).
# Same ownership map, same two enforcement lines, same generated-dir exemption,
# same >50-files sanity floor. ASCII only (PS5.1 BOM-less UTF-8 script hazard).
$ErrorActionPreference = 'Stop'
$root = 'D:\space-x\uni-agent\uni_claw'

$ownership = [ordered]@{
  'src/UniClaw.Kernel'                          = 'src/UniClaw.Kernel/README.md'
  'src/UniClaw.Kernel/Core'                     = 'src/UniClaw.Kernel/README.md'
  'src/UniClaw.Kernel/Capability'               = 'src/UniClaw.Kernel/Capability/README.md'
  'src/UniClaw.Kernel/Perception'               = 'src/UniClaw.Kernel/Perception/README.md'
  'src/UniClaw.Kernel/Perception/Fusion'        = 'src/UniClaw.Kernel/Perception/README.md'
  'src/UniClaw.Kernel/Perception/UiHierarchy'   = 'src/UniClaw.Kernel/Perception/README.md'
  'src/UniClaw.Kernel/Evidence'                 = 'src/UniClaw.Kernel/Evidence/README.md'
  'src/UniClaw.Kernel/World'                    = 'src/UniClaw.Kernel/World/README.md'
  'src/UniClaw.Kernel/World/UiRealization'      = 'src/UniClaw.Kernel/World/README.md'
  'src/UniClaw.Kernel/Run'                      = 'src/UniClaw.Kernel/Run/README.md'
  'src/UniClaw.Kernel/Outcome'                  = 'src/UniClaw.Kernel/Run/README.md'
  'src/UniClaw.Kernel/Control'                  = 'src/UniClaw.Kernel/Control/README.md'
  'src/UniClaw.Kernel/Assurance'                = 'src/UniClaw.Kernel/Assurance/README.md'
  'src/UniClaw.Kernel/Effects'                  = 'src/UniClaw.Kernel/Effects/README.md'
  'src/UniClaw.Kernel/Effects/ExecutionSource'  = 'src/UniClaw.Kernel/Effects/README.md'
  'src/UniClaw.Kernel/Trace'                    = 'src/UniClaw.Kernel/Trace/README.md'
  'src/UniClaw.Kernel/Diagnostics'              = 'src/UniClaw.Kernel/Diagnostics/README.md'
  'src/UniClaw.Kernel/Runtime'                  = 'src/UniClaw.Kernel/Runtime/README.md'
  'src/UniClaw.Agent'                           = 'src/UniClaw.Agent/README.md'
  'src/UniClaw.Agent/Goal'                      = 'src/UniClaw.Agent/README.md'
  'src/UniClaw.Agent/Evaluation'                = 'src/UniClaw.Agent/README.md'
  'src/UniClaw.Agent/Profile'                   = 'src/UniClaw.Agent/README.md'
  'src/UniClaw.Agent.Dsh'                       = 'src/UniClaw.Agent.Dsh/README.md'
  'src/UniClaw.Host'                            = 'src/UniClaw.Host/README.md'
  'src/UniClaw.Host/Capability'                 = 'src/UniClaw.Host/README.md'
  'src/UniClaw.Host/Runtime'                    = 'src/UniClaw.Host/Runtime/../README.md'
  'src/UniClaw.Host/SettingsCoverage'           = 'src/UniClaw.Host/README.md'
  'src/UniClaw.Host.Dsh'                        = 'src/UniClaw.Host.Dsh/README.md'
  'src/UniClaw.Core'                            = 'src/UniClaw.Core/README.md'
}
# normalize the one intentionally written-with-.. entry (test uses plain path)
$ownership['src/UniClaw.Host/Runtime'] = 'src/UniClaw.Host/README.md'

$readmeCache = @{}
$violations = @()
$checked = 0
foreach ($project in [System.IO.Directory]::GetDirectories((Join-Path $root 'src'))) {
  foreach ($file in [System.IO.Directory]::GetFiles($project, '*.cs', [System.IO.SearchOption]::AllDirectories)) {
    $norm = $file.Replace('\', '/')
    if ($norm -match '/obj/' -or $norm -match '/bin/') { continue }
    $checked++
    $relFile = $norm.Substring($root.Length + 1)
    $relDir = [System.IO.Path]::GetDirectoryName($relFile).Replace('\', '/')
    if (-not $ownership.Contains($relDir)) {
      $violations += "${relFile}: dir '${relDir}' has no ownership entry"; continue
    }
    $readme = $ownership[$relDir]
    if (-not $readmeCache.ContainsKey($readme)) {
      $readmeCache[$readme] = [System.IO.File]::ReadAllText((Join-Path $root ($readme -replace '/', '\')))
    }
    $stem = [System.IO.Path]::GetFileNameWithoutExtension($file)
    if (-not $readmeCache[$readme].Contains($stem)) {
      $violations += "${relFile}: stem not named in ${readme}"
    }
  }
}
"files checked: $checked (floor > 50)"
if ($checked -le 50) { $violations += "sanity floor failed (only $checked files)" }
if ($violations.Count -eq 0) { 'Rule5 SourceReadmeOwnership replica: PASS (0 violations)' }
else { 'Rule5 SourceReadmeOwnership replica: FAIL'; $violations | ForEach-Object { " - $_" } }
if ($checked -gt 50 -and $violations.Count -eq 0) { 'RESULT: PASS' } else { 'RESULT: FAIL' }

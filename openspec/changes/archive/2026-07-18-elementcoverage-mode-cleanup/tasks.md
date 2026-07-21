## 1. Code: remove legacy path + auto-derive

- [x] 1.1 `ElementCoverageMode.cs` — remove `LegacyRatio` member (enum → `{Exact, Subset}`)
- [x] 1.2 `ElementCoverageExpectation.cs` — remove `RequiredRatio` param; `Mode` default → `Exact`; update doc
- [x] 1.3 `ExpectedBehavior.cs` — `FromJson`/DTO drop `RequiredRatio`; `ParseElementCoverageMode` drop legacy branch (absent/unknown → Exact); `DeriveElementCoverage` drop `RequiredRatio` passthrough; `ResolveModeAndTarget` removed (auto-derive dropped — Mode preserved, TargetName captured from cp for subset)
- [x] 1.4 `ExpectedBehavior.Verify.cs` — remove `VerifyElementCoverageLegacy` + switch default; VerifyElementCoverage switch on Exact/Subset only

## 2. Migrate 4 orphan fixtures

- [x] 2.1 `scroll/persistent-dedup.json` — add `mode: exact`, drop `requiredRatio`
- [x] 2.2 `scroll/overlapping-adaptive.json` — add `mode: exact`, drop `requiredRatio`
- [x] 2.3 `scroll/wifi-list-full-traversal.json` — add `mode: exact`, drop `requiredRatio`
- [x] 2.4 `scroll/wifi-list-target-search.json` — add `mode: subset`, drop `requiredRatio`

## 3. Verify + archive

- [x] 3.1 `openspec validate elementcoverage-mode-cleanup` — valid
- [x] 3.2 `dotnet build` (0 errors) + `dotnet test` (711 green, no behavior change)
- [x] 3.3 `openspec archive elementcoverage-mode-cleanup` — archived
- [x] 3.4 Record decision in `docs/system/decisions/log.md` (D-88: legacy path removed); D-88 recorded at line 1435

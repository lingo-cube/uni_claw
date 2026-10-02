# AGT-010 真机复跑结果

## 四元组

- method：在 API 35 `p26_pixel` emulator（`emulator-5554`，`1080x2400`）执行 `DSH_TEST_PERCEPTION_LIVE=1 dotnet test tests/UniClaw.Host.Tests --filter "FullyQualifiedName~SettingsCoverageLiveTests" --logger "console;verbosity=detailed"`。
- expected：`Security & privacy` 进入步骤 `Verified=true` 且 `RouteAfter=android.settings|rk1:Settings|src=title|up=1`；无撞名 target-unique 失败；至少一次 verified swipe；终局为 `CoverageComplete` 或带未覆盖项清单的 `BoundedStop`。
- actual：测试 1/1 通过；共 25 步、25 步 verified、2 次 swipe（2 次 verified），无 obstacle consult。`Security & privacy` 为 step 13，`Verified=true`，route 为 `android.settings|rk1:Settings|src=title|up=1`，无 failure reason。终局 `BoundedStop`，coverage rate `0.6666666666666666`，未覆盖项为 `scroll-discovered-entry` 与 `repeated-entry`；step success rate 1，firstDivergence=null。
- evidence：`evidence/agt-010/live-rerun/run-20261002-155017-713/coverage-steps.json`、`coverage-report.json`、`facts.json`、`settings-trace.json`、`trace.json`、`evidence/`（51 个 PNG/XML 文件）及 `exec.journal`。

## 与 e1 基线对照

e1 的 step 20 在 `Security & privacy` 因根页与撞名页标题同为 `Settings`、digest `55291AC…` 而确定性失败。本次 step 13 使用 `rk1:Settings|src=title|up=1`，进入验证通过，且无 target-unique 失败，证明 AGT-010 RouteKey 修复在真机链路生效。列表底部路径产生 2 次 verified swipe；本次终局因 `repeated-entry` 与 `scroll-discovered-entry` 未覆盖而诚实 `BoundedStop`，未将其伪造为完整覆盖。

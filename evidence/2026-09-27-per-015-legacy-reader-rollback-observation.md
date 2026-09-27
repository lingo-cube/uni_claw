# PER-015 Legacy Reader Retirement + Rollback Observation

日期：2026-09-27  
base：8ddc32c7  
范围：ConflictResolver typed migration + PER-012 representative observation

## 做了什么

- `ConflictResolver` 改为只消费 `ObservedValue<CheckedState>` 和 typed capture timestamp。
- `Checked/Unchecked` 继续使用既有 category-authority conflict rule。
- `Partial/Unknown/Unsupported` 统一 fail-closed 为 unresolved。
- rendered appearance 以独立 ClaimDomain 标记，不参与 semantic.checked resolution。
- XML snapshot 保留在 `PostActionXmlRouter` rollback/egress surface；未删除 legacy writers、projection 或 egress flag。
- UniKernel 只在 occurrence 与 typed claim 有明确关联时调用 typed resolver；缺证据不猜。
- PER-012/PER-014 inventory 测试更新为 zero production readers。
- 场景认证按既有流程以 PER-015 重盖 runtimeSourceHash；expectationsDigest 未变化。

## 验证结果

| 项目 | 结果 | 证据 |
|---|---|---|
| A1–A8 ConflictResolver typed fixtures | PASS 8/8 | `tests/UniClaw.Kernel.Tests/ConflictResolverTests.cs` |
| PER-014 cutover/inventory | PASS 9/9 | `LegacyStateSurfaceFreezeTests` + `Per014CutoverTests` |
| Kernel focused migration/regression | PASS 31/31 | ConflictResolver、PER-009 remediation、PER-014、inventory |
| Kernel full | PASS 643/643 | `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore` |
| Simulation behavior（先于再认证） | PASS 180/180 | 排除 certification tests |
| Simulation full | PASS 184/184 | 再认证后全绿 |
| Host deterministic | PASS 63/63 | `dotnet test tests/UniClaw.Host.Tests/...` |
| coordinate-space/live effect deterministic path | PASS 10/10 | LiveCoordinateGate 2 + Adb/LiveClosedLoop 8 |
| live device capability manifest | PASS | API 35；ADB screenshot、uiautomator、wm size、settings 均可用；selector=`UNICLAW_ANDROID_DEVICE` |
| real TypedLiveChain | PASS 1/1 | `UNICLAW_ANDROID_DEVICE=emulator-5556 DSH_TEST_PERCEPTION_LIVE=1`；real XML → typed projection → world path |
| real LiveCoordinateGate | PASS 2/2 | 同一设备；坐标空间恢复与真实 tap 两门均通过 |
| real HostLiveFull | PASS 1/1 | API 35 AVD；真实截图、视觉、ADB tap、post-action XML、独立 settings 复核；`status=Completed`, `delivered=1`, `legacyEgress=false` |
| full solution | PASS 1051/1051 | `dotnet test UniClaw.Kernel.slnx --no-restore` |
| scenario certification | PASS 29/29 | `scenario_certify.py --change PER-015 --check` |
| scenario coverage truth chain | PASS | `scenario-coverage.py --run` |
| diff check | PASS | `git diff --check` |
| production `*.state` readers | 0 | source scan + T10 inventory |
| expectationsDigest | unchanged | 29 files仅变更 runtimeSourceHash/certifiedByChange |

## 真机观察

本轮使用由脚本约束的临时 AVD clone（`/tmp`，API 35，selector 通过
`UNICLAW_ANDROID_DEVICE` 注入；无 `emulator-5554` 默认值）：

- capability manifest PASS：ADB、API>=35、screenshot、uiautomator、`wm size`、
  `settings` 均可用；设备 viewport 为 1080x1920 override。
- `TypedLiveChainTests`：PASS 1/1。真实 XML 进入 typed projection 和
  WorldModel；关闭态保持 Unknown/无 checked claim，未用 legacy reader 补值。
- `LiveCoordinateGateTests`：PASS 2/2。真实坐标空间恢复、真实 tap 和复查均通过。
- `HostLiveFullTests`：PASS 1/1。测试先用 `svc wifi disable` 将 API 35 AVD 固定到
  可验证的 off 基线，再由真实截图、视觉、ADB tap、post-action XML 和独立 settings
  复核完成 off→on；Host 终态 `Completed`，`delivered=1`，`legacyEgress=false`。
  这条路径使用 typed checked=true 完成证明，不把 collapsed checked=false 缺席猜成 unchecked。
- 本机默认 perception 配置包含不可用的 MPS provider；运行 HostLiveFull 时仅临时
  移出这两个声明以使用 CPU-compatible fallback，测试后已恢复原文件。这是运行环境
  处置，不是产品代码变更。
- 未观察到 typed→legacy rollback（live result `legacyEgress=false`）；effect-critical
  legacy dependency = NONE。Unknown/Partial 保持 fail-closed，未触发 legacy reader fallback。
- 未发现 semantic blocker、post-action/RuntimeAssurance 依赖 legacy 的确定性测试证据。

## Gate 判定

1. ConflictResolver migration：PASS
2. production `*.state` readers：0
3. typed semantic preservation：PASS
4. semantic/rendered separation：PASS
5. authority deviation：NONE
6. Kernel regression：643/643
7. Simulation：184/184
8. HostLiveFull：PASS（真实链 `Completed`, `delivered=1`, `legacyEgress=false`）
9. TypedLiveChain：PASS（real XML → typed projection → world path）
10. LiveCoordinateGate：PASS（2/2）
11. full solution：PASS（1051/1051）
12. certification / coverage：PASS（29/29 + truth chain）
13. typed→legacy rollback observed：NO（本次 live result 无 legacy egress）
14. effect-critical typed terminal proof：PASS（HostLiveFull real Completed）
15. effect-critical legacy dependency：NONE
16. rollback observation window：PASS（代表性真机窗口已闭合）
17. PER-012 four removal gates：
    - Gate 1：PASS
    - Gate 2：PASS
    - Gate 3：PASS
    - Gate 4：PASS（API 35 真实设备 HostLiveFull Completed）
18. verdict：READY_FOR_LEGACY_REMOVAL

## 保留与停止点

legacy writers、LegacyStateProjection、LegacyStateEgress、SharedSubjects legacy constants
和 rollback support code 在本 change 内仍保留；PER-011 Fusion 未启动。Gate 4 已闭合，
因此后续 change 可按 owner gate 移除 production legacy surface，同时保留历史与 fixtures。

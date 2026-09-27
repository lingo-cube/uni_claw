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
| full solution | PASS 1051/1051 | `dotnet test UniClaw.Kernel.slnx --no-restore` |
| scenario certification | PASS 29/29 | `scenario_certify.py --change PER-015 --check` |
| scenario coverage truth chain | PASS | `scenario-coverage.py --run` |
| diff check | PASS | `git diff --check` |
| production `*.state` readers | 0 | source scan + T10 inventory |
| expectationsDigest | unchanged | 29 files仅变更 runtimeSourceHash/certifiedByChange |

## 真机观察

尝试启用 `DSH_TEST_PERCEPTION_LIVE=1`：

- `TypedLiveChainTests`：FAIL，`adb` 无可用设备（`real dump 失败，structural=False`）。
- `HostLiveFullTests`：FAIL，环境阻塞：`device 'emulator-5554' not found`。
- 使用 `DSH_TEST_NO_ADB=1` 的门控运行只得到显式环境 skip，不能当作真机 PASS。
- 未观察到 typed→legacy rollback；由于无设备，这一项是“无观察证据”，不是已证明的真实设备 NO。
- 未发现 semantic blocker、post-action/RuntimeAssurance 依赖 legacy 的代码或确定性测试证据。

## Gate 判定

1. ConflictResolver migration：PASS
2. production `*.state` readers：0
3. typed semantic preservation：PASS
4. semantic/rendered separation：PASS
5. authority deviation：NONE
6. Kernel regression：643/643
7. Simulation：184/184
8. HostLiveFull：FAIL（环境阻塞，exact blocker 见上）
9. TypedLiveChain：FAIL（环境阻塞，exact blocker 见上）
10. full solution：PASS（1051/1051）
11. certification / coverage：PASS（29/29 + truth chain）
12. typed→legacy rollback observed：NO（未观察到；真机证据不足）
13. effect-critical legacy dependency：NONE（legacy 仅保留 rollback/egress surface）
14. rollback observation window：HOLD
15. PER-012 four removal gates：
    - Gate 1：PASS
    - Gate 2：PASS
    - Gate 3：PASS
    - Gate 4：HOLD（无可用真机/模拟器，HostLiveFull 与 TypedLiveChain 无法取得代表性 PASS）
16. verdict：NOT_READY_FOR_LEGACY_REMOVAL

## 保留与停止点

legacy writers、LegacyStateProjection、LegacyStateEgress、SharedSubjects legacy constants
和 rollback support code 均保留；未启动 PER-011 Fusion。下一步只需在有
`emulator-5554` 或等价设备的环境重跑 HostLiveFull 与 TypedLiveChain，并以同一
Gate 4 标准重新裁决。

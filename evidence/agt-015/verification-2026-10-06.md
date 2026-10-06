# AGT-015 验证证据 — 未知状态安全停止与错误目标拒绝（2026-10-06）

> 环境：API 35 emulator `emulator-5556`（start-test-device.sh/supervisor 托管，wm 1080×1920）；
> 模型线路：专用 DSH 实例 127.0.0.1:3081（`zai-coding-cn/glm-5.3-flash`，家目录 7890 代理）；
> 运行时 profile/config：`UNICLAW_UNIAGENT_PROD_CONFIG=/tmp/uniagent-prod-3081.yaml`（baseUrl 3081）、
> NFC 回合 `UNICLAW_SETTINGS_COVERAGE_CONFIG=/tmp/agt015-coverage-nfc.yaml`（actionPolicy.path →
> `testsets/android-settings/decoy-target-policy.json`，targeted=toggle:NFC）。

## 真实回合暴露的三个局部缺陷与修复（A5：复现→修复→回归）

| # | 缺陷（真实回合复现） | 修复 | 回归 |
|---|---|---|---|
| 1 | traversal 不校验 target-descriptor 与策略声明的一致性：descriptor="Network & internet" 时静态策略仍放行 `toggle:Wi-Fi`，模型错误切换了 Wi-Fi（run-20261006-045101-746，guard reason="declared targeted toggle…"） | `SettingsActionPolicy.ValidateTraversalTargetDescriptor`；Host.Dsh 在任何设备动作/咨询前 fail-closed（`CONFIG_CONFLICT` rc=2） | `TraversalTarget_UndeclaredByPolicy_IsConfigConflict`（红→绿）；端到端：CONFIG_CONFLICT 在 am start 前打印 |
| 2 | Guard 可见性冻结规则绑定字面 "Wi-Fi"（SettingsActionPolicy.Evaluate）：非 Wi-Fi 任务穿过 Wi-Fi 开关可见页时导航被无关目标冻结 | 由 `policy.DeclaredTargetSwitchLabels`（toggle: 前缀派生）替代字面量 | `UndeclaredSwitchVisible_DoesNotBlockNavigation`（红→绿）；既有 `VisibleTargetSwitch_RejectsUnrelatedNavigation` 保持绿 |
| 3 | traversal objective 硬编码 Wi-Fi 文案（HostRunner.cs:210）：NFC 任务的模型收到的目标却是 "Confirm the Wi-Fi state…" | objective 由 `TargetSemanticDescriptor`+`TargetState` 派生 | 复现=NFC 首回合 consultations.json 的 objective；修复后 objective="Confirm the NFC switch state…leave it checked" |

## 真实回合（修复后；全部 4 级证据中的 ENVIRONMENT 级）

| 回合 | run dir | 终局 | delivered | 关键事实 |
|---|---|---|---|---|
| NFC-B1（未知状态安全停止） | rounds/nfc-b1/run-20261006-050036-006 | `TerminalNotProven (evidence-insufficient)` | 4（3 navigate+1 back） | **零目标 effect、零 toggle**；模型 defer 理由可追溯（"NFC switch not present in current Connection preferences hierarchy…"）；出现 **dispatch 前拒绝**（verdict=2 "multi-step act is not policy-guardable"） |
| NFC-B2（错误目标拒绝） | rounds/nfc-b2/run-20261006-050318-958 | `TerminalNotProven (evidence-insufficient)` | 5（3 navigate+2 back） | **零非目标 effect receipt**；零 desiredState 提议（拒绝分支未发生——如实记录）；设备 `wifi_on` 保持 1 |
| Wi-Fi 同链正回归 | rounds/wifi-regression/run-20261006-050719-090 | `Completed (terminal-emitted) / Completion` | 2（navigate） | 修复后的链上正确目标路径完好：已满足态导航后 noAction、无重复切换、`wifi_on=1` |

每回合 run dir 含 facts.json（终局/guard/digest）、consultations.json（objective/决策/耗时）、exec.journal（prepare/submission/receipt）、settings-trace.json。

## 验证声明

```yaml
CONTRACT:
  method: validate-testset-manifests（3/3）；CONFIG_CONFLICT 端到端（am start 前拒绝）；certify --check
  expected: 声明一致、冲突 fail-closed、认证不变
  actual: PASS——manifest 3/3；CONFIG_CONFLICT 在设备动作前打印（rc=2）；certification 28 files 0 violations
DETERMINISTIC:
  method: SettingsActionPolicyTests（含 2 个新红→绿回归）+ Host.Tests 全量 + Agent.Dsh.Tests
  expected: 缺陷修复有回归、全量绿
  actual: Host.Tests 159/159；Agent.Dsh.Tests 132/132；策略+Director 21/21
SCENARIO:
  method: 无（本 Change 闭环面在 ENVIRONMENT；确定性对面沿 AGT-014 故障矩阵不变）
  expected: —
  actual: NOT_APPLICABLE（确定性对面 46/46 由 AGT-014 证据承载，未改动）
ENVIRONMENT:
  method: 两个 NFC 真实回合 + 一个 Wi-Fi 同链正回归（真实模型/设备/ADB）
  expected: A1 未知状态零目标 effect+诚实有界终止；A2 零非目标 receipt+拒绝机制 live+正确目标可执行；A4 分层
  actual: PASS（见回合表）；toggle-non-target 事件未发生（模型未提议诱饵）——不变式成立、分支如实记录
```

## 运维注记

- E2E 预检测试会占用 3081 的 product-session 挂接且不 revoke——实例需重启后才能跑正式回合（已按此操作；后续可考虑给 E2E teardown 补 revoke）。
- B1 首回合（run-20261006-045101-746）为缺陷 1 的复现证据：错误目标 Wi-Fi 被放行切换（已随即恢复 wifi_on=1）；该 run dir 保留不删（失败尝试是证据）。

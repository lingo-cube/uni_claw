# Tasks — physical-wifi-off-to-on-minimum-semantic-loop

> **本 change 当前仅交付 proposal + design + specs + tasks（proposal-only）**。下方任务为
> `PROJECT_LEADER_IMPLEMENTATION_AUTHORIZATION_DECISION` 批准后的实施清单（两切片顺序执行），完成一项立即勾选 `- [x]`。
> 实施前必读: proposal.md + design.md + specs/physical-wifi-off-to-on-minimum-semantic-loop/spec.md +
> `docs/decisions/provider-foundation-reconciliation.md` + `docs/decisions/implementation-authorization-physical-wifi-off-to-on.md`。
> 实施遵守: 宪章 §33（emulator-only）、§54（Responsibility → Authority → State Owner → Interfaces →
> Implementation → Verification）、裁决 7（单一 Runtime slice）、I-4 / I-10 / 裁决 10（evidence 纪律）、
> 以及 spec「实施约束」条目（无 svc wifi / cmd wifi / 隐藏 emulator API / 直接 WorldState 改写 / 场景状态注入）。

# Slice 1 — REALITY_COMPOSITION_FOUNDATION

> 范围: 生产组合根 + Host 项目/运行时接线 + 真实 IEnvironment 构造路径 + Fake/Replay/Simulation 保持测试侧 +
> Startup.AttachAsync 落地 + async seam 修复。
> 证明: **Agent 可以以真实环境依赖图运行**。
> 禁止: WiFi 行为实现、provider registry、provider discovery、capability redesign。

## 1. 门与基线（Slice 1 前置）

- [x] 1.1 宪章 §33 门决策记录（emulator-only，不接真实手机）写入 `docs/decisions/` —— **任何真实接线实现前必须完成**（`docs/decisions/section-33-emulator-only-gate-physical-wifi.md`）
- [x] 1.2 基线快照：实施前既有确定性套件 + ArchitectureGuardTests + Perception 套件全绿记录（回归基准，供 3.3 对比）

## 2. 生产组合根（新宿主项目）

- [x] 2.1 新建宿主项目（如 `src/UniClaw.Runtime.PhysicalHost`，引用 Runtime + Adapters），加入 sln；`Main` 入口仅做构造/接线/预检/生命周期，无任何语义决策
- [x] 2.2 接线 `AdbDeviceResolver`（serial 解析：单设备确定性、多设备 fail-closed）→ `AdbDevicePreflight`（4 轴 readiness 含真实截图探针）→ `PhysicalEnvironment(AdbScreenshotSource, LocalVisionPerceptionSource, ImageSwitchStateProvider, AdbDispatchTarget, foregroundApp, displayW/H)`
- [x] 2.3 将组合出的 `PhysicalEnvironment` 注入 `Startup` / `Traversal` / `Recovery`（spec: 生产组合根接线真实 IEnvironment）
- [x] 2.4 Guard 复核：`src/UniClaw.Runtime` 对 Adapters 零 ProjectReference（Guard 1 保持）；宿主项目是唯一接线点；Runtime 核心无环境选择逻辑（spec: Fake→Real 过渡显式）
- [x] 2.5 Tier 1 测试（无 emulator 依赖，入普通套件）：组合根在注入替身（fake runner/source）下可构造；预检失败 → `NotReady(显式原因)` 且 ActionHistory 为空（`tests/UniClaw.Runtime.Tests/Composition/PhysicalHostSlice1CompositionTests.cs`，9 断言全绿）
- [x] 2.6 **Falsifier F1 — Fake 环境无法进入生产路径**：架构断言 —— Runtime 核心零 Adapters 引用；宿主项目是 `PhysicalEnvironment` 唯一生产构造点；Runtime 内不存在 Fake-vs-Physical 的 flag/switch/环境选择分支；Fake/Replay/Simulation 仅存在于测试项目（F1 三断言全绿：宿主源码无 Fake 标识符 / Runtime 无环境选择 flag / 宿主 csproj 仅 Runtime+Adapters）

## 3. 真实 IO deferred seam 落地（最小核心改动）

- [x] 3.1 `Traversal.ExecuteStep` 系列 async 化（Traversal.cs:39-41 自带裁决「Phase 4 接入真实 IO 时改为异步形状」）；语义（Select→Check→Execute→Observe→Verify→Branch、journal、retry、authority）不变（新增 `internal async Task<TraversalStepResult> ExecuteLoweredActionAsync`，Agent 语义闭环 await 消费；`ExecuteStep` 同步重载保留 Phase 1 Fake 路径）
- [x] 3.2 `Startup.AttachAsync` 落地：预检门控 → serial → Ready；失败 → `NotReady(原因)`（替换 Startup.cs:99-104 no-op）（attach 注入 delegate，null=Phase 1 no-op；失败→`NotReady(设备预检失败（Attach）：<原因>)`）
- [x] 3.3 回归：Tier 0 既有确定性 Fake 套件（SC-P1-001..005 + frozen 13 capability + Guard + Perception）全绿 —— 证明 async 化未改变 Fake 语义（环境同步完成 → 行为等价），对照 1.2 基线（**915/915 通过**，含 9 条新 Tier 1）

## 4. Slice 1 证明（必须）

- [x] 4.1 **证明：Agent 以真实环境依赖图运行** —— 组合根在 emulator 上启动 → 预检通过 → Startup Ready → 一次 `ObserveAsync` 返回新鲜 Observation（序列推进）→ Agent 建立初始 WorldBelief。本切片不实现任何 WiFi 行为（Goal 仅用于观测闭环，不做开关切换）（live emulator-5554：PROOF-A/B/C 全 True；belief seq=2 新鲜观测；ActionHistory 仅 1 次 LaunchApp）
- [x] 4.2 **Falsifier F2 — 无真实设备 → 零分发**：预检失败/设备断连 → NotReady（启动前）或运行中零动作分发（Startup 未 Ready 前 Traversal 不得执行任何 dispatch）；双入口断言（Tier 1 F2 断言 + live `PROOF-F2 deviceUnavailable=true zeroDispatch=true zeroTraversal=true`）

# Slice 2 — WIFI_SEMANTIC_LOOP

> 范围: WiFi capability 执行 + ADB-backed action path + 动作后 fresh screenshot + perception 验证 + GoalEvidence 闭合。
> 证明: Goal WiFi OFF→ON；**唯一成功条件** = Action receipt + Fresh Observation + Perception Evidence + GoalEvidence
> （dispatch receipt ≠ world state change）。
> 开始条件: Slice 1 证明（4.1/4.2）+ 3.3 回归全绿后，才允许进入本切片。

## 5. Emulator 现实校准（emulator-5554）

- [ ] 5.1 首次真实运行录制 WiFi OFF→ON 转换对（screencap + 感知证据 + 序列），存校准资产目录并附 provenance（时间/设备/序列/来源说明）
- [ ] 5.2 以录制现实 provenance 替换 `SYNTHETIC_STATE_TRANSITION_PENDING_REALITY_CALIBRATION` 标记（RealitySeededSettingsFixture.cs:138,:147,:158；RealitySeededWifiScenarioTests.cs:7-13 同步）；录制资产仅作测试侧 Fixture 数据，**不得注入生产路径**（约束: 无场景状态注入）
- [ ] 5.3 Tier 3 校准测试：`ImageSwitchStateProvider` 对 OFF 资产判定 false、ON 资产判定 true，确定性可重复；SYNTHETIC 标记不再出现在状态转换数据中

## 6. OFF→ON 语义闭环（Tier 2 真实集成）

- [x] 6.1 端到端：`SemanticGoalInput("WifiConnectivity","Enabled",true)` → capability 恰好一个匹配 → SetSwitch(true) 授权+lowering → 物理 tap dispatch → **fresh Observation（post-dispatch 序列推进）** → perception → `ISwitchStateReader` 状态验证 → SATISFIED（`GoalEvidence.SourceObservationSequence` 指向 fresh 观测）
- [x] 6.2 **Falsifier F3 — dispatch 成功但无观测不得满足 Goal**：ADB 返回 Dispatched 但世界未变 → 非 SATISFIED 终止（dispatch 收据 ≠ 世界变化；违反 I-10/裁决 10 的路径必须被测试证伪）
- [x] 6.3 **Falsifier F4 — 陈旧截图不可验证成功**：验证仅接受 fresh Observation（post-dispatch 序列推进，Traversal.cs:245「动作后观测序号未推进」fail step）；`SwitchStateValidation` 陈旧帧 fail-closed 测试；使用 pre-dispatch 截图断言 SATISFIED 的路径必须失败
- [x] 6.4 **Falsifier F5 — 失败动作不误触发恢复**：单次 dispatch 失败 → `TraversalStepResult.Failed(结构化原因)` → 经 Container 转交 Agent 决策（SC-P1-004 escalate 不偷权）；恢复仅按 Agent scope 规则进入，不因单次失败自动触发（Recovery 入口断言）
- [x] 6.5 **Falsifier F6 — provider 失败不产生语义成功**：perception 失败（INFRASTRUCTURE_FAILURE/MALFORMED_RESPONSE/SCHEMA_FAILURE/TIMEOUT 等 fail-closed 诊断）→ 无候选 → Unknown → STATE_EVIDENCE_REQUIRED；dispatch 失败 → step Failed；两条路径均不得产生 SATISFIED
- [x] 6.6 幂等/Unknown 变体：世界已满足 → NoOp 零物理分发（幂等）；状态 Unknown → STATE_EVIDENCE_REQUIRED 零分发
- [x] 6.7 **约束断言（架构检索 + 测试）**：`src/` 无 `svc wifi` / `cmd wifi` / 隐藏 emulator API（emulator console wifi 命令、UiAutomator 隐藏接口）调用；无直接 WorldState/WorldBelief 改写（状态仅经 Observation evidence 进入判定）；生产路径无场景状态注入
- [x] 6.8 Tier 4 trace 证明：闭环运行 trace 可重建 Goal → capability → token → dispatch → observation → verification 因果链（runtime-observability-trace-foundation 契约，只追加不改写 I-2）

## 7. 文档与验证（两切片完成后）

- [x] 7.1 CI 约束记录：Tier 2 需 emulator 前置（显式失败，不静默 Skip）；若 CI 无 emulator，记录降级策略（design.md Open Question 3）
- [x] 7.2 `scripts/check-consistency.sh` ALL PASS；ArchitectureGuardTests 通过；AGENTS.md 导航如有新增目录则同步
- [x] 7.3 `dotnet build` 0 警告 0 错误；`dotnet test` Tier 0/1/3 全绿；Tier 2 在 emulator 前置下通过
- [x] 7.4 Runtime 证明要求核对（design.md）：SATISFIED + fresh GoalEvidence + 六项 falsifier 至少各一条非 SATISFIED 证据
- [x] 7.5 评审通过后 `/opsx:archive`，提取 decisions 同步宪章/契约/导航

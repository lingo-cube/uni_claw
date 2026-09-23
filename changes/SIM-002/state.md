# SIM-002 — Simulation Baseline Compliance

lifecycle_state: implementing · disposition: none · depth: decision-heavy · base: c3ef5b30

## Intent

不是继续设计仿真，而是**让当前仓库重新符合已冻结的仿真架构基线**。
外部审阅（第三轮，2026-09-22）确认：Architecture CLOSED / Implementation Compliance OPEN。
本 change 按 Gate 顺序闭合差距。

## 核心裁决（用户 2026-09-22）

```text
G1 Product Host 无 Simulation / Replay 依赖闭包
G2 Golden certification 不可由 test runtime 自行重新签发
G3 Scenario status 来自真实 test execution，而非 JSON 自报
G4 C7 v0.2 能准确描述当前 hybrid Agent realization
```

## Scope

### Gate 1 — S1 Product Host 边界剥离

从 `src/UniClaw.Host/` 剥离 Simulation/Replay 组件：
- `ReplayPerception` → 移出 Product Host
- `V0Runtime`（Simulated FrameFeed）→ 移出
- `ServicePerception`（ServiceReplayFrameFeed）→ 评估归属
- `HostRunner` 的 `EffectProfile.Simulated` 默认值 → 改为 fail-closed
- CLI `--replay` / `--service` 路径 → 剥离或移入 DevHost

验收：Product Host 依赖闭包机械可证不含 Simulation/Replay 路径。

### Gate 2 — S2 Golden 认证持久化

从 `ScenarioBundleDigest.Sealed()`（runtime auto-seal）改为：
- 认证记录持久化到 scenario JSON（certified digest + change id + artifact hash）
- test runtime 只允许 Verify（不允许 re-Seal）
- 覆盖率工具检查 certified hash 与实际 runtime hash 匹配

验收：修改 Expected 后重跑测试不再自动通过认证。

### Gate 3 — S3 覆盖率真值链

`tools/scenario-coverage.py` 升级：
- 读取 `dotnet test` 结果（而非 JSON 自报 status）
- test-case ↔ scenario-id 映射
- 无结果 / 结果不匹配 / schema 违规 → exit 1

验收：`python3 tools/scenario-coverage.py` 输出的 passing 代表
"此 HEAD 此期望此测试执行刚刚通过"。

### Gate 4 — S4 C7 v0.2 混合 Agent Realization

推进 `simulation-baseline-v0.2`：
- C7 拆分：`agentDecisionRealization` + `goalEvaluationRealization`
- 不顺手改其他 C1-C9 条款
- 场景 schema 加对应字段

### Schema Hygiene — S5/S6/S8

- 修 `scenarios/schema.json` 非法 JSON
- Schema v2：补 realization / certification / deviation / security 字段
- `source: generated` → `synthetic`（Phase B 前的语义清理）

## Out of Scope

- Phase B（ScenarioBuilder / IStimulusScheduler）— **G1-G3 闭前不启动**
- 任何新仿真功能 / 新场景
- C1-C6 / C8-C9 的基线修订（仅 C7 走 v0.2）

## Decisions

- D1 Gate 顺序 = S1 → S2 → S3 → S4 → S5/S6/S8，不可并行
  （S1 是产品级冻结边界违规，最高优先）
- D2 不开第四轮文档审阅——直接进 compliance implementation
- D3 S1 完成标准 = 依赖闭包机械可证（不是"代码挪了"）
- D4 Phase B 冻结到 G3 闭合

## Status log

- 2026-09-23 · G2 closed（golden 认证持久化 + Verify-only）·
  scenarios/ 17 条目全部带 certification 块（expectationsDigest /
  runtimeSourceHash / certifiedByChange / certifiedAt）；唯一写入口
  tools/scenario_certify.py（无 --change 拒绝执行——C8 搭乘协议执法化）；
  test runtime 无写回路径，双执法面：C# 验证器
  （ScenarioCertification.VerifyFile + ScenarioCertificationTests，与
  python canonical 双语言独立实现互为一致性检查）+ 覆盖率工具
  （违规 exit 1）。取舍记录：runtime hash 取 Kernel+Agent**源码**哈希
  而非 DLL 哈希（DLL 哈希依赖构建环境、不可跨机复现；源码哈希内容
  寻址、双侧可独立重算）。实现期实测抓到跨语言分歧一例：python 侧
  曾以绝对路径入帧（自洽但嵌机器路径），C# 侧相对路径——修复后双侧
  在真实数据上逐字节一致。验收语义实测（spec 原文场景）：篡改
  expectations.effects 不带重认证 → C# 报「摘要不匹配」+ 覆盖率工具
  exit 1；源码哈希过期同理报红；恢复后全绿。已知语义（按设计）：
  Kernel/Agent 源任何后续改动使认证失效，重认证 = 显式 tool 跑
  --change <致因change>，期望值是否随动由该 change 评审裁决。
  验证：level DETERMINISTIC——method RED 先行（17 条目缺 certification
  块全红）→ 盖章 → GREEN；全量 Host 18/18 · Kernel 467/467 ·
  Simulation 143/146（3 失败为 HEAD 存量 RED：ImportReDrive /
  AsyncImportRedrive / AsyncPerceptionRealization，与本 change 零接触，
  stash 复验在案）；evidence 本条 status log + 仓库 HEAD + 认证测试。

- 2026-09-23 · G1 closed（follow-up slice，接 b4b6865c 首切片）·
  产品 Host 仿真/回放闭包剥离完成：`V0Runtime` / `ReplayPerception` /
  `ServicePerception`（含 `FrameFeed` / `ReplayFrameFeed` /
  `ServiceReplayFrameFeed` / `Consult` / `FrameOccurrenceStrategy` /
  `DeterministicDeliveryDriver`）全部移入
  `tests/UniClaw.Simulation.Tests`（dev 档组合根 `DevLoopRunner` 承载，
  双 Host 对称：同一 Kernel 真件）；`HostRunner` 外部缝 fail-closed
  （Live / ConsultAgent 缺席即抛，无仿真默认档，EffectProfile.Simulated
  枚举删除）；CLI `--replay` / `--service` / `--effect adb` 剥离，
  `--analyze` 改经产品面 VisionServiceSession，无参调用 fail-closed 退出；
  产品孪生独立成件：`ScreenFrameOccurrenceStrategy`（live 路径依赖）+
  切片 1 已落 `HostUtilities` / `FrameOccurrenceStrategy`。机械执法扩面：
  `ProductHostClosureTests` 禁词 + Host csproj 引用白名单；`HostClosureTests`
  同步（2026-09-20「感知回放属能力层」定性撤销备注）。
  验证：level DETERMINISTIC——method 全量 `dotnet test`；expected 闭包
  测试 GREEN + 零回归（已知 RED 不变）；actual Host 18/18 · Kernel
  467/467 · Simulation 140/143（3 失败经 stash 复验为 HEAD 存量 RED：
  ImportReDrive×1 / AsyncImportRedrive×1 / AsyncPerceptionRealization×1，
  与本切片无关）；evidence 本条 status log + 仓库 HEAD。
  附带修正：切片 1 提交的 `FrameOccurrenceStrategy.cs` 含编译错误
  （`ProposedOccurrence` 无 `EvidenceId` 参数——b4b6865c 提交时未跑
  `dotnet build`，其守护脚本仅为文档检查），本切片已修（ui.detect.class
  → Role-only occurrence）。

- 2026-09-23 · G1 first slice（b4b6865c）· `HostUtilities`（VirtualClock
  / AnchorDetection / ExtractJson）+ `FrameOccurrenceStrategy`
  （ui.detect.*.class）产品侧提取；`LivePerception` 切换引用。（状态
  记录滞后补登；该切片含上述编译错误，已在 follow-up slice 修复。）

- 2026-09-22 · created·persisted · 外部第三轮审阅触发（S1-S8 + P1-P3），
  用户裁决按 Gate 推进，Phase B 暂停

# Human 裁决包 — ABG-001（Phase 3 治理半边）与 UAP-001（Phase 4 统一异步 Perception）

> 整理日期：2026-09-16 · 分支 `uni-harness` · HEAD `40f190c6`（所有 Phase 1–4 工作
> 均在工作树未提交；本裁决包不改变任何 change 状态，只供 Human 裁决）
>
> 今日（2026-09-16）Leader 第一手复跑：**Kernel.Tests 382/382 · Agent.Tests
> 17/17 · Simulation.Tests 112/112 = 511/511，0 失败**（与 UAP-001 state 记录一致）。

---

## 0. 一句话总览

- **ABG-001**：`implemented · disposition: none · Human re-review pending` ——
  2026-09-14 完成 13 项修复回归（RED→GREEN），**提交 re-review 待裁决**，未 commit。
- **UAP-001**：`implemented · disposition: none` —— 2026-09-14 完成复审追加
  S5b + 引擎重写（D13），511/511 绿，**提交复审待裁决**，未 commit。
- 两个 change 均显式声明**不自封 CLOSED、不 commit**，只等 Human 裁决。

---

## 1. ABG-001 — Minimal Bundle v0 Asset Governance & Baseline Promotion

### 1.1 是什么 / 为什么

用 Phase 1 已有 Minimal Scenario Bundle v0 资产，建立最小资产治理 tracer：
concrete filesystem store + 四类责任分离（Capture Producer / Asset Governance
Authority / Scenario Assertion Owner / Human Promotion Authority）+ 内容寻址
identity + 关键 metadata 可检索 + fail-closed 完整性门 + Baseline 显式晋升且
版本不可覆盖。补齐 roadmap §7.2.1/§7.3（H7）缺失的
`Capture→Register→Replay-Eligible→Baseline-Promoted` 闭环。

> 范围边界：全部落 `tests/UniClaw.Simulation.Tests/`（test-side，产品程序集
> 零接触）；无新公共 Interface；不 commit；未经 Human Review 不自封 CLOSED。

### 1.2 上一轮 Human Review 结论（2026-09-14）与修复对照

Human 结论 **REQUEST_CHANGES**，Leader 依 UniFlow 失败边回 IMPLEMENT 修复，
十三项修复先 RED 后 GREEN。问题 → 修复对照：

| Human 发现 | 修复（已落入测试） |
|---|---|
| 状态字段不合规范 / Scope 类型名不一致 | state 修订，类型名统一 |
| Bundle 批注册部分写、故障留部分 identity | 先全量校验、后**单次 registry 发布**，发布失败回滚内存 identity/occurrence |
| 仅存 digest 文本 | 完整 bundle JSON 内容寻址存储，重载/re-drive 复现同一 digest |
| 晋升证据不关联对象 | 晋升核对 SourceBundle / claim / scenario / **祖先 lineage**，并同源重跑两遍 digest |
| 同内容跨环境 metadata 丢失 | 同内容多 occurrence（identity 不变，环境/lineage 成独立 occurrence） |
| Trace opaque 边界表述冲突 | Opaque/Quarantined Trace 仅作诊断 bytes 保管，不得获 Stimulus/Baseline 语义（表述澄清 + 测试） |
| Store 暴露未校验 baseline 写入路径 | 私有 baseline 写入仅由嵌套 promoter 可调用；组合根不暴露 Store |

另含：断言实际变化必须新 Bundle version（v2）+ supersession 链；v2/claim 失败
发布不暴露未持久化记录；sealed baseline tamper read 拒绝。

### 1.3 Acceptance 1–9 逐项证据（state 声明 + 今日复跑）

| 验收 | 证据 |
|---|---|
| 1 改名不变性：bytes 同 → identity 同；跨环境独立 occurrence | `AssetGovernanceTests`（含 cross-environment occurrence、corrupted-blob idempotent re-registration） |
| 2 关键 metadata 可检索；缺失字段显式 Unknown | 按 UI system 版本 / app build / Runtime artifact hash / lineage parent 检索用例；Unknown OS/runtime、wrong indexed version 反例 |
| 3 fail-closed 四臂 + 发布失败不残留 | 缺 bytes / 错 hash / 断 lineage / 错 schema 各自 typed 首因；late-missing-asset、registry publish failure 反例 |
| 4 Baseline 仅显式晋升 | 无 Human 晋升 → 无记录；证据缺失/来源失配/祖先 lineage 不完整 → 拒绝；scenario/digest spoof 反例 |
| 5 断言修改 → 新 Bundle v2，v1 保留可解析 | 真实 v2 assertion + failed v2 publish + sealed baseline tamper read |
| 6 authority 越权拒绝 | CaptureProducer/AssertionOwner/Governance 直接晋升 → 无路径或拒绝 |
| 7 sealed Trace 只经 Importer 派生 Stimulus | 未封存/Quarantined 不可导入或消费；opaque 可保管、不得挂 claim |
| 8 治理层零产品接触 | `ProductHostClosureTests` 所在 Kernel 套件 GREEN；无新公共类型 |
| 9 全量回归 + diff check + 精确路径 status | 见 §1.4 |

### 1.4 验证摘要

- state 宣告（2026-09-14）：Kernel 382/382 · Agent 17/17 · Simulation **94/94**
  = **493/493**（ABG 20/20 专项）；RED→GREEN 已观测；`git diff --check` 与未
  跟踪文件 no-index whitespace check 均干净。
- 今日复跑：511/511（Simulation 已含后续 UAP-001 增量到 112），无回归。
- 路径纪律：仅三条 ABG 路径修改；并行 dirty（Phase 5 痕迹、UAP-001 等）
  未触碰；未 commit。

### 1.5 Residual risks（裁决要点：均为后续扩展项，None 阻塞本 change 验收）

1. 单机 filesystem realization——在线 registry / 矩阵 / migration / tombstone /
   retention / redaction 全属 **H8 延后项**，未做（明示 out of scope）。
2. Compatibility Judgment 未独立成面（只返回记录集合，不出
   Exact/Compatible/Incompatible/Unknown 判定对象）——待真实兼容矩阵 buyer。
3. 部分晋升证据仍是自报 boolean（per-cycle-zero / trace-arms）——两遍 digest
   已同源重跑核对，其余待后续 evidence artifact。
4. 单文件发布边界：发布失败回滚内存，孤儿 blob 可能残留；无 fsync 级
   crash durability，不宣称事务原子。
5. 重驱动仍依赖仓库 fixture 的 golden 路径，未证明脱离 fixture 的移植式 replay。
6. Human 代理边界：test-side `HumanPromotionAuthority` 不是身份认证。
7. claim/baseline id 跨 store 合并语义未定义。
8. TraceModel 与 SealedTraceStore 的 canonical rendering **双实现**（TRW-001
   亦标注）——两份实现需合一，属 TRW-001 follow-up。

### 1.6 裁决选项（ABG-001）

- **A. CLOSED（推荐）**：Acceptance 1–9 均具名证据 + 今日 511/511 复跑无回归 +
  修复对照闭环 + residual 全部为已声明的后续项（H8 / 兼容矩阵 / 双实现合一）。
  Human 授权后置 `closed · disposition: none`，可 commit。
- **B. APPROVED_WITH_FOLLOWUPS**：若 Human 希望把"canonical rendering 双实现
  合一（TRW-001 follow-up）"或"Compatibility Judgment 独立面"显式钉入下一
  change，以此 disposition 落档。
- **C. REQUEST_CHANGES**：仅当 Human 认为某项 Acceptance 证据不足 / residual
  中某项应在本 change 内解决。需列出具体点。

---

## 2. UAP-001 — Phase 4 统一异步 Perception 最小垂直 tracer

### 2.1 是什么 / 为什么

证明 Phase 4「统一异步观察消费语义」（roadmap §10）：Fast-only / Fast→Slow /
Slow-only / one-shot 四种 Perception realization 对 Runtime 顶层同构——观察
结果一律以 ObservationProposal 经 P2 admission → P3 accepted Evidence →
relevance/reconciliation 进入 belief；late / duplicate / out-of-order /
partial / failure / timeout / cancel-then-late 受控且 fail closed；Perception
不直接改 WorldBelief；Fast/Slow 标签不进入 Run/World 顶层模型。含新增
「小标题/副标题误作菜单项」场景（复用 FSV-001 真值资产）。

> 授权例外：修复已提交产品缺陷 `PersistentRevisionCollections.cs`
> （BuildCanonicalView 以 null 为重建基 → canonical 枚举静默丢祖先键），
> Human 2026-09-14 授权，2 处各 4 行 + 回归测试转正（D11）。

### 2.2 复审追加（Human 2026-09-14 指令）与修复

Human 要求追加 **S5b**（拉取节奏不变性 + 完成后迟到 failure 反例）：
RED 对照证实旧引擎两个缺陷（迟到错误 Fast 入交付批、迟到 failure 翻转已
完成 op）→ `PumpAll` 重写为**事件时间逐项推进**（D13：到达事件 + deadline
事件按事件时间顺序处理，状态转移及时发生；交付批次与拉取节奏无关；完成后
迟到结果一律隔离）→ S5b/S3a/S8c GREEN → 三套回归 511/511。

### 2.3 Acceptance 1–6 逐项证据

| 验收 | 证据（report §1–§6；八组场景 15 测试，含 S5b） |
|---|---|
| 1 八组场景 GREEN（四个 realization 顶层同构；迟到/重复/乱序/partial/failure/timeout/cancel 全 fail closed；Perception 不改 WorldBelief；Fast/Slow 不进顶层模型） | `AsyncPerceptionScenarioTests` 15/15 + 回归 18/18 |
| 2 四 realization 同语义输入 → 规范化世界结论一致（首个分歧报出） | `AsyncPerceptionRealizationTests` |
| 3 真实 live one-shot/fast 策略路径 + recorded deterministic adapter 真实执行 | 真 `FastPerception` + `LiveVisionStrategy` + 录制 golden-run-v1 response（D8） |
| 4 每场景记录 observation/admission/reconciliation/revision/Effect 计数与虚拟延迟 | report §5；Trace 仅诊断（DisabledRunTrace 零耦合负证据） |
| 5 三套回归实际数 + diff check + 未跟踪文件空白检查 | 见 §2.4 |
| 6 H9 correlation 留在 tracer（无产品 Interface 变更，结构性断言）；H10 无 preliminary 自动授权路径 | S2b/S6/S8b fail-closed 断言 |

关键语义发现（tracer 级，未升格产品语义，均已如实声明）：
D9 整帧替换与 partial 交付不相容 → descriptor 键合并；D12 同流 Revise 在
完成窗口内覆写 / 跨 producer Conflict 保持；D13 事件时间完成语义。

### 2.4 验证摘要

- state 宣告（2026-09-14）：Kernel 382/382 · Agent 17/17 · Simulation 112/112
  = **511/511**；RED 留痕（实现前 15/16 失败 + S5b 旧引擎失败）；diff check
  干净；新增未跟踪文件 7 个 PASS。
- 今日复跑：511/511 一致。
- 产品缺陷修复：`CanonicalEnumerationRegressionTests` 通过
  （修复前 count=3/enum=[s.late]，修复后枚举全量）。（D11 证据 report §4）
- 路径纪律：`src/` 唯一改动为 D11 授权修复；协议/基线/ADR/并行 change 零触碰。

### 2.5 Residual risks（裁决要点）

- **R1** Slow/full-model 无真实 adapter——只证异步时序/失败/相关性语义，不
  证真实 Slow 质量（诚实声明，Phase 7 才重接真模型）。
- **R2** hold-until-complete 是 tracer 级投递策略（D3），非冻结产品语义；
  产品化时渐进投递 + sufficiency 判定待 **H9/H10**。
- **R3** `KernelRunDriver` GroundingFailed 后无 Runtime 级再观察循环——定向
  Slow 由 Perception realization 内部完成，能力缺口留 **Phase 5/6**（语义边
  界已在 report 标注）。
- **R4/R7** occurrence 景观与 descriptor 键合并的 typing 仲裁/重复文本语义待
  UWM 后续裁决。
- **R6** 顺序敏感（窗口限定）：conflict-aware grounding/assurance 是明确缺口，
  本 change 明示不做，待 H9/后续 Change。
- **R8 ⚠ 建议 Human 目检**：D11 产品缺陷修复虽经 510 项回归 + 独立回归测试，
  但 `PersistentRevisionCollections` 完整语义测试面属 WMP 谱系——**建议 Human
  对 WMP 系测试做一次针对性目检**（UAP-001 换取了超出 tracer 范围的产品代码
  修复，这是本 change 最值得 Human 亲自确认的一点）。
- R5 无设备 live 环境臂（Phase 7）。

### 2.6 裁决选项（UAP-001）

- **A. CLOSED（推荐，附 WMP 目检）**：Acceptance 1–6 具名证据 + 511/511 +
  D13 反例证伪与修复闭环 + H9/H10 结构性断言；residual 全部为已声明后续项。
  建议 Human 裁决前/后顺手目检 WMP 系测试（R8）。授权后置
  `closed · disposition: none`，可 commit。
- **B. APPROVED_WITH_FOLLOWUPS**：把 R3（Runtime 再观察循环）与 R6
  （conflict-aware grounding）钉为 Phase 5 的显式输入。
- **C. REQUEST_CHANGES**：仅当某项 Acceptance 证据不足。需列具体点。

---

## 3. 联合裁决动作（裁决通过后的建议执行序列）

1. Human 对两 change 各自给出裁决（可用上方 A/B/C）。
2. 通过 → Leader 更新两 change 为 `closed`（disposition 按裁决）+ status log
   补 Human closure 记录。
3. 按纪律分批 commit（工作树含 Phase 1–4 全部未提交内容；commit 顺序建议：
   RFS-001 闭包 → TRW-001 → WRC-001 → UAR-002/SKL → ABG-001 → UAP-001，
   或按 Human 指定的打包粒度）。
4. TRW-001 follow-up「canonical rendering 双实现合一」另立或并入下一 change。
5. 下一站 **Phase 5 — Effect 后验证 / Local Recovery / 策略预算**（H11/H12
   在途）；工作树已有 Phase 5 痕迹（`PostActionEffectVerification.cs` +
   `SimulationHost.cs` 已引用 `PostActionVerifications`），正式立项按 UniFlow
   UNDERSTAND → state 建立 → TDD tracer。

---

## 4. 本次复跑命令（可复现）

```bash
dotnet test tests/UniClaw.Kernel.Tests --no-restore   # 382/382
dotnet test tests/UniClaw.Agent.Tests --no-restore    # 17/17
dotnet test tests/UniClaw.Simulation.Tests --no-restore  # 112/112
```
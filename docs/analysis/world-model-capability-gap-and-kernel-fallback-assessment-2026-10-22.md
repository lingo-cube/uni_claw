# World 模型胜任性 / 当前-设计 Gap / Kernel 兜底健壮性评估

> Status: `COMPLETE / EVIDENCE-BOUNDED`（只读评估，未授权实现）
> Authority: `NONE`（分析产物，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> Date: 2026-10-22
> Scope: 回答四个问题——①当前测试暴露了哪些问题；②用流程和公式推断，当前
> World 模型与设计的 World 模型各自能否胜任、gap 是什么；③Kernel 的流程设计
> 能否兜底、是否足够健壮；④上述问题中哪些是设计缺陷/遗漏、哪些是实现缺陷/遗漏。
> 输入：架构基线 L0-L3（§12/§14/§15/§20）、RUN-006 真机报告、
> WORLD-SEMANTICS-001/002、WORLD-OBSERVATION-001、WRC-003、TRAJECTORY-001
> 修复链、ENG-OPT-001、SETTINGS-COVERAGE-007、RUNTIME-PLAN-006；源码锚点
> 均为本次实测读取。

---

## TL;DR 判定

1. **当前 World 模型：部分胜任。** 在已实现角色（身份安全 / 补证闭环 /
   Slice 消费 / 导航-滚动关系）上胜任，有专项测试与真机有界样例双证据；
   对测试暴露的三个扩展需求（同 route 语义实体区分、覆盖层生命周期、
   语义目的地符合性）不胜任，且均被变更显式"保留未知"而非静默失败。
2. **设计的 World 模型：胜任其设计角色，但只承诺安全维度。** 设计的胜任观
   不是"全知"，是"显式不知道 + 消费侧 fail-closed"。活性（持续推进）设计上
   就不由 World 保证——感知持续失败 ⟹ 补证预算耗尽 ⟹ 诚实停止。
3. **Gap 不是架构性的**，集中在两点：图完备性（Contains/Overlays 有枚举
   无生产）与执法一致性（单一当前容器执法下沉到消费者、存在直读绕过、
   Uncertainty 退化为标量）。
4. **Kernel 兜底：对"世界不确定"兜得住（fail-closed 全覆盖、零盲走路径）；
   对"决策层循环"设计上就不兜**（不变量 3：Kernel 不是 Product T2 ownership
   的兜底；Effect != Completion）。实际防循环兜底（stagnation 指纹 /
   auto-skip / back-to-root）全部落在 Host 层，Kernel 内只有续授挡板一处
   近似物。安全维度足够健壮；活性维度有三处可击穿点。

---

## 1. 当前测试暴露的问题（按层归类）

### A. 世界侧（身份/关系）— 来源：WORLD-SEMANTICS-001 探针及后续变更

| # | 问题 | 状态 |
|---|---|---|
| W1 | 迟到观察回退当前身份（A(t1)→B(t3)→迟到 A(t2) 使 CurrentContainer 回退） | 已修（capture 时间 gate，SEM-002） |
| W2 | 冲突声明仍推进身份（New(B) 照样建立） | 已修（冲突阻断关联；保护范围 `ui.screen`） |
| W3 | 重访签名不稳（每轮新铸 occurrence id → 身份碎片） | 已修（改用 accepted 唯一元素描述） |
| W4 | 同 route 不同语义页无法区分 | 部分修（语义只丰富已识别容器；明确不声称能区分同 route 新实体） |
| W5 | typed 迟到保护不覆盖普通 claim | 已修（语义 claim 独立核对 exact capture） |
| W6 | 验证不核"语义目的地符合性"（IsVerified ≠ 到了正确的页） | 保留（设计上归 Assurance，ADR-0049） |
| W7 | 导航边证据引用弱（字符串引用、不要求 target revision=current） | 保留（串行 driver 缓解） |
| W8 | Contains/Overlays 关系枚举在、无生产；图投影只选 NavigatesTo | 保留（无真实 buyer，ADR-0026 纪律） |

### B. 决策/桥侧 — 来源：TRAJECTORY-001 提交链 + RUN-006 追加轮

skip 链三段丢失（菜单未教 / 载荷嵌套错 / Director 不认 typed union）、
no-submit-decision、localView DTO 缺映射、preset 漏钉（AGT-019 B6）、
slow 空文本（插件事件形状演进）——全部已修或已定位有界后续项。

### C. 流程判据侧（最重要的一组）— 来源：RUN-006 真机 + TRAJECTORY-001

- **M5 双零判据在弹窗循环下不可达**：每步 CANCEL tap 都"验证通过"→
  `stepsVerified` 持续 +1 → 双零窗口永不成立。真机实录 **96 步全
  verified ∧ 覆盖 37.5% 并存**（RUN-006 发现②）。
- stagnation latch 单发（intervention 只注入一次）、back-to-root 只在
  CoverageComplete 触发（深页卡死）、switch 目标被 Director 派发后必然
  guard 拒绝 → 空响应。三者均已在 Host 层修复（TRAJECTORY-001）。

### D. 既有失败

4 项（Kernel 1 + Agent.Dsh 3）已由 ENG-OPT-001 收口；最终 8 工程
2024/2024 全绿（commit 0acdc1d5，2026-10-22 01:18，本次评估基线 HEAD）。

---

## 2. 公式与流程推断

### 2.1 判定框架

```
胜任:      World胜任(C) ⟺ G_world(C) ⊑ R(C)
           （对每个消费面 C，World 的保证满足该消费的最低需求）
安全:      Safe ⟺ ∀消费路径 p: Unknown(p) → Barrier(p)
           （fail-closed 全覆盖，无盲走路径）
活性:      Live ⟹ Identity确认 ∧ Assurance验证 在预算内发生；否则 HonestTerminal
全局推进:  Progress = Σ s: ΔCoverageFacts(s) ≠ 0
           （TRAJECTORY-001 D3 裁决：清障步不计入进度）
```

RUN-006 的实证反例即最后一条的违反展示：

```
StepsVerified = 96（每步 verified）∧ ΔCoverage = 37.5%
⟹ 步级验证 ⇏ 全局推进        （"Effect != Completion" 的真机实例）
```

### 2.2 当前 World 模型逐消费面判定

| 消费面 | 需求 R | 当前保证 G | 判定 |
|---|---|---|---|
| Binding/Freshness | 当前身份可绑、freshness 三态 | revision 不可变 + ADR-0010 三态 fail-closed | 胜任 |
| 身份安全 | 单调性 / 冲突不推进 / 重访稳定 | W1/W2/W3/W5 修后全成立（专项 100/100 + 真机有界样例） | 胜任 |
| 导航/滚动关系 | 建边有据、位移有据 | NavigatesTo 事后验证接纳；滚动配准"无据保留未知"（WRC-003 19/19） | 胜任（W7 引用弱） |
| 图完备（父子/覆盖） | Contains/Overlays | 无生产；`Relations` 实际只有 NavigatesTo（WorldModel.cs:243；枚举在 ContainerAssociation.cs:28-31） | 不胜任 |
| 同 route 语义实体区分 | 语义身份等价 | 明确不声称 | 不胜任（诚实保留） |
| 语义目的地符合性 | "到了对的页" | IsVerified 只证变化发生 | 不胜任（设计上非 World 职责，见 2.3） |

### 2.3 设计的 World 模型判定

设计的胜任观是"显式不知道 + 消费侧 fail-closed"（基线 §12/§20.2，
ADR-0010/0012/0049）。三个缺口对回设计：

- **同 route 新实体** → 设计规定无区分依据不造身份（Ambiguous 不建
  identity）——"不知道"是正确行为；
- **覆盖层生命周期** → 基线 §22 明确冻结，等真实 buyer（ADR-0026 不为
  想象需求预造）；
- **语义目的地** → 设计上不是 World 的职责：ADR-0049 预期与观测分离，
  符合性判断归 Assurance。

结论：设计胜任其设计角色（安全维度）；**设计不承诺活性**——推进依赖
感知质量 + Control 补证 + 预算，感知持续失败 → 诚实停止。这是设计选择，
不是缺陷。

### 2.4 当前 vs 设计 Gap（逐不变量对照）

| 设计要求 | 当前实现 | 判定 |
|---|---|---|
| 不变量 18：WorldBelief 必须表达 uncertainty | `Uncertainty = new Uncertainty(conflicts.Count)`（WorldModel.cs:667）——冲突计数标量；结构性未知靠 ObservationNeed 单点补偿 | 部分（表达过薄） |
| §12.2 World Graph 含 entities/relations/interaction context | 只生产 NavigatesTo | 未达成（图不完备） |
| 单一当前容器应由 World 执法 | 执法下沉到消费者：两处各自 `Count==1` + 兼容回退（KernelRunDriver.cs:3117-3124）；多容器 → 静默 GroundingFailed | 部分（执法位置错层） |
| 冲突过滤一致性 | traversal Returned 直读 claim 绕过冲突检查（KernelRunDriver.cs:2183-2185 vs 3120）——悬案时可拿陈旧值 → 假 `return-failed` | 未达成（不一致） |
| Reconciliation 处理迟到/过期 | `ui.screen` + 语义 claim 已保护；通用 claim 全路径未证 | 部分 |
| revision 不可变 / evidence basis / Slice 派生 | 全部达成 | 达成 |

---

## 3. Kernel 兜底能力与健壮性

### 3.1 能兜住（有证据）

**对"世界不确定"——Kernel 的强项。** ObservationNeed ≠ null 时五条消费
路径全部屏障、零盲走：

- 观察 → 强制 Slow 补证（KernelRunDriver.cs:3711）；
- 验证 → `post-action-page-identity-unconfirmed` 拒绝
  （RuntimeAssurance.PostAction.cs:22-29）；
- Act → 无根则 `no-single-root-container`（KernelRunDriver.cs:1050-1065）；
- Traversal → `current-container-unknown` Pause（:2232-2237）；
- Terminal → `evidence-insufficient` 保持非终态（UniKernel.cs:818-819）。

辅助：补证子配额 4 次/5s 耗尽 → 诚实停止（不自动点 Back）；预算两层顶
（契约额度 + 续授 ≤ HardCap）；no-response 单次重试 + durable
fail-closed；Outcome Proof 三分终局（Completion/Failure/Safe-Stop/
Escalation）+ Delivery Closure。真机 97 周期零 divergence、零违规 confirm
点击是该设计的实证。

### 3.2 兜不住 / 弱（有证据）

1. **决策层循环——Kernel 级没有进度判据。** 唯一近似物是续授挡板
   `renewal:repeat-route-without-new-evidence`（KernelRunDriver.cs:611-619，
   仅在续授时查 `HasNewProgressFacts`）。真机 46× 同目标重派 Kernel 全程
   放行。实际兜底（stagnation 指纹 / auto-skip / back-to-root）全部落在
   Host 层（SettingsCoverageDirector.cs:55、SettingsCoverageRunner.cs:41）。
   这不是失职（不变量 3 + Effect != Completion），但 Kernel 单独不防循环。
2. **步维 HardCap 可被重启击穿**：`_stepsDispatched` 是 driver 易失字段
   （KernelRunDriver.cs:173），重启只从 journal 恢复咨询轮次，步数归零。
3. **两处执法绕过**：§2.4 直读 claim；冲突新鲜度窗 3 秒占位常量
   （KernelRunDriver.cs:153-156）且非相交悬案不阻 Act（依赖 agent 自律
   Skip）。
4. **need 清除无下限**：WorldModel 只在 `pageIdentity ∧
   associatedContainer` 同时成立时清 need（WorldModel.cs:681-685）——
   关联策略持续返回 null ⟹ 补证配额必耗尽 ⟹ 诚实停止。安全，但活性
   完全依赖感知质量。

### 3.3 判定

安全维度足够健壮（每个失效模式有显式路径、fail-closed 全覆盖、终局
诚实、Recovery 强制重入闭环）。活性维度：兜底在 Host 不在 Kernel，且
有三处可击穿点（重启步账、Kernel 级无循环检测、直读绕过）；Host 层
修复不在 Kernel 复用面内——换一个 Director 就失去保护。

---

## 4. 能力边界 vs 能力缺陷；设计问题 vs 实现问题

### 4.1 能力边界（设计上故意不做，不是病）

| 边界 | 直白说法 |
|---|---|
| World 只管"信什么"，不管"想干嘛" | 记录"页面上有什么"，永不记录"我想到哪去" |
| 不判断"到对了没有" | 能说"页面变了"，不能说"变到你要的那页了"——符合性归 Assurance |
| 无依据不造身份 | 分不清就是分不清，宁可标"未知"也不猜合并 |
| 父子/覆盖关系不建 | Contains/Overlays 有枚举没逻辑，等真实买方 |
| 不承诺"一直能推进" | 感知持续失败 → 花完补证预算 → 诚实停下；"安全地不知道"优先于"勉强往前走" |
| Kernel 不当保姆 | 不变量 3：Kernel 是执行边界，不兜各组件职责 |

### 4.2 设计缺陷/遗漏（设计文本层面缺了或没说死）

1. **没人负责"发现原地踏步"**——设计反复强调"步成功 ≠ 完成"，但全文
   没有一处指定零推进循环检测的 Owner。Traversal 不管完成、Kernel 不管
   推进，于是真机 46× 重派同一目标、96 步"全成功"配 37.5% 覆盖，系统
   全程绿灯。M5 指纹、auto-skip、back-to-root 是 TRAJECTORY-001 在 Host
   层事后打补丁，不是设计内生的。最大一条设计遗漏。
2. **Uncertainty 只提要求不定形状**（不变量 18）——没说表达成什么样、
   谁消费；留白直接传导给实现（见 4.3-3）。
3. **重启后的账本语义没闭合**——Checkpoint/预算列为 OPEN GATE（Phase 5
   未闭合），实现用易失字段填坑：进程重启，步数硬顶归零。
4. **"当前容器唯一性"没定执法者**——留白导致两个消费者各自实现、行为
   不一致（见 4.3-1）。

### 4.3 实现缺陷/遗漏（设计有要求或有缝，实现没做到位）

1. **返回时绕过冲突检查**——KernelRunDriver.cs:2183 直读 claim，旁边
   3120 的正规入口有冲突过滤，这条路没有；悬案时可拿陈旧值 → 误报
   "返回失败"。纯实现 bug。
2. **步数账本不持久**——`_stepsDispatched` 内存字段，重启清零，HardCap
   可被击穿。设计的 OPEN GATE 没关，实现交了最省事的答卷。
3. **Uncertainty 交白卷式实现**——把"不确定性"实现成冲突计数整数
   （WorldModel.cs:667）。设计留白 + 实现低配，各打一半。
4. **迟到/冲突保护只护了正门**——`ui.screen` 和语义 claim 有保护，普通
   claim 路径未证明覆盖。
5. **导航边证据引用弱**（W7）——字符串引用、不核对目标 revision，靠
   "driver 串行"这个巧合兜着。
6. **占位常量在岗**——冲突新鲜度窗硬编码 3 秒，注释自认等待遥测接线。
7. **已修的历史缺陷**——W1（迟到回退）/ W2（冲突推进）/ W3（重访签名）
   都是实现 bug，SEM-002 已修并有 100/100 专项测试。

### 4.4 一句话收束

> 设计是诚实的，但没设计"发现自己犯傻"的机制；实现是安全的，但执法
> 有绕近路、账本有假抽屉。

---

## 5. 下一步建议（按收益排）

1. **P1 步维账本入 journal**：`_stepsDispatched`/续授账本持久化——唯一
   "设计承诺被实现击穿"的点。
2. **P2 执法收敛到 World**：单一当前容器 / 冲突过滤下沉为 WorldModel
   保证；`Returned` 直读改走 `CurrentInteractionContainerId` 同一入口。
3. **P3 Uncertainty 结构化**：`conflicts.Count` → 分类型未知（身份/
   关系/覆盖层），喂给 Heartbeat/CasePacket，Agent 介入判断直接受益。
4. **P4 Contains/Overlays 等真实 buyer**（需要父子层级/覆盖检测的
   change 立项时一起做，不预造）。
5. **P5 TRAJECTORY-001 H7 补真机一轮**（当前 blocked 项：3081 专线 +
   设备流）。
6. **P6 会话环境**：src/tests 子树受限写入仍被拒（目录 DACL 已修复并
   验证仍拒，疑似完整性标签层；恢复命令在 `.dsh/acl-reports/`）；本
   会话要做 src 内实现需切完全权限。

以上均为候选建议，实施须另立 change 并按 UniFlow 验收。

---

## 6. 证据与限制（诚实申报）

- 本评估**未自跑测试套件**：受限沙箱拒写 src/tests 子树（MSB3491，
  obj 目录新建/写既有文件均被拒；src 链 DACL 修复并验证后仍拒）。
  "当前全绿"依据仓库自带最新验证记录：ENG-OPT-001 全量 2024/2024 +
  WORLD-OBSERVATION-001 的 4 项既有失败收口，均落在评估基线 HEAD
  （d7aa7fc3）。
- 关键断言均在源码逐条复核（锚点即文中 文件:行号）；文档类结论（W1-W8
  等）溯源各 change 的 report/evidence，未重放其真机步骤。
- 4 路素材提取中 3 路由 glm-5.3-flash worker 完成（World 审计 / 设计
  文档 / 测试证据），1 路由强模型完成（Kernel 源码控制流推导）；
  Leader（本会话）复核了全部进入结论的关键断言。

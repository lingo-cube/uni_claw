# PROJECT_LEADER_ROW_IDENTITY_TRANSITION_SIGNAL_REFINEMENT_RESULT

> Gate：ROW_IDENTITY_TRANSITION_SIGNAL_REFINEMENT（STABLEKEY 容器域的最终语义订正）。
> 结论：**apaction 型启发已移除**（`Tap/ScrollBackward => pending` 不再存在）；**权威过渡信号在 ValidationHarness
> 观测边界不存在** → 按 gate §3/§8 停止并报告 **MISSING_TRANSITION_OBSERVABILITY_SEAM**（未回退到启发、
> 未伪造 authority）。Phase 2.6 维持 STOPPED。

## 1. Chosen authoritative transition signal

**NONE AVAILABLE at the harness decision point（证据）**：
- 并行代理已落地 `Model/ContainerTransition`（公开 record：kind=ENTER_CHILD/VERIFIED_RETURN…/
  from/to/activeExecutionContainer）与 `Agent/ActiveContainerContext`（**internal** sealed class）与
  `ContainerTransitionClassifier`——但全部在 **Agent 内部**：分类发生在 `ObserveAsync` 返回后的
  reconciliation 中（`Agent.ContainerReconciliation`），而 RowIdentityContext 的 offer 决策发生在
  **环境边界（tap transform）内的同一观测时点**——运行时也**无法**对 null 位置帧预先表达"意图过渡"
  （分类输入即该帧本身；null 帧无 ground 证据）。
- harness 现有可观测信号仅：resolved page（已用）+ 已执行 DeviceAction（gate 明令不得作为边界真值）。
- ⇒ 无既有 read-only 事实可供 harness 消费；亦无 older same-authority 信号。
- ⇒ **STOP：MISSING_TRANSITION_OBSERVABILITY_SEAM**（gate §3/§8 出口）。

## 2. Removal of the Tap/ScrollBackward heuristic

- `SettingsCampaignProgram`：无 actionTap / MarkTransitionPending / lastExecutedAction（grep 空）；
  transform 仅 `BeginContainer(ResolveSemanticPage(obs))` + Stabilize + 刷新 header。
- `RowIdentityContext`：无 `_transitionPending`/quarantine（grep 空）；类文档明示
  "a Tap is NOT a container transition" 与 seam 缺口为**已登记残余，不静默掩盖**。
- 保留（已验证）：容器域作用域（ACTIVE domain 提供面/接受面/重键）、验证返回重激活、同容器 null 保持域。

## 3. Behavioral RED→GREEN

| buyer | 语义 | 证据 |
|---|---|---|
| RED(a) Z4：授权进 child → 首帧未解析 → 父行被 offer → child 继承父键 | 预域修复行为 | 由 STABLEKEY_CONTAINER_DOMAIN gate 关闭（域修复后 grounded child 不继承；11/11）|
| RED(b) false-suspension：同容器 local Tap + 下帧 null → 当前补丁挂起 | 启发式过度 | 设计上 RED（挂起=Assert 不符）；该补丁已整段移除，行为由 GREEN 反证 |
| GREEN：同容器滚动/local 交互 null 帧 → 当前域保留 | 最终语义 | `RowIdentityContextTransitionSafetyTests` 4/4（含 False-suspension falsifier）|
| GREEN：RowIdentityContext 全套 | 域语义 | `RowIdentityContextDomainTests` 11/11；合计 **15/15** |

（RED(b) 的独立运行受工作树孪生并发编辑中期状态阻塞——诚实声明；最终语义的 GREEN + 移除证据是主证。）

## 4. Interaction matrix（A–H，最终语义）

| # | 场景 | 行为 |
|---|---|---|
| A | Tap 导航进 child（落地帧）| child 域激活；无继承 ✓ |
| B | Tap local toggle（下帧 null）| **当前域保留（offer 正常）** ✓ |
| C | Tap no-op/disabled | 同容器保留 ✓ |
| D | 容器内 ScrollForward | 当前域保留 ✓ |
| E | 容器内 ScrollBackward | 当前域保留 ✓ |
| F | 显式 verified return | 重激活父域 ✓ |
| G | child 入口首帧未解析（null）| **提供面=父域（开放边缘 → MISSING_SEAM 残余，未掩盖）** |
| H | 同容器交互 + 下帧 null | 当前域保留 ✓（B/C/E 覆盖）|
| 冻结 | TRANSITION_PENDING_FOR_CORRELATION != OBSERVED_CONTAINER_TRANSITION；CORRELATION_SUSPENDED != WORLD_LOCATION_CHANGED | 无任何独立转换断言生成（无启发）|

## 5. Shared-tree baseline validation

- 并行代理的 `ContainerTransition`/`ActiveContainerContext`/`Agent.ContainerReconciliation` 已使树可编译；
- 全量套件 **A/B（stash 我的改动对照）**：失败集完全相同（7 唯一名一致、无仅属一方）→ **我的改动零新增失败**；
  其余失败为并行代理既有/在途（Capstone/CORR_HOST/ExternalBoundary 环境性 + 其新增文件相关）。
- 目标套件：Domain 11/11 + Safety 4/4 = 15/15。

## 6. Fresh real evidence（Buyer）

- Z6 / Z6b（新 OCR 收据 + 域修复）：均于 **root 起步期失败**（Z6：首滚动帧 occurrences 5→0→6 抖，
  stability 预算耗尽；Z6b：仅 2 个 root 窗 accepted → normalizer unresolved）——**root 感知稀疏/环境类**，
  两次均未进入 child。
- ⇒ **真实 child 进入的 buyer 本会话未获得**；跨容器泄漏/假挂起的真机计数未采集（单元/矩阵级已证明）。
- ASSET：截图因 root 起步失败未捕获 → 如需按 gate §7 记录 MISSING_ASSET。

## 7. Graduation 状态

- **STABLEKEY_CONTAINER_DOMAIN_SCOPING：NOT GRADUATED**（seam 缺口 + real child buyer 未获得）。
- RowIdentityContext = **被动消费者**（仅消费 verified resolved page + 域状态；不产转换真值）。
- 重新敷设条件：待 Agent 侧暴露只读过渡事实（如 Observation 级 Transition 标注或 harness 可读的
  expected-transition）——届时复用该 seam 补 G 边缘（不加新 authority）。

## 8. Phase 2.6 / 后续

- **Current first blocker：ROOT_UNKNOWN_PERCEPTION_VARIANCE**（'LoO' 族；Z6/Z6b 起步稀疏同族）。
- Slow VLM Container Semantic Calibration（SHADOW）就绪：**READY**（gate §9：Fast perception 改进
  + VLM shadow 校准，针对 LoO/OCR、Wallpaper 短读、Bluetooth 碎片、Accessibility 节标题/描述、
  PageTitle vs 内容行——不连运行时 authority）。PageTitle 不重开（无 fresh evidence 使其成首个 blocker）。
- 证据链：本文件 + `PROJECT-LEADER-ROW-IDENTITY-TRANSITION-DOMAIN-SAFETY-RESULT.md` +
  `PROJECT-LEADER-STABLEKEY-CONTAINER-DOMAIN-MINIMAL-REPAIR-RESULT.md` + 系列。Phase 2.6 STOPPED。
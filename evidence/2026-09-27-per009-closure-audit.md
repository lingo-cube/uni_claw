# PER-009 Closure Audit（C1–C4 处置 + HEAD 复验）

> 日期：2026-09-27 · 对象：PER-009 lifecycle `verifying → closed` 前的 closure discrepancy 审计
> 方法：Leader 重读 HEAD 全部相关文档（PER-009/010/011/012、CORE-005、remediation/review evidence）
> + 一手读码（UiAutomatorDump / LivePerception / ConflictResolver / PostActionXmlRouter /
> UniKernel / KernelRunDriver / ProducerTrust）+ 机械 inventory（subagent，file:line 全量）
> + HEAD 全量回归复跑。本审计不改 Product code、不改 PER-009 frozen 语义（D1–D14、
> mechanism.md 零触碰）。

## Verdict

```text
PER-009_CLOSABLE
```

## C1–C4 处置

### C1 真机状态 —— truth = remediation evidence；plan 为 stale 文档

- 冲突面：`plans/PER-009-plan.md` 仍标 PENDING-ENV（本机无模拟器裁决，2026-09-22 开工时点）；
  `changes/PER-009/state.md` Verification 块仍指向 plan 的 PENDING-ENV 清单。
- 实况：remediation（同日更晚，commit `2bbad0a8`）记录 **HostLiveFull 真机 GREEN（11s）**，
  新传输通道（定点文件+cat+rm 单次原子）下 XML 实际在场——手动 dump 实证 switchWidget
  checked/checkable/bounds 与视觉检测 bounds IoU 高位对齐；state.md status log 同步在案。
- 裁决：**remediation evidence 为当前 truth**（时间序在 plan 之后、双向记录一致）。
  discrepancy 性质 = 文档漂移，非行为缺口。处置：plan PENDING-ENV 清单同步（本日完成，
  见下"文档同步"）；closure 以 remediation evidence 为 live 项判据。
- 残余（如实）：60s×≤3 探测**耗尽路径**与 structural/transient 分类的 live 实测未单独留档
  （探测状态机纯逻辑 5 例 fixture 全绿；live run 走的是成功路径）。登记为已知未覆盖面，
  不构成 closure blocker（失败语义确定性已测，live 复跑随下次有设备环境顺带补）。

### C2 Focused rescan —— B：Acceptance #4 环路已交付，裁剪机制显式合法 defer

- Acceptance #4 = "ReobserveFocused 端到端：冲突 → 定向裁剪重扫 → 销案或升级留档"。
- 已交付且被证明：
  - 环路端到端（world 真实 Conflict → CurrentConflictedSubjects → policy → 驱动面
    Focused 指令 → 有界 ≤3 → 销案或诚实耗尽）：
    `KernelRunDriverTests.FocusedLoop_DrivenByRealWorldConflict_NoManualInjection`
    （:585-662，全栈、零手工注入）+ remediation C-1 双点接线（StepAct :486 /
    TerminalEvaluation :759 先裁决后聚焦）。
  - "定向"：directive.Subjects 映射层过滤（`LivePerception.SubjectInScope` :209-211）。
  - "销案或升级留档"：`WorldModel.ResolveConflict` + `ConflictResolutionLog` /
    `ClaimEvolutionLog` 留档不删；耗尽 → `focused-reobserve-exhausted` 诚实失败。
- 未交付：**真裁剪重扫**（感知管线区域裁剪后同一快模型重扫）。remediation 登记 defer
  （"待 Tier 1 管线裁剪支持"），依赖真实存在：`/v1/analyze_raw` 只收整帧 RGBA
  （server.py :532，客户端 width/height 头），管线无区域分析语义；客户端自行裁剪
  属感知域决策（检测器对裁剪帧的行为/坐标逆映射），归 Tier 1 感知管线 change 所有。
  PER-011（FROZEN）escalation 阶梯保留 "focused rescan（同一 source/frame，有限次数）"
  为第一实现 slice，前向接续同一机制。
- 裁决：**B**——环路主体交付且证明；裁剪为机制细节，显式登记、依赖真实、owner 明确
  （Tier 1 感知管线 change / PER-011 escalation slice）。closure 记录 #4 按环路级交付，
  不宣称裁剪已实现。

### C3 B 级 + 不可逆 gate —— B：门槛语义交付，接线显式递延（owner = Grant/Phase 6）

- Acceptance #7 = "信任门槛生效：B 级孤证 + 不可运动作 → 未补佐证前拒绝授权"。
- 已交付且被证明：冻结表落盘（`producer-trust.json` EmbeddedResource + `LoadFrozenTable`
  fail-closed，P-1）；CSS 级联查找 + A/B/C 语义 + 门槛函数
  （`CanAuthorizeRoutine` / `RequiresCorroborationBeforeIrreversible`，
  `ProducerTrustTests` 6 例全绿）。
- 未交付：门槛函数**零生产调用方**（src/ 无 consumer，仅测试）——即"生效"未成立。
  这正是 review 报告的核心反模式（"验证了零件、没验证系统"），closure 如实按未接线记录。
- defer 合法性：门槛点火前提 = effect 不可逆性分类，该模型属 Grant/Phase 6 域、
  当前不存在；在 PER-009 内伪造分类或擅自发明"未分类即不可逆"保守策略都是越权
  （Grant 域决策）。remediation "无模型不伪造"（:16）为正确的边界行为，且已登记。
- 裁决：**B**——#7 按"等级机制交付、接线递延"记录；owner = Grant/Phase 6
  不可逆性分类 change。closure 不宣称 #7 全量生效。

### C4 S7 / live verification —— 当前 Acceptance 下满足（两条脚注如实登记）

- XML 事后路由：接线闭环在案——`KernelRunDriver._lastDispatchAt`（:143/:584，门④时序
  执法）→ `UniKernel.VerifyPostActionEffect`（:450）→ `TryRouteXmlVerification`（:480，
  establishing producer = XML 的 `{role}.state` 映射 claim）→ `PostActionXmlRouter.Route`
  四门（:29-66）。确定性证明：router 8/8 + remediation `XmlRoute_FourGates_VerifiesWithoutOccurrence`
  / `XmlRoute_TemporalGate_Fails_FallsBackToOccurrencePath`。
- target resolution：实现为 bounds 空间映射（IoU≥0.5 ∧ 领先余量≥0.25 ∧ checkable guard，
  `MapTargetStateClaim` :193-235）+ lineage 携带身份快照（`xml-map:`/`xml-checkable:`/
  `xml-unique:` → `SnapshotFromLineage` :192-207）。
- 真机 evidence：remediation 记录全真闭环 GREEN + XML 在场 + IoU 高位对齐；
  post 相映射在场 → XML 路由由构造成立（mapped claim 为 establishing producer 时
  必经四门路由；四门之②③由映射构造保证、④由 post 相时序保证）。
- 脚注 1：run trace 中的路由标记（`xml-route-four-gates` check）未在 evidence 文中
  引用原文——live 路由选择是结构性推论（映射在场 ⇒ XML 路由）而非引用的 trace 行。
  如实登记；不构成 blocker（确定性测试已锁路由行为）。
- 脚注 2：plan S7-wiring 注记的 "TargetSpec.Role → resource-id 尾段" Kernel 侧约定
  已被 lineage 快照桥取代（C-2 落地形态）；plan 该行为 stale 设计注记，本次同步删除。

## 审计新增发现（C1–C4 之外，closure 一并处置）

| # | 发现 | 事实 | 处置 |
|---|---|---|---|
| A-1 | P-4（评审）D8 预算推导未实现 | dump 超时 = 固定 3000ms（`TryDumpToDevice` 默认参）；D14 冲突新鲜窗 = 固定 3s（`KernelRunDriver` :138）；无"视觉耗时+500ms"生产侧推导 | 偏差方向 fail-safe（预算更紧 ⇒ 更早 degraded:no-xml，不增权威）。登记为已知偏差；owner = 后续 typed observation change 的 bounded acquisition seam（PER-010 明文要求调用方预算/超时/取消语义） |
| A-2 | P-5（评审）#11 调研落档形状未兑现 | PER-009 名下无"权威字段表每格有出处"的 evidence 文件（仅 grill docket 速览 + D12/status log 引官方参考页短语）；实质产物后置于 `plans/2026-09-26-per-010-compatibility-inventory.md`（逐格 developer.android.com 出处或 UNKNOWN，D12 字段表与覆盖表的严格超集，PER-010 FROZEN 资产） | closure 记录指针：#11 实质由 PER-010 inventory 兑现（每格有出处）；形状差异（plans/ vs evidence/、PER-010 vs PER-009 名义）如实登记，不再补造追溯件 |
| A-3 | 值域 {on,off,partial} 无运行时执法 | 值域仅存在于注释与 4 处 string 映射点（UiAutomatorDump :227 / ConflictResolver :91 / PostActionXmlRouter :58 / UniKernel :203 反向）；无枚举/校验器 | 登记；typed observation change 的 CheckedState 域类型 + PER-012 M-01–M-03 fixture 将执法 |
| A-4 | 映射层零确定性测试 | `MapTargetStateClaim` / `TryCoObserveXml` / `degraded:no-xml` / `ResetForNewRun` 在 tests/ 零引用；#9 值域断言仅冻结 subject 拼写（`SharedSubjects_FrozenTrio_ValueDomain`），XML 产 *.state 的映射层值域未被直接断言 | 登记；迁移 change Slice B/C fixture（含 M-04/M-05 egress 投影）将强制覆盖。IoU/唯一余量/checkable guard 逻辑现仅由 env-gated live 测试传递性覆盖 |
| A-5 | 解析层 missing→false 折叠 | `CheckedValue` :303-312 缺属性/未知值折叠为 "false"（→off）；`Bool` :314 同 | 已由 PER-012 Recorded semantic mismatch 第 1 行正式登记为迁移缺口；PER-009 语义按冻结不回改；typed change 按 M-02 以 Unknown(partial-unrepresentable) 修正 |

## Deferred items ownership（closure 定案）

| 递延项 | 事实状态 | owner |
|---|---|---|
| P-2 信任门槛接线（#7 后半） | 门槛函数零生产调用方；缺 effect 不可逆性分类 | Grant/Phase 6 不可逆性分类 change |
| P-6 真裁剪重扫（#4 机制细节） | Subjects 过滤已交付；裁剪未实现，管线无区域分析 API | Tier 1 感知管线区域裁剪 change / PER-011 escalation slice |
| A-1 D8 预算生产侧推导 | 固定 3000ms/3s 占位（fail-safe 方向） | typed observation change（bounded acquisition seam） |
| 冲突触发率统计（Tier 2 立项买家数据） | 待多场景运行积累 | Tier 2 立项前置数据采集 |
| A-3/A-4/A-5 | 值域无类型执法、映射层零测试、missing→false | typed observation migration change（Slice A 域类型 / Slice B+C fixture） |

## Verification（closure 时点 HEAD 复验）

```yaml
level: DETERMINISTIC（live 项沿用 remediation ENVIRONMENT 记录）
method: dotnet test 全量七套件（HEAD = 400d7b53，含此前 env 失败套件）
expected: 零 PER-009 回归；四元组与 state.md 声明一致
actual: |
  Agent 17/17 · Simulation 182/182 · Kernel 520/520 · Host 18/18 ·
  Agent.Dsh 121/121 · Core 14/14 · FileSystemRealization 9/9
  ——合计 881/881，0 失败，0 环境失败
  （state.md 原记录的 8 例 env 失败在本机当前环境不复现——closure 时点全绿）
evidence: 本文件 + changes/PER-009/state.md（closure 行）+
  evidence/2026-09-22-per009-remediation.md（live 项判据）
```

## 文档同步（本次执行）

- `plans/PER-009-plan.md`：PENDING-ENV 清单按 remediation 实况结算；S7-wiring stale
  注记（role→resource-id）删除；计划状态改"已闭合"。
- `changes/PER-009/state.md`：Verification 块 live 项判据指向 remediation + 本审计；
  status log 追加 closure 行；lifecycle → closed。Intent/Scope/Decisions/Acceptance
  零改动（frozen 语义保持）。
- `changes/INDEX.md`：再生（PER-009 移出 open 清单）。

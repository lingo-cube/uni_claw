---
name: uniclaw-evolution-loop
description: Replay-anchored fix loop for UniClaw. Use when a run / trace / scenario failure should become a verified candidate fix end-to-end, not just a diagnosis — lock an immutable case, reproduce RED, localize FDP/owner, produce one minimal candidate, prove RED→GREEN on the same case, and emit a decision-ready evidence packet. Composes uniflow (lifecycle), diagnosing-bugs (general loop), uniclaw-debug-evidence (E-level/FDP/owner), tdd (red-green), code-review. Never owns lifecycle, merge, baseline promotion, or a second task system.
source: LOCAL_UNICLAW
---

# UniClaw Evolution Loop（replay-anchored fix proof）

> 定位：**开发侧受控演进闭环**。Uniflow 拥有 WHEN / WHAT NEXT / 完成判定；
> `diagnosing-bugs` 拥有通用诊断循环；`uniclaw-debug-evidence` 拥有证据语义；
> `tdd` 拥有 red-green；`code-review` 拥有评审。本 Skill 只拥有一件事：
> **同一不可变 case 上的 RED→候选→GREEN 证明纪律 + 证据包**。
>
> 它不是产品 Runtime 的自改代码，不是自动合并器，不是第二套生命周期。

## 0. Composition Contract

```text
uniflow（spine：状态 / 失败边 / 完成）
  └ uniclaw-evolution-loop（本 skill：HOW to prove a fix）
      ├ diagnosing-bugs        reproduce → falsifiable hypothesis → minimal fix
      ├ uniclaw-debug-evidence E0-E4 / failure class / FDP / owner
      ├ tdd                    RED → GREEN → refactor
      └ code-review            Standards / Spec 双轴

返回：evidence packet + candidate diff + gate/decision request
```

**本 skill 不拥有**：lifecycle 状态、fix 是否被接受、review pass/fail、
merge、scenario/baseline 晋升、完成判定。它在 Gate 处交还 Uniflow。

## 1. 何时用 / 何时不用

用：

- 一个失败可从 run dir / trace / scenario / 红例稳定重放；
- 需要“同一 case 上改前红、改后绿”的可复现证明，而不只是诊断；
- 希望把结果交给人做一次方向/合并裁决。

不用：

- 没有不可变输入，只能代码分析 → 交给 `diagnosing-bugs`；
- 纯架构/协议设计，没有可执行 case → 交给 Uniflow RESOLVE / ADR；
- 想看“修复对下游的影响” → 这是 simulation 的假设修正注入（当前延后项），
  不在本 skill；
- 希望不经人批准自动合并/晋升 → 不允许（C1/C2、Human promotion）。

## 2. 七步循环

```text
0 INTAKE    失败信号 → 候选 case（run dir / trace / scenario / 红例）
1 LOCK      冻结输入 + 独立预期 + owner + gate
2 REPRODUCE 跑红；必须因预期原因失败；存 TRX
3 LOCALIZE  E-level / failure class / FDP / owner；此前不改代码
4 CANDIDATE 一个假设 → 最小 diff；禁止改 expected
5 PROVE     同一 case RED→GREEN + 回归；diff 只含候选与回归测试
6 PACKET    packet.json/md：case/inputs/red/fdp/candidate/green/risk/gate
7 FEEDBACK  更新 Change State；场景/基线只提候选；摩擦≥2 走 A8
```

### 0 INTAKE

两域统一使用 `uniclaw.evolution.signal.v0`（工具入口与字段见
`tools/evolution-signal.py --help`、`changes/EVO-002/signal-contract.md`）：

- `dev-verify`：验收出口复用本次 TRX、直接核对 composition pins，并记录
  在途变更。显式 expected-red 只留信号，不生成 intake 候选；不得把它当意外回归。
- `live-run`：显式 run 出口检查 facts 终态，无模型；深诊断由人调用
  run-diagnosis，诊断包以 path+sha256 引用。缺 runDir→case 导出器时标为
  pending-reproduction，不生成虚假复现命令或进入 LOCK。EVO-003首片
  `tools/evolution-case.py export/verify/replay`支持已落盘Settings XML弹窗分类：
  显式独立Oracle与capture/receipt关联齐备，导出新bundle并通过封存校验后，
  该组件case可进入LOCK。使用实际生成的重放命令/TRX；其余领域仍待复现。

INTAKE 按 failureKey 对照已有候选/显式已知问题引用；重复信号保留来源，
不重复候选。候选记录只是 evidence，不创建 Change、不授予修复或晋升权力。
无输入或诊断不完整时回到 Uniflow 取证。已有 case 的下列四类来源仍可使用：

| signal | 输入 | 最低证据 |
|---|---|---|
| 红例 | 已归档的失败测试/场景 | E1 + 失败输出 |
| run dir | live/sim 的一次运行产物 | E3 trace + facts/steps |
| trace | sealed / 持久化 trace | E3 + 封存校验 |
| scenario | 已存在的确定性场景 | E3 + digest |

没有不可变输入 → 不进循环；先补录制或先做 `diagnosing-bugs`。

### 1 LOCK

写一份 case manifest（见 `references/case-and-packet.md`）：

- 所有输入的内容 hash（XML/PNG/JSON/steps/trace）；
- **独立预期**：来自独立 oracle / 需求 / 已审 golden，**不得来自本次 actual**；
- 复现命令（project + filter / scenario id）；
- owner（预期出错的组件）；
- gate（是否需要 Owner 裁决 / 是否触及冻结语义）。

导出bundle使用`inputBase=case-directory`，输入路径相对`case.json`目录；
case.sha256封存manifest，载体/脚本/配置realization另钉hash。目标产品函数
仅记录baseline，避免将候选实现误当不可变输入。此适配只复现该组件，
不是整个原Run，也不赋予夹具标签窗口事实权威。

同一 case 的 red 与 green 必须使用同一 manifest；任何输入变化 → 新 case，
旧 case 只作历史。

### 2 REPRODUCE

- 只跑定向命令，不跑全量；
- 必须失败，且失败原因与 manifest 的 expected 一致；
- 存原始 TRX / stdout，不裁剪；
- 如果它是绿的：case 已过期或修复已存在 → 停，不制造红。

### 3 LOCALIZE

按 `uniclaw-debug-evidence`：

- E0-E4 证据等级；E2+ 必须先读证据再改代码；
- failure taxonomy A-F；
- First Divergence Point + owner；
- 没有 FDP/Owner → 不进入 CANDIDATE。

### 4 CANDIDATE

- 一次只验证一个可反驳假设；
- 最小 diff；不顺手重构、不扩大授权、不改 expected；
- 若候选改变已冻结语义、需要新协议/新字段、或依赖未决 Owner 裁决 →
  **停**，把问题写成 Decision Request，候选人保留为 patch，不冒充已修。

### 5 PROVE

- 用同一 manifest 的同一命令重跑 → GREEN；
- **按需选层，不盲目全量**：按候选影响面选择起始验证层、按需升级
  （六层阶梯与会话复用规则见
  [references/verification-ladder.md](./references/verification-ladder.md)）；
  L5（模拟器/真实模型）只在需要 live 证据时进入，同一循环连续多轮共享
  一次 boot+预热 lease，不重复启动模拟器；
- packet 的 `green.ladder` 如实记录到达层与受限原因；停在低层时
  "更高层未证明"写入 residualRisk；
- diff 审查：只含候选与回归测试；无无关文件；无其他会话改动；与既有红
  冲突先做在途变更对照再归因；
- 确定性：同一 case 连续两次结果一致；flaky 不得记为 green。

### 6 PACKET

`packet.json` 至少八段：`case` / `inputs+hashes` / `red` / `diagnosis` /
`candidate` / `green` / `residualRisk` / `gate`。缺一段即 RED。
`diagnosis` 以 path+sha256 引用诊断包，由该包提供 FDP/owner；不复制正文，
不改 LOCK 后的 case manifest。cycle-01 的历史内联格式保持原样。
`packet.md` 是给人看的同内容摘要，不复述全部 DTO。

### 7 FEEDBACK

- 把 packet 交还 Uniflow：它是 VERIFY 的证据，不是 CLOSED；
- 返回重放/证明结果前，按[开发测试资产收尾规范](../../../docs/agents/test-asset-retention.md)
  完成本轮处置并记录。LOCK输入、独立Oracle、规定的RED/GREEN与重复执行
  记录、packet及有效引用继续保留；无下游用途的解包/构建缓存、中间actual
  和重复源码副本当轮清理。失败、提前停止也要收尾，不能只留最后一次绿。
- scenario / baseline / golden 只提候选，不写；
- 同一摩擦 ≥2 次、或新术语定型、或决策被推翻 → 触发 Uniflow A8
  LEARNING HOOK，另开 change 改 Skill/流程；本循环内不改自己的 SKILL.md。

## 3. 硬规则（违反即 RED）

1. **禁止 expected 来自 actual**（按测试输出回填预期 = 造假）。
2. **禁止跳过 RED**（先红后绿；没有 red 就没有 fix proof）。
3. **RED 与 GREEN 必须同一 case / 同一输入 hash / 同一预期**。
4. **候选 diff 不得夹带无关改动**；不得回滚/覆盖其他会话的文件。
5. **禁止 commit / merge / push / baseline 晋升 / golden 更新**。
6. **禁止新增生命周期、任务系统、第二套 debug workflow**。
7. **命中冻结语义或 Owner 未决项 → 停**，packet 写 Decision Request。
8. **没有 FDP/Owner 不改代码**；没有 GREEN 不说“已修”。

## 4. 停止条件

| 条件 | 结果 |
|---|---|
| 无法稳定复现 | `BLOCKED`（记录环境/资产缺口与恢复条件） |
| case 已绿 | 停；不制造红，不更新 expected |
| 候选需 Owner 裁决 / 冻结语义 | `GATE`；packet 带 Decision Request |
| 多个假设混在一起 | 拆 case / 拆候选，回到 LOCK |
| flaky / 非确定性 | 记录，不宣称 GREEN；按 `uniclaw-debug-evidence` 处理 |
| 候选超出最小修复 | 回 CANDIDATE 收敛，或拆 Change |

## 5. 交还 Uniflow

```text
packet 完成
  → Uniflow REVIEW（code-review 双轴，输入 = candidate.diff + packet）
  → Uniflow VERIFY（四元组：method/expected/actual/evidence）
  → Human Gate（方向 / 合并 / baseline）
  → CLOSED 后 A8（如触发）
```

本 Skill 不宣布任何完成；`packet.gate` 未清时，Uniflow 状态最多到
`implemented`/`reviewed`，不得进 `closed`。

## 6. 首个 tracer

见 `references/tracer-default-apps.md`：SIM-016 的 `default-apps` 跨包误判。
它验证循环本身；分类语义是否该修仍由 SIM-017 的 Owner 裁决。

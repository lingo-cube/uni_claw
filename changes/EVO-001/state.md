# EVO-001 — Skill 级受控演进闭环（诊断→重放→候选修复→验证→学习）
lifecycle_state: implemented · disposition: none · depth: decision-heavy · base: 56a75fd74b5dff72db931edc96e343f0aea6d80b

## Intent（WHAT/WHY）

把已经存在但彼此断开的四段能力接成一条可重复执行的开发侧闭环：

```text
失败信号 → 锁定重放 → 复现 RED → FDP/Owner → 最小候选修复 → 同一 case RED→GREEN → 证据包 → Gate/学习
```

2026-10-23 实测结论：重放与仿真这一片已有真实证据（SIM-016 的
`default-apps` 红例、原录制提取、故障注入、2109/2109），但“诊断结论”
没有接进重放，“仿真结果”没有接进修复，修复也没有一条可判定的证明链。
本 Change 把这四段做成 **Harness 层的 Skill 过程 + 一个真实 tracer**，
让 AI Coder 能自己驱动一轮演进，而不是每次重新发明调试流程。

“self-evo”在本 Change 中只指 **开发 Harness 的自我驱动**：Agent 按 Skill
自动跑完一轮，产出候选修复与证据包；**不**指产品 Runtime 自改代码，也
**不**指自动合并/发布/基线晋升（见 Decisions D2/D6）。

## Scope（IN）

- `.agents/skills/uniclaw-evolution-loop/`：新本地 Skill（LOCAL_UNICLAW）。
  - `SKILL.md`：组合契约、7 步循环、硬规则、停止条件、交还 Uniflow。
  - `references/case-and-packet.md`：Case manifest 与 Evidence Packet 的
    最小字段、反作弊规则、示例。
  - `references/tracer-default-apps.md`：首个真实 tracer 的输入、红例、
    FDP 预期、候选方向与 Gate 说明。
- `changes/EVO-001/cases/default-apps.json`：首个 tracer 的不可变 case
  manifest（输入 hash、红例 filter、独立预期、owner、gate）。
- 一次真实 tracer 执行：从 `evidence/sim-016/classification-red.cs.txt`
  恢复回归测试 → RED → FDP → 最小候选 → 同一测试 GREEN → 证据包。
- `changes/EVO-001/evidence/<cycle>/`：TRX、diff、packet.json、packet.md。

## Out of Scope（禁止 / 延后）

- **禁止**自动 commit / merge / push；候选只落工作区与 evidence。
- **禁止**自动晋升 scenario / baseline / golden；只产出候选与建议。
- **禁止**把 SIM-017 的五项 Owner 裁决替 Agent 做掉；候选若依赖窗口
  证据，必须停在 Gate 并把 Decision Request 写进 packet。
- **禁止**新建第二套任务系统 / 生命周期 / debug workflow；Uniflow 仍是
  唯一 spine，`diagnosing-bugs` / `tdd` / `code-review` 仍是各自方法的
  唯一 owner。
- **禁止**在 EVO-001 内实现 simulation-baseline §4 的“假设修正注入”；
  本循环用既有重放做证明，不注入候选修复到仿真。
- **禁止**修改 `src/` 下除 tracer 最小候选外的任何产品代码；候选必须
  可整体 revert。
- 不启真实设备 / 模型；不重跑全量八工程作为每轮循环的默认成本。

## Decisions

- **D1 Skill 承载，不建平台。** 演进循环是 HOW，落本地 Skill；不新增
  产品组件、不新增 Capability Registry 条目、不建进化数据库/服务。
- **D2 候选只到 Gate。** Agent 可改工作区、跑红绿、产 diff；合并、
  发布、baseline 晋升必须 Human 批准。与 ADR-0007/0029 的 C1/C2 与
  Human promotion 边界一致。
- **D3 首个 tracer = `default-apps` 跨包误判。** 选它因为：SIM-016 已有
  归档红例与真实录制、SIM-017 已冻结问题边界、失败可单测复现、不需要
  设备/模型。它的价值是验证循环，不是宣称分类 bug 已修。
- **D4 证明锚定同一不可变 case。** RED 与 GREEN 必须使用同一 case
  manifest、同一输入 hash、同一独立预期；禁止按 actual 回填 expected。
- **D5 命中冻结点就停。** 候选若改变已冻结语义或依赖未决 Owner 裁决，
  循环在 Gate 结束；packet 必须列出具体裁决项，而不是把候选说成已修。
- **D6 Skill 自身的演进走 Uniflow A8。** 同一摩擦 ≥2 次或术语定型时才
  提 Skill/流程 delta；EVO-001 不在循环中改自己的 SKILL.md。

## Acceptance

| ID | method | expected | actual | evidence |
|---|---|---|---|---|
| A1 | `git ls-files`/目录检查 + Skill discovery（AGENTS.md → `.agents/skills/`） | `uniclaw-evolution-loop` 三件套存在，frontmatter 合法，组合契约与边界可读 | PASS：SKILL.md + 2 references 就位；DSH 会话已发现该 skill | `.agents/skills/uniclaw-evolution-loop/` |
| A2 | 只读核对 case manifest 与源资产 hash | `default-apps.json` 的输入、hash、红例 filter、独立预期、owner、gate 与 SIM-016/017 一致 | PASS：4 输入 hash 实测匹配；预期锚 SIM-017 正反例表 | `cases/default-apps.json` |
| A3 | 恢复归档红例后运行 Host.Tests 定向 filter | RED：失败消息为 `present`，与 SIM-016 归档一致 | PASS：exit 1，消息逐字一致 `Recorded normal Default apps page was asserted as popup: present` | `evidence/cycle-01/red.trx` |
| A4 | 按 `uniclaw-debug-evidence` 做 FDP/Owner | FDP 定位到 `Classify` 的 package 分支；owner=`UniClaw.Host`；failure class 可判定 | PASS：E2 / class B / package 分支早于结构证据返回；owner 已定 | `evidence/cycle-01/packet.json` fdp 段 |
| A5 | 最小候选 + 同一 filter 重跑 | GREEN：红测通过；diff 只含候选与回归测试；两次运行一致 | PASS：18/18 × 2（红测+known-gap+分类器 9 例）；diff 仅 2 文件 | `evidence/cycle-01/green.trx`；`candidate.diff` |
| A6 | packet 自检 | packet 含八段；缺一 RED | PASS：case/inputs/red/fdp/candidate/green/residualRisk/gate 八段齐 | `evidence/cycle-01/packet.json` + `packet.md` |
| A7 | 边界检查 | `git status` 两文件回 HEAD；无 commit/push；无 scenario/baseline 改写；未新增生命周期 | PASS：主树零残留（候选以 diff+副本存 evidence）；无晋升 | `git status`（2026-10-23） |
| A8 | 第二 case 干跑（证明循环可复用） | 另一 case manifest 跑完 LOCK→REPRODUCE；步骤不硬编码 `default-apps` | NOT RUN（S5 未执行；本轮以真实 tracer 优先） | — |

## 循环外新发现（2026-10-23 归因修正后）

1. **在途变更部分随迁（非资产链损坏）**：初判"56a75fd7 后 SIM-014 pins
   未随迁、主树既有红"经复核**有误**。事实：HEAD 绿（HEAD blob 879ff136
   与 PopupReplay pin 一致）；红仅在含在途 WORLD-VISUALIZATION-001（未提交）
   的工作区 —— 该会话重签了 ComponentReplay manifest（30/30 绿验证有效），
   **漏签** Host.Tests PopupReplay manifest。承接方 = 该会话提交前补签
   （其自身 VERIFY 面），EVO-001 不接管。
   证据：`evidence/cycle-01/control-no-candidate.trx` + 本轮 git/blob hash
   核对（879ff136/054b7c28）+ Simulation Component 30/30。
2. **cycle-01 对照实验盲区（摩擦 #1）**：对照只排除"本候选致红"，未排查
   工作区在途变更清单 —— evolution-loop 的 PROVE 步应补"在途变更核对"；
   一次不改 skill，第二次同类摩擦触发 A8。
3. **DSH Windows 沙箱运行约束**：命名管道不可用 → MSBuild 多节点与
   VSTest testhost 死锁；须 `-m:1 -nr:false`；pwsh 合并原生命令输出用
   `*> file`。已记录在 packet；第二次同类摩擦触发 A8 落进 skill。

## Constraints

- 文件面：`.agents/skills/uniclaw-evolution-loop/**`、`changes/EVO-001/**`、
  tracer 的回归测试 + 最小候选（`tests/UniClaw.Host.Tests/` 与
  `src/UniClaw.Host/SettingsPopupClassifier.cs` 或经 D5 停在 Gate 的
  patch 文件）。
- 共享工作区存在其他会话的 staged/untracked 变更（62 项）；不回滚、
  不接管、不混入本 Change 的 evidence。
- 不启设备/模型；不提交/推送；不向其他会话发消息。

## Assumptions / Alternatives

- **Assumption**：SIM-016 归档红例足以在 Host.Tests 复现（已在 evidence
  有失败输出）；若环境差异导致无法复现，A3 转 BLOCKED 并记录恢复条件。
- **Alternative（被拒）**：产品 Runtime 自改代码 / 自动学习在线策略——
  越过 Human promotion 与 Evidence 边界，且当前无真实 buyer。
- **Alternative（被拒）**：自动合并/自动晋升 baseline——违反 C1/C2 与
  ADR-0029，样本也不足。
- **Alternative（被拒）**：建独立“Evolution”服务或数据库——第二套
  任务/资产系统，与 C3 及 AGENTS.md §4 冲突。
- **Alternative（被拒）**：先做 simulation §4 的假设修正注入——成本高、
  且首个 buyer 是开发闭环，用既有重放已足够。

## Owner / Authority impact

- 不新增产品公共类型、协议、Capability、World/Evidence/Effect authority。
- 诊断仍只产证据（`uniclaw-debug-evidence` 边界不变）；修复仍归 Uniflow
  IMPLEMENT + `tdd`；评审仍归 `code-review`；完成判定仍归 Uniflow。
- 唯一新增 authority 是 **case lock + packet 的证明纪律**，归本 Skill；
  它不裁决“现实为真”，只裁决“同一 case 上 RED→GREEN 是否成立”。

## ADR refs

- ADR-0007（流程主干；本 Change 不新增状态）
- ADR-0029（C1/C2、Human closure；本 Change 不自动关闭）
- ADR-0013（Trace 非权威；重放只作证明输入）
- ADR-0026（不为想象需求预造；先 tracer 后扩展）
- `docs/architecture/simulation-baseline-v0.1.md`（Phase A 回放、C1/C8）
- `docs/design/uniclaw-architecture-overview-presentation-v0.1.md` §7.4
  （测试资产循环；本 Change 是它的开发侧驱动）

## Residual risks

- `default-apps` 候选若采用“覆盖窗结构签名”，仍是 XML-only 启发式，
  不能替代 SIM-017 的窗口证据；Gate 必须显式保留该风险。
- 归档红例来自隔离发布副本（`/private/tmp/uniclaw-sim016-...`），当前
  树恢复后可能因路径/资产差异需要最小适配；适配必须记录，不得改预期。
- 单测全绿不等于 live 行为修复；ENVIRONMENT 证据仍缺。
- 第二 case 若找不到真实失败，A8 只证明“可复用”，不证明“可泛化”。

## Verification

```yaml
verification:
  level: SCENARIO            # 循环端到端：red→fdp→candidate→green→packet
  method: >-
    归档红例逐字恢复 → Host.Tests 定向 filter RED（-m:1 -nr:false）→ FDP 核对 →
    最小候选 → 同一 filter + 组件回归 GREEN（两次）→ 无候选对照运行 →
    packet 八段自检 → 主树回滚边界检查
  expected: A1–A7 满足；A8 NOT RUN；RED 消息与 SIM-016 归档逐字一致；GREEN 18/18 可重复
  actual: >-
    A1–A7 全 PASS；RED exit 1 消息逐字一致；GREEN 18/18 × 2；对照运行证明
    扩大回归失败（SIM-014 hash-mismatch + exec.journal 并发锁）与候选无关；
    候选未应用主树，以 candidate.diff + candidate-files 存 evidence
  evidence: changes/EVO-001/evidence/cycle-01/（red/green/control TRX、candidate.diff、packet.json/md）
```

## Plan（垂直切片）

1. **S1 Harness 件**：SKILL.md + 两个 reference + case manifest。只写
   Harness，不动产品。
2. **S2 复现与定位**：恢复归档红例 → RED → FDP/Owner。命中 D5 就先
   停，不写候选。
3. **S3 候选与证明**：最小候选 → 同一 case GREEN → diff + TRX。
4. **S4 打包与交还**：packet.json/md → Uniflow 状态与 Gate → 学习/场景
   候选建议（不晋升）。
5. **S5 复用**：第二个 case 干跑，去硬编码。
6. **REVIEW → VERIFY → CLOSED**：双轴 review 与四元组验证分离。

## Status log

- 2026-10-23 UNDERSTAND：核对 SIM-016/017、simulation baseline §4、
  run-diagnosis/ScenarioImporter 现状；确认四段中间通、两头断。
- 2026-10-23 RESOLVE：用户裁决 self=开发 Harness、Skill 级、候选只到
  Gate、tracer=弹窗误判（2026-10-23 对话）。
- 2026-10-23 PERSIST→PLAN：落 state + Skill 骨架 + case manifest；等待
  执行 tracer 的批准（S2 会新增测试文件并可能改一行产品代码）。
- 2026-10-23 PLAN→IMPLEMENT：用户"继续"授权执行；期间沙箱升级为
  danger-full-access（此前命名管道限制导致 MSBuild/testhost 死锁，全程
  `-m:1 -nr:false`）。S2/S3/S4 完成：RED（与归档逐字一致）→ FDP → 最小
  候选（白名单优先 + 跨包无声明 + known-gap 期望搭乘迁移）→ GREEN
  18/18 × 2 → 对照运行 → packet 落盘 → 主树回滚零残留。Gate 未清
  （SIM-017 五项 + 过渡语义 + 期望迁移 + SIM-014 pins 漂移承接），按 D5
  不进 REVIEW 通过、不得 CLOSED。A8（第二 case）未跑。
- 2026-10-23 归因修正（IMPLEMENT 内）：核查"SIM-014 漂移"发现初判有误 ——
  HEAD 绿（879ff136 已随迁），红仅在含在途 WORLD-VISUALIZATION-001 的
  工作区（其漏签 PopupReplay manifest；Simulation 侧已重签 30/30 绿）。
  已修正 packet.json/md 与本文件三处归因；gate 第 4 项改指该会话提交前
  补签。新增摩擦 #1：对照实验未排查在途变更。

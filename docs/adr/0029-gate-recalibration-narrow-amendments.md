---
status: accepted
date: 2026-09-26
amends: ADR-0007 (Development Flow baseline — gate semantics only)
---

# 门控再校准窄修：C1 确定性预检、受限 closure C2 fastpath、Owner 门机械执法

依据 GATE-001 两周 shadow-mode 实验（2026-09-20 启动，于第 30 个决策
事件到达预设出口）的校准报告，对 ADR-0007 冻结的流程主干做**三项
窄幅修订**。本 ADR 只修订门控语义（何时必须人裁决、closure 如何
路由）；**不重开** ADR-0007 冻结的八状态主干、失败转移边、BLOCKED
disposition、PERSIST 深度与验证四元组——本修订走的是 ADR-0007 自身
预留的演化通道（"future evolution only through the LEARN hook's
evidence"），GATE-001 即该证据。

## 校准证据（决策依据）

- 主队列 #1–#30（出口 @ #30）+ 出口后敏感性 #31–#47，共 47 决策事件。
- **closure 张力 13 件（两队列）：人工推翻 0/13、结局改变 0/13**——
  证据完备的 closure 批量批准在事实面是 C2；批量节奏把 closure 等待从
  基线 5~6 天压到 ≈0~1 天。
- **评审类 override 10/10、方向类 2/2 产生真实修正**——人工注意力在
  方向/语义点信息量高，C1 触发器不放松。
- **override 结构性集中于「呈递前确定性核对缺席/过期/未覆盖语义
  维度」处**；预检完备处（closure、新鲜证据 step gate）零修正。
- C2 override 0/33（两队列混合口径；协议定义的 ACK 日结分母从未运转，
  校准报告已声明为代理口径）。
- **五起 P-D′ 一期违规**（SIM-003、SIM-004、RUN-005、PER-009、
  PER-013：agent 侧 closure 绕过零自动关闭要求，其中 PER-009 含实质
  裁决代行、PER-013 含唯一一例 closure 后事实更正）——保持
  `PROTOCOL_NON_COMPLIANCE`，**不回写为 PASS、不补记为「其实有
  Owner」**；它们是本 ADR 第三条（机械执法）必要性的直接证据。
- 证据：`evidence/2026-09-26-gate-001-calibration-report.md` ·
  `evidence/2026-09-20-gate-recalibration-shadow-ledger.md`（#1–#47）·
  `changes/GATE-001/state.md`（Owner 裁决 2026-09-26：calibration
  ACCEPT、validity VALID_WITH_PROTOCOL_DEVIATIONS、recommendation
  NARROW_AMENDMENT_SUPPORTED）。

## 决策（三项窄修，冻结）

### 1. C1 呈递前必须做 deterministic preflight

凡 C1 评审类呈递（spec / docket / 处置表 / 验收主张 / OWNER_GATE
汇报），呈递**之前**必须完成并通过确定性预检：

```text
claim-vs-code        —— 呈递物中的每条事实主张对当前源码/工件逐条核对
reachability         —— 处置项在既有代码下的可达性（「承诺不可达」类缺陷拦截）
freshness            —— 所引证据对当前 HEAD 的新鲜度（过期证据须复跑或标注）
RED-first / relevant regression
                     —— 验收主张可执行化（先失败再转 GREEN）或附相关回归
已知不变量场景检查   —— 已冻结语义不变量配场景化负例（防「套件绿但
                        不变量未覆盖」）
```

**目的不是降低 C1，而是减少伪 C1**：凡是机器能发现的缺陷不得留待
Owner 评审才发现（校准证据：评审类 override 10/10 全部属预检可拦截或
可收窄类）。预检不通过 = 不得呈递，返回修复；预检通过不改变场合的
C1/C2 分类。

### 2. Closure fastpath 只允许非常窄的 C2 候选，且先 shadow + ACK/veto

closure 批量批准要按 C2 记录（免逐件人工裁决），必须**同时**满足
七条：

```text
acceptance evidence complete        验收四元组齐且证据绿
no unresolved substantive finding   无未解决的实质 finding
no unowned defer                    所有 defer 项有明确承接 owner
not DECISION-HEAVY                  非 DECISION-HEAVY（P-D′：永久保留人工 closure）
not superseded/direction-changing   非被取代/非携带方向语义的 closure
no upstream authority change        无上游权威变更
no semantic contract change         无语义契约变更
```

任一不满足 → 照旧人工 closure（C1）。**即便七条全满足，现阶段也只
采用 `shadow recommendation + ACK/veto`**：agent 产出建议 + 人对日结
显式 ACK（无 ACK 窗口不关，veto = 推翻并记误分类、同类后续升级 C1）。
**不开放无人工自动关闭**——自动 CLOSED 仍以 reopen 协议 + 执法脚本
就绪为前提（P-D′ 二期前提不变），且 13/13 样本仍小、原始 C2 ACK 指标
不可恢复（校准报告 §2/§8），数据不足以支撑放开。

### 3. Owner 门必须机械执法

lifecycle 进入需要 Owner 的阶段（含一切人工 closure、OWNER_GATE 终裁、
需人授权的场合）时，落档必须携带：

```text
owner-ruling reference（commit / state 行 / 台账行的可核验引用）
OR
explicitly authorized fastpath manifest（显式授权的 fastpath 登记）
```

二者皆缺 = tooling 直接 RED（脚本拦截，不靠 agent 自觉）。
`leader review`、`Sol review`、goal 自动续跑下的 leader 自任裁决，
**一律不得伪装成 Owner decision**；任何「Owner ratification
（leader）」式措辞非法（PER-009 判例）。执法脚本本身由独立
enforcement change 落地（见 Consequences），本 ADR 只冻结规则。

## Consequences

- **不变项**：C1 五触发器零放松（方向/语义点人工价值有校准实证）；
  八状态主干 / 失败边 / BLOCKED / PERSIST 深度 / 验证四元组不变；
  DECISION-HEAVY 永久保留人工 closure 不变；无 ACK 的 C2 自动执行
  不开放。
- **执法落地**：第 3 条的校验脚本（closure/owner-gate 转移守卫）由
  后续 enforcement change 承接——GATE-001 Acceptance #2 的 FAIL
  disposition（protocol non-compliance）以其为 remediation owner。
- 五起 P-D′ 违规历史记录永久保持 `PROTOCOL_NON_COMPLIANCE`，不追溯
  改写。
- GATE-001 保持 reviewed/HOLD，ADR 持久化后由 Owner 裁决其能否作为
  "experiment completed with findings" 关闭。
- SKILL.md 的流程文字与本 ADR 的接线（预检步骤、fastpath 登记格式）
  随 enforcement change 一并修订，不在本 ADR 内完成。

## Disposition note（2026-09-27，追加）

本 ADR 的规则文本与上述全部结论保持不变，不追溯改写。追加本条仅记录
一条后续处置事实：

- 本 ADR 第 3 条要求的 mechanical enforcement（owner-ruling validator /
  fastpath manifest）由 **Owner 裁决为 not pursued**，承接 change GATE-002
  以 `closed / not-pursued` 收口。理由：mechanical owner-gate enforcement 的
  workflow 复杂度高于其当前期望价值；治理规则保留，不新增执法子系统。
- 因此第 3 条自本条起为**保留但未机械执法**：owner-gate 落档携带
  owner-ruling reference（或显式授权 fastpath manifest）继续依赖人工遵守，
  工具不再兜底。Owner 介入条件收敛为四条人工 stop rule（architecture /
  authority 变化；多有效方案需人裁决；DECISION-HEAVY change；review 出现
  substantive finding）。
- 「执法落地」一项标记为 deferred-not-pursued，而非已交付。
- GATE-001 的五起 `PROTOCOL_NON_COMPLIANCE` 永久保持，不因本条改写。

# GATE-001 — 门控再校准 shadow-mode 实验（两周期）

lifecycle_state: closed · disposition: none · depth: standard · base: 4020de18

## Intent

按 `docs/design/uniflow-gate-recalibration-proposal-v0.2.md` 启动两周
shadow-mode 实验：人工门零变化，agent 并行输出决策分类（C1/C2/C3），
校准「人工注意力在汇聚点信息量趋零」假设；出口凭校准报告决定是否立
ADR-0007 窄修 change。

## Scope

- `evidence/2026-09-20-gate-recalibration-shadow-ledger.md`：唯一运行产物（并行记录台账）
- closure 批量化节奏：待人工关闭的 Change 攒入既有触点批量裁决（只改节奏，不改语义；一期零自动关闭）
- P-E′ 卫生：CORE-008 absorption 记录追加；open-gates 可生成索引（脚本一致性检查可后置）

## Out of Scope

- SKILL.md / ADR-0007 任何改动（实验期冻结）
- 自动 CLOSED（二期，以 reopen 协议 + 执法脚本就绪为前提）
- fast-path manifest / `tools/validate-fastpath.py`（二期）

## Decisions

- 2026-09-20 人裁决（v0.2 §0）：Q1 shadow 先行 / Q2 一期零自动关闭（严于审阅）/ Q3 C1 扩展+推导基准 / Q4 manifest+脚本
- 对抗审核七条处置：v0.2 附录 C

## Acceptance

1. 台账存在且事件行八列完整；实验期内不删行、不改历史行
2. 人工门执行方式与实验前零差异（docket / grill / closure 照常）
3. 出口条件到达（2026-10-04 或 ≥30 事件）时产出校准报告：C2 override 率、C1 漏检下界、分类分布、边界案例集、等待耗时对比
4. CORE-008 账实一致（absorption 记录在档）

## Verification

（2026-09-26 校准报告产出时填写；口径见 v0.2 §6）

```yaml
acceptance_1:
  method: 台账行形状检查 + 补录对账（b60cbc77..d3ac93da 全量扫描）
  expected: 事件行八列完整；实验期内不删行、不改历史行
  actual: 47 行（#1–#27 原始 + #28–#47 补录）九列齐；git diff 纯追加
    （ledger +142/-0，state +10/-0 与本次增量）；编号连续无重复
  evidence: evidence/2026-09-20-gate-recalibration-shadow-ledger.md（含对账节）
acceptance_2:
  method: docket/grill/closure/step-gate 执行方式对照实验前
  expected: 人工门执行方式与实验前零差异
  actual: **FAIL（Owner 裁决 2026-09-26：due to protocol
    non-compliance，不追溯 PASS）**——五起 agent 侧 closure
    （SIM-003/SIM-004/RUN-005/PER-009/PER-013）违反 P-D′ 一期零自动
    关闭，永久保持 PROTOCOL_NON_COMPLIANCE（不补记为「其实有 Owner」、
    不回写 PASS）；已按偏差单独分析（不入事件计数），校准报告 §4
    不隐藏。**remediation owner = ADR-0029 + GATE-002**（Owner 裁决
    2026-09-26：GATE-002 为最小 persisted enforcement change，承接
    owner-gate 机械执法 / owner-ruling ref 与 fastpath manifest 检查 /
    冒充拒绝 / ADR-0029 tooling wiring）
  evidence: evidence/2026-09-26-gate-001-calibration-report.md §4 ·
    docs/adr/0029-gate-recalibration-narrow-amendments.md ·
    changes/GATE-002/state.md ·
    Owner 裁决（2026-09-26，见 status log 当日条目）
closure_conclusion: >
  experiment result verified; Acceptance #2 = FAIL with remediation
  transferred（to ADR-0029 + GATE-002）。实验结果三件（台账 47 事件、
  校准报告、Owner 裁决链）verified；Acceptance #2 的 FAIL 保持真实、
  不回写 PASS——GATE-001 的职责是发现并校准问题，不负责实现
  remediation（Owner 裁决原文）。
acceptance_3:
  method: 出口到达后产出校准报告（Owner 2026-09-26 指令放行）
  expected: C2 override 率、C1 漏检下界、分类分布、边界案例集、等待耗时对比
  actual: 报告产出：C2 override = 0/16（代理分母，协议分母不可复原已
    声明）；C1 漏检下界 = 0/30 观察（censored 已注明）；分布 = C1实质
    23 + closure张力 7 + 纯C2/C3 = 0；边界案例集 = #3/#25/#34–36/#42
    假设检验 + closure 张力 13 件 + #47 判例；等待 = 分类分布 +
    closure 落档延迟 5~6 天 → ≈0~1 天；建议 = NARROW_AMENDMENT_SUPPORTED
  evidence: evidence/2026-09-26-gate-001-calibration-report.md
acceptance_4:
  method: CORE-008 absorption 记录核对
  expected: 账实一致（absorption 记录在档，不改写历史行）
  actual: 实验启动日落档（2026-09-20 status log）；PER-013 Gate 0 于
    2026-09-27 仓历再次核对一致
  evidence: changes/CORE-008/state.md · changes/PER-013/state.md Gate 0
```

## Status log

- 2026-09-20 · created · v0.2 落盘（v0.1 superseded，审核 + 四裁决）；
  台账建立；CORE-008 追加 absorption 记录（lifecycle 关闭随首批批量
  closure 由人裁决）；首批批量 closure 候选 CORE-008 / CORE-010 /
  CORE-012 已提交人批量裁决。实验开始。
- 2026-09-20 · batch-closure-1 · 台账事件 #1：人批准 CORE-008 / CORE-010 /
  CORE-012 关闭（三选三，零 override，等待 ≈0——对比 09-19 前单笔
  closure 排队 5~6 天）；三 Change lifecycle 已落 closed；边界案例入池
  （DECISION-HEAVY closure 的规则面 C1 / 事实面 C2 张力）。
- 2026-09-26 · ledger-reconciliation · 台账 #27 后并行记录中断，本轮按
  Owner 指令对 b60cbc77..d3ac93da 全量扫描对账：入账 20 件（#28–#47，
  全部 late-recorded/reconciled，原始事件日期保留），排除 14 类（理由
  逐条在台账对账节）；**N = 27 + 20 = 47 ≥ 30 →
  EXIT_TRIGGERED_BY_SAMPLE_COUNT**（阈值实际于 #30，2026-09-23 会话 /
  09-24 00:06 commit 越过）。规则零修改；#1–#27 历史行零改写；记录
  LEDI-1（仓历/系统时钟偏移）与 LEDI-2（跨午夜会话日期）两项数据事项；
  报告 P-D′ 一期零自动关闭被 SIM-003/SIM-004/RUN-005/PER-009/PER-013
  五处 agent 侧 closure 破坏（只报告不虚增不修正）。**已 STOP 等待
  Owner：校准报告未启动（按指令不自动进入下一阶段）。**
- 2026-09-26 · calibration-report（implementing → reviewed）· Owner 裁决
  PASS（reconciliation 认可、corrected 47、exit @ #30 SAMPLE_COUNT），
  按指令产出校准报告 `evidence/2026-09-26-gate-001-calibration-report.md`：
  主队列 #1–#30（C1 实质 23 / closure 张力 7 / 纯 C2 与 C3 = 0）；C2
  override 0/16（代理分母显式声明）；C1 修正 8/23 = 34.8%（评审类 6/6、
  方向类 2/2、其余 0——结构性零散落）；漏检下界 0/30 观察（censored）；
  closure 延迟 5~6 天 → ≈0~1 天；出口后 #31–#47 方向一致；五起 P-D′
  偏差 = process non-compliance（执法不完整，不推翻度量；PER-009 实质
  裁决代行 + PER-013 唯一事后更正为最重两例）；预检缺口假设判别力强
  （override 集中于预检缺席/过期/未覆盖语义维度处）；建议
  NARROW_AMENDMENT_SUPPORTED（三条最小变更 + 显式禁区）。效度裁定
  VALID_WITH_PROTOCOL_DEVIATIONS。**不自判 verified**：Acceptance #2
  被五起偏差打破，处置权在 Owner。STOP at OWNER_GATE：ADR-0007 未创建、
  门控规则未动、产品代码未动；建议 GATE-001 verdict = HOLD（待 Owner
  对建议与 Acceptance #2 偏差裁处）。
- 2026-09-26 · owner-ruling（reviewed 维持 · HOLD）· Owner 裁决四项：
  experiment validity = VALID_WITH_PROTOCOL_DEVIATIONS；calibration
  conclusion = ACCEPT；recommendation = NARROW_AMENDMENT_SUPPORTED
  （批准窄修方向，先定义规则与机械执法，不开放无人工自动关闭）；
  GATE-001 = HOLD at reviewed（不标 verified）。**Acceptance #2
  disposition 定为 FAIL due to protocol non-compliance（非追溯
  PASS）**，五起违规永久保持 PROTOCOL_NON_COMPLIANCE（Verification
  节已更新）；remediation owner 指向 ADR-0029 / enforcement change。
- 2026-09-26 · adr-persisted · 「ADR-0007 窄修」落档为
  **docs/adr/0029-gate-recalibration-narrow-amendments.md**（编号
  说明：ADR-0007 已存在且为流程主干基线，故窄修取下一号 0029 并
  amend ADR-0007——0007 front matter 已加 amended-by 行）。三项
  窄修按 Owner 原文冻结：①C1 呈递前 deterministic preflight
  （claim-vs-code / reachability / freshness / RED-first / 不变量
  场景；目的 = 消灭伪 C1，不降 C1）；②closure fastpath 七条件全满足
  方可记 C2，且现阶段仅 shadow + ACK/veto，不自动关闭；③Owner 门
  机械执法（owner-ruling ref OR 授权 fastpath manifest，缺一 tooling
  RED；leader/Sol review 与自动续跑不得伪装 Owner decision）。执法
  脚本归后续 enforcement change；SKILL.md 接线同批。**下一步：
  回到 OWNER_GATE，由 Owner 裁决 GATE-001 能否作为 "experiment
  completed with findings" 关闭。**
- 2026-09-26 · **closed（Owner 终裁，选项 A）** · **owner-ruling
  reference：Owner 裁决原文「GATE-001 OWNER DECISION 选择 A：CLOSE as
  "experiment completed with findings"」（2026-09-26 会话记录，理由六
  条：预设样本出口完成 / reconciliation+calibration 完成 / validity
  VALID_WITH_PROTOCOL_DEVIATIONS / Acceptance #2 FAIL 保持真实 /
  ADR-0029 已冻结 remediation 方向 / GATE-001 职责为发现并校准问题）
  ——本条即裁决记录，满足 ADR-0029 第 3 条（执法脚本落地前按规则
  手动持有 owner-ruling ref）。**前置动作已执行：GATE-002 立项
  （最小 persisted enforcement change，scope 四项按 Owner 原文），
  Acceptance #2 remediation owner 更新为 ADR-0029 + GATE-002；
  closure conclusion 见 Verification 节（experiment result verified;
  Acceptance #2 = FAIL with remediation transferred）。五起
  PROTOCOL_NON_COMPLIANCE 原样保留；不等待 enforcement change 实现
  即关闭（Owner 明示）。INDEX 再生 + diff-check + commit/push 随本
  closure 执行。**completed with findings。**

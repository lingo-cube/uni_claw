# GATE-001 — 门控再校准 shadow-mode 实验（两周期）

lifecycle_state: implementing · disposition: none · depth: standard · base: 4020de18

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

（实验结束时填写；口径见 v0.2 §6）

## Status log

- 2026-09-20 · created · v0.2 落盘（v0.1 superseded，审核 + 四裁决）；
  台账建立；CORE-008 追加 absorption 记录（lifecycle 关闭随首批批量
  closure 由人裁决）；首批批量 closure 候选 CORE-008 / CORE-010 /
  CORE-012 已提交人批量裁决。实验开始。
- 2026-09-20 · batch-closure-1 · 台账事件 #1：人批准 CORE-008 / CORE-010 /
  CORE-012 关闭（三选三，零 override，等待 ≈0——对比 09-19 前单笔
  closure 排队 5~6 天）；三 Change lifecycle 已落 closed；边界案例入池
  （DECISION-HEAVY closure 的规则面 C1 / 事实面 C2 张力）。

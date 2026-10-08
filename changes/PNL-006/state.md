# PNL-006 — 全链路单 run 测试报告（correlator + 双格式投影）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）

把一次 run 的全链路事实（Runtime Host → UniAgent → Capability → WorldModel → 执行操作）
从 5+ 种 source-native 产物（metadata / facts / trace / settings-trace / exec.journal /
consultations / preflight / failure）关联成一份确定性报告：canonical `report.json`
（`uniclaw.workspace.run-report.v1`）+ 从同一 JSON 派生的人读 `report.md`。
消费者三种（失败诊断 / Workbench 展示 / 测试验收 evidence），各读不同粒度层，
事实永不互相矛盾（单一派生源）。纯事实投影，不含诊断结论。

## Scope

- `schemas/workspace/run-report.schema.json`：报告契约（envelope / coverage /
  anomalies / L1 summary / L2 timeline / L3 六层明细 / L4 引用索引）+
  `examples/valid/run-report.json`，进 `validate-workspace-schemas.py` 自动发现。
- `tools/gen-run-report.py`：只读 correlator；确定性输出（同输入同字节）；
  默认降级生成 + coverage 声明，`--strict` 供 CI/evidence（缺 metadata/facts/
  trace/journal 或 runId 不可解析 → exit 2）；`--check` golden 字节比对。
- exec.journal 解析（4 字节大端长度前缀 + UTF-8 JSON）；损坏 → artifact
  partial + anomaly，不崩溃。
- L2 时间线合并规则（documented derived，不伪造时间轴）：
  External cycle 先行（原生 cycle 序）；kernel checkpoint（derive-slice /
  resolve-current）按其前置 revision 落位于首个 RevisionNumber ≥ 该 revision 的
  attempt 之前；k 次 consultation 先于 k 次 attempt；PostActionEffectFlow
  cycle k 跟随 attempt k；terminal 最后。
- 首份真实报告：`evidence/pnl-006/run-20261003-075727-844/`（PNL-003 fixture）。

## Out of Scope

- 结果诊断 / 结果判断能力：未来独立功能，消费本报告作为输入（owner 2026-10-06
  确认）；本报告永不产出结论性内容。
- 测试集级多 run 聚合报告（单 run 格式冻结后再议）。
- RuntimeHost finalize 自动产出、verify-live 挂接（下一 change，P2）。P2 同时
  补两处输入缺口，消除首份报告的历史性降级（定性：旧 fixture 局限，非设计
  缺口，见 `evidence/pnl-006/README.md`）：① 需求原文（taskRef/requirement）
  落盘进 metadata——agent 前的「需求」层当前只能 `--requirement` 传入或标
  未采集；② `.runtime-runs` 事件并入报告输入——host 分区当前仅由 run 目录
  产物重建。P2 后新 run 报告若再出现这两处降级即视为回归。
- Workbench 页面（P3，直接消费 report.json）。
- trace.json 内部 IntegritySha256 的跨语言重算（artifact 自带声明即可）。

## Decisions

1. 报告 = 纯 read model：每 section 声明 availability/valueOrigin；跨轨排序
   只用各来源原生序（cycle / journalSeq / captureSequence），derived 规则显式
   标注 `orderingBasis: derived`；不引入 wall-clock 时间轴（Kernel trace 刻意
   无时间，尊重 TRC-001）。
2. schema 落 `schemas/workspace/`（而非 tools/ 内部）：Workbench / 工具 /
   evidence 三方消费，属跨面契约；envelope 复用 workspace 家族的
   availability(7 值) / valueOrigin(4 值) 语义。
3. 完整性：envelope 记录每个输入 artifact 的 bytes + 原始字节 SHA256；输出无
   任何时间戳/机器相关字段（runDir 用调用者原样路径），保证字节级可复现。
4. 不写 certification 块：认证写入是 `scenario_certify.py` 独占域；报告只是
   evidence 输入。
5. anomalies 只做观察（severity info/warning/failure），不做归因：trace
   recorderTerminal ≠ Finalized、span 非 Completed、receipt ≠
   DeliveryCompleted、facts.terminal=false、failure.json 存在、artifact 解析失败。
6. L4 引用索引 = 报告自身提及的 id 闭包（runId/captureId/attemptId/binding/
   intent/receipt/checkpoint span/首末 revision → artifact + locator）；
   不复制 trace.json 的逐 span ev-/rev- 全量索引（trace.json 本身即权威索引）。
7. journal attempt 分组以 AttemptId 为键（fixture 显示 attempt 序号可跳跃，
   如 attempt-1 → attempt-4；序号连续性不是契约）。

## Assumptions

- run 目录产物集合 = HostRunner 现行落盘清单（facts / environment-preflight /
  consultations / trace / settings-trace / exec.journal / failure）+ metadata。
- `facts.runId`（v2 起）或 `trace.json.runId` 即 Kernel canonical RunId
  （HostRunner 的 launch-run-mismatch 检查保证二者与 Runtime RunId 一致）。
- checkpoint→revision 落位规则的有效性由数据佐证：fixture 中 checkpoint 前置
  revision（584/1218/1905）与对应 attempt 的 RevisionNumber 完全一致。

## Alternatives rejected

- C# 产品侧实现：把 harness 功能渗入产品仓库边界（owner 已裁决 Python tools/）。
- 逐 span 全量 ev-/rev- 复制进报告：~3800 条引用徒增体积，不增可导航性。
- 报告内置诊断结论：会成为 evidence 链中的伪 authority（owner 补充确认：
  诊断做独立功能消费报告）。

## Owner / Authority impact

- 报告不拥有任何状态：Kernel/Host/Agent 权威不变；报告是只读投影。
- 新增共享契约面一个 schema 文件；无产品代码改动。

## Acceptance

1. 真实 fixture 生成报告且 schema 校验通过。
   - method: 生成 + Draft202012 校验
   - expected: 0 errors
   - actual: 0 errors（20177B json / 7437B md）
   - evidence: `evidence/pnl-006/run-20261003-075727-844/`；见下 verification
2. 字节确定性：同输入重复生成 + `--check` golden 比对通过。
   - method: `--check --expect-dir`
   - expected: CHECK-PASS, exit 0
   - actual: CHECK-PASS, exit 0
   - evidence: 本文件 verification 段命令 2
3. 降级语义：空目录可生成（coverage 声明 not-collected/partial）；
   `--strict` exit 2 且列出具体违规。
   - method: 合成空目录 / 损坏 journal
   - expected: 降级 exit 0；strict exit 2；损坏 journal → artifact partial + anomaly
   - actual: 全部符合
   - evidence: verification 段命令 3-5
4. 仓库 schema 校验器收录新 schema 且全绿。
   - method: `python3 tools/validate-workspace-schemas.py`
   - expected: PASSED 13 schema(s)
   - actual: PASSED 13 schema(s)
   - evidence: verification 段命令 6
5. 时间线合并正确性（数据佐证）：checkpoint revision 584/1218/1905 与
   attempt-1/attempt-4 RevisionNumber 及末次 reconcile 一致。
   - method: 人工核对 report.json timeline
   - actual: 一致（12 个 timeline 事件）
   - evidence: `evidence/pnl-006/run-20261003-075727-844/report.json` timeline

## verification（2026-10-06，CONTRACT/DETERMINISTIC 级）

```text
1. python3 tools/gen-run-report.py --run-dir evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844 --out-dir evidence/pnl-006/run-20261003-075727-844
   → WROTE report.json (20177 bytes) / report.md (7437 bytes); anomalies=0 timelineEvents=12 references=23
2. python3 tools/gen-run-report.py --run-dir … --check --expect-dir evidence/pnl-006/run-20261003-075727-844
   → CHECK-PASS, exit 0
3. 空目录（合成）降级生成 → exit 0；--strict → exit 2（5 条违规逐条列出）
4. 损坏 exec.journal（合成）→ journal availability=partial + anomaly artifact-parse-failed, exit 0
5. Draft202012Validator(run-report.schema.json).iter_errors(报告实例) → 0 errors
6. python3 tools/validate-workspace-schemas.py → PASSED 13 schema(s) and examples
```

## Status

- 2026-10-06 IMPLEMENT：schema + correlator + md 渲染 + golden/strict/check 三模式
  + 首份真实报告落 evidence/pnl-006/。待 REVIEW。
- 2026-10-06 REVISION（owner 漏斗模型反馈）：报告呈现从"观测者普查"改为
  "验收者漏斗"——①需求 → ②效果达成（verdict 视图：终态/投递/验证锚点/
  receipts）→ ③组件概览（一行一组件）→ 异常观察 → 细节区（`<details>`
  默认折叠，有异常或未 Completed 时自动展开）。schema：summary 新增
  `requirement`（text/availability/valueOrigin/source）与 `achievement`
  （原 summary 的 status/outcome/terminal/receipts 迁入，另加 anchors 三字段）；
  canonical JSON 全量语义不变，分层披露是渲染策略。需求原文 run 产物暂不携带：
  本期 `--requirement` 显式传入（valueOrigin: configured），落盘进 metadata
  属 P2 产品侧改动（新增 Out-of-Scope 延续项）。验证：golden CHECK-PASS /
  PASSED 13 / degraded 0·strict 2 / 异常 run 自动展开 + --requirement 流入①区
  均实测通过；report.json 20483B、report.md 8250B。

## Review / Verify / Closure（2026-10-06）

- REVIEW：实现保持只读 correlator + JSON 派生 Markdown 的边界；需求/达成漏斗只改
  展示层，canonical JSON 语义未被诊断结论污染；P2 的 metadata 需求采集与 Runtime
  finalize 接线仍明确留在 Out of Scope。
- VERIFY：真实 run 生成与 `--check --expect-dir` 均 CHECK-PASS；空目录降级/strict
  与损坏 journal 行为符合声明；Draft 202012 校验 0 errors；workspace schema
  校验 PASS 13 schemas。
- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · 所有五项 acceptance 均有
  四元组证据，未发现实现缺陷或未授权产品侧改动。

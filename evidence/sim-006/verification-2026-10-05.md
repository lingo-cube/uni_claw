# SIM-006 实现验证证据（2026-10-05）

> 四级四元组汇总。运行快照（resolved-config）在 `evidence/sim-006/runs/`（本地保留、
> 不提交，目录已 gitignore）；本文件为提交版证据，含关键实际输出值。

## 运行清单

| 运行 | 入口 | 结果 | 关键输出 |
|---|---|---|---|
| self-test | `python3 tools/verify-change SIM-006 --self-test` | rc=0 | T1–T18 全 PASS；FINAL_STATUS=PASS |
| A5 负例×3 | `--scope full --scenarios …` / `--scope quick --scenarios …,SCN-NOPE` / manifest 暂移 | rc=2×3 | 三条均在任何执行前拒绝（参数冲突/未知场景/必需配置缺失） |
| validator 正例 | `python3 tools/validate-testset-manifests.py` | rc=0 | PASSED 3 test-set manifest(s) |
| validator 负例×2 | 临时 manifest：未知场景 / 小写 id | rc=1×2 | `references unknown scenario SCN-DOES-NOT-EXIST` / `must be a scenario id (SCN-*)` |
| quick | `python3 tools/verify-change SIM-006 --scope quick` | rc=0 | 8/8 场景 Passed（sim006-quick.trx 恰 8 结果）；GROUP_FULL_SOLUTION/COVERAGE=SKIPPED(scope=quick)；FINAL_STATUS=PASS + SCOPE_NOTE |
| full（首轮） | `--scope full` | rc=1 | 唯一失败 = DocsMetadataTests 文档分类学 tripwire 抓到新矩阵文档缺 Status/Authority 头（真实执法，非环境噪音）；其余 6 组全 PASS |
| full（修复后×2） | `--scope full` | rc=0×2 | 7 组全 PASS；certification 29 files 0 violations；coverage 真值链对 sim006-full.trx 验证 29 场景全 certified；EXPECTATIONS_DIGEST=UNCHANGED |

## 验证声明（按等级）

```yaml
CONTRACT:
  method: >
    validate-testset-manifests.py（3 manifests + 2 临时负例）；
    tools/verify-change --self-test（T1–T18）；validate-workspace-schemas.py（12 PASS）；
    DocsMetadataTests（4 PASS，含新增 analysis 文档头）；git diff --check；
    快照脱敏核对（无 env 变量/凭证采集，by-construction 声明落快照）
  expected: 引用有效、schema/头部合规、非法输入 fail-closed、快照脱敏
  actual: >
    全部通过；未知 scenarioRef/小写 id/缺 manifest 分别 rc=1/1/2 拒绝；
    文档头违规被既有 tripwire 抓获并修复后转绿
  evidence: 本文件运行清单；runs/ 快照

DETERMINISTIC:
  method: >
    classify() 多失败并存单测（T10–T16）；validate_scenario_ids/scope_skips
    纯函数（T17/T18）；A5 三条端到端拒绝路径
  expected: 并存失败均保留且聚合不可 PASS；未知输入执行前拒绝；跳过组带原因
  actual: T10 断言 [BEHAVIOR_REGRESSION, CERTIFICATION_STALE_ONLY] 并存；
    T14 断言三类并存；T15 隔离环境失败；T16 聚合不可 PASS；A5 三路 rc=2
  evidence: /tmp 自测日志摘录见运行清单；self-test 源码 tools/verify-change

SCENARIO:
  method: >
    新鲜执行 quick（8 场景 trait 过滤）+ full×3（188 测试全量；sim 单次执行
    + TRX 复用驱动 coverage）；scenario_certify --check；跨 TRX/快照 digest 对比
  expected: 全部执行通过；期望 digest 不变；同 fixture 重复执行结果一致
  actual: >
    quick 8/8 Passed；full 188×2 Passed（首轮 1 文档 tripwire 失败已修复）；
    certification 29/29 0 violations；EXPECTATIONS_DIGEST=UNCHANGED×3；
    8 场景在 quick/full 两 TRX 中 outcome 一致；4 份快照的每场景
    expectationsDigest/executionDigest 与 runtimeSourceHash 完全一致；
    套件内 C5 层3 双跑 digest 相等测试（AssetGovernanceTests 等）随全量通过
  evidence: sim006-quick.trx / sim006-full.trx；runs/ 4 份 resolved-config；
    本文件运行清单

ENVIRONMENT:
  method: live 设备门未请求（--live 未传）
  expected: 与前几层分开；不可用/未运行如实报告
  actual: LIVE=SKIPPED (not requested)×4 次运行；无设备/模型执行声明
  evidence: 各运行输出 GROUP_LIVE 行
```

## 边界与不变量

- 产品代码零改动（git diff 仅 tools/、testsets/、docs/、changes/、.gitignore、evidence/、workitems/）。
- scenarios/ 与 golden 零改动；认证 digest 三次运行 UNCHANGED；无自动重认证路径（T13）。
- AGT-012 范围未触碰；testsets/android-settings 原样。
- 仿真 PASS 未替代任何 live 声明；quick PASS 带 SCOPE_NOTE 不冒充完整验收。
- WorkItem WI-SIM006-001（只读审计）与本文件互为证据；runs/ 快照不提交。

## 附录：2026-10-05 审阅回合（post-closure review rework）

所有者审阅复现并确认 4 项，处置与重验：

| # | 发现 | 处置 | 重验 |
|---|---|---|---|
| 1（高） | `--scenarios SCN-POLICY-001` 越出首批 manifest 仍执行并 PASS（范围不匹配静默通过，违反 A5/D9） | `selection_conflicts()`：显式选择必须是 manifest 子集；T19 | 越界复现命令现在 `FAIL: …scope mismatch rejected before execution`，rc=2；合法子集（SCN-WIFI-001,SCN-BARRIER-001）正常执行 PASS |
| 2（中） | FULL_SOLUTION 名实不符（已无 slnx 单命令，实为逐项目聚合） | 改名 `FULL_TEST_PROJECTS` + NOTE 说明聚合与复用；T9 契约同步。不保留 slnx 命令（会重新引入 sim 重复执行，违反 A6） | full4 输出 `GROUP_FULL_TEST_PROJECTS=PASS` + NOTE |
| 3（中） | runs/ 快照仅本地证据，跨机器不可复原 | 不单方翻转 D7（不提交生成运行文件）；登记所有者待决：提交脱敏快照 vs 外部 evidence 存储。快照可复算部分（per-scenario digest）可由 committed scenarios/*.json 经确定性函数重导 | 见 state.md status log 审阅回合条目 |
| 4（低） | 审计报告 WorkItem status 文字过期（in_progress vs 实际 done） | 带日期注记修正（终态以 WorkItem 文件为准） | scenario-audit.md 头部 |
| 附 | "8/8" 混同载体通过与声明成立 | 新增 `SCENARIO_CAVEATS`（`execution.kind=none` 机械推导，非人工清单/PERC 特判，T20）：载体 PASS ≠ 库声明成立；输出与快照均携带 | full4：`SCENARIO_CAVEAT=SCN-PERC-001 …`、`SCENARIO_CAVEATS_SUMMARY=1/8`；快照 `scenarioCaveats=["SCN-PERC-001"]` |

**本回合验证**：self-test T1–T20 全 PASS（rc=0）；越界拒绝 rc=2；合法子集 rc=0；full 重跑（`--scope full`）7 组全 PASS、`EXPECTATIONS_DIGEST=UNCHANGED`、`FINAL_STATUS=PASS`（rc=0），快照 `SIM-006-full-20261005T120623Z`。C7 realization 语义与 SMOKE/WIFI 载体合并仍未触碰（后续致因 Change）。

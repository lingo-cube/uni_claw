# Phase 3 — Bug Extension Conformance 运行记录（进行中）

> 前置：`LOCAL_UNICLAW_DEBUG_EXTENSION_READY`。目标：真实 Runtime Bug 全流程
> （Failure → diagnosing-bugs → extension 证据 → FDP/Owner/RC → STOP →
> EXPLORE_RESOLVED → UniFlow Fix → Regression GREEN → Review/Verify）。

## 诊断日志（Pre-UniFlow，extension 语义实际参与）

### 对象选择（按证据收敛）

1. **候选源**：`runtime-debug-post-graduation-conformance-repair`（0/11，
   proposal 记载 reader/generator/bundle 三类真实缺陷）。
   工作区：`git worktree ../uni_claw-p3`（uni-agent@ab70f82）。
2. **P1d 主张（生成物 schema-invalid）检验**：
   - happy-path bundle 与 no-chain bundle 各生成 packet，经独立 Draft
     2020-12 校验（本地 registry 解 `uniclaw.local` 引用）→ **均 0 错误**。
   - 假线索排除：生成 packet `EvidenceChain` 键呈字母序 ≠ 缺陷（JSON 对象
     键序语义为空；`CAUSAL_STAGES` 本身为规范序元组，构造点 `query.py:239`
     无违约）——**STAY_IN_DIAGNOSIS 规则拦截一次假修复**。
3. **P2c 主张（reader 接受悬空 ref / 畸形 chain）对抗探针**（E2 级证据，
   逐向量实测）：

| 对抗向量 | reader 结果（summarize 入口） |
|---|---|
| TerminalState 悬空 EvidenceRef 注入 | `SCHEMA_VIOLATION` 拒绝 ✅ |
| 未知 chain 阶段注入（rogueStage） | `SCHEMA_VIOLATION` 拒绝 ✅ |
| 缺失 required 阶段（del fused） | `SCHEMA_VIOLATION` 拒绝 ✅ |

**当前结论**：proposal 中心主张在 ab70f82 上不（再）复现；尚无真实 Failure。
`_validate_reference_closure`（packet.py:408-424）实测 fail-closed。

### 下一步（进行中）

产品测试套件全量运行（`dotnet test src/UniClaw.Runtime.sln`，后台）——
任何真实失败即为本 Phase 对象；全绿则记录「当前无可复现真实失败」并
重新评估对象来源（不制造假 bug）。

## Extension 参与度记录（截至目前）

- E-level 选择（E2：产物级执行历史检查）✓ 实际使用
- Expected/Observed/Gap 对照 ✓（每探针一组）
- 假线索否证（先证据后归因）✓（字母序线索被证据否决）
- FDP/Owner/Root Cause：待真实 Failure 出现后走全流程

## 真实 Failure 源确认（dotnet 全量）

- 全量：**189 失败 / 2550 通过 / 0 跳过**（E1 证据：job bash-42 输出）。
- **已知红基线**：interim 记载冻结时点 156 失败（迁移债残余 + 基线）——
  差值 33 需分类（疑似本机环境门 RealDevice/VisionHost）。
- 取样（E3 完整 trace）：`SQ1_MalformedProvisional_CleanAccepted`——期望
  `RunState.Completed`（ScrollEvidenceQualityTests.cs:366），实际
  `RunState.Failed`，reason=`Verified bounded traversal completion but fresh
  GoalEvidence remains unsatisfied：capstone goal evidence`；4 子节点
  verified return + post-completeness PASS 后目标证据不满足；确定性 ~156ms。

## 委派 WI-P3-001（进行中）

分类 189 失败（环境门/基线债/新回归）+ 选定 1 个完整闭环候选 +
extension 格式证据包（fresh subagent `0a6f7ae7`，required_skills:
diagnosing-bugs + extension 规范注入）。只读，零代码修改。

## 所有者裁决（2026-09-06，ADR-0006）

Phase 3 改为**只验证诊断**：Fix 相关标准（Fix 只在 UniFlow 内发生 /
Regression RED→GREEN / Review+Verify 实际执行）记 `NO_REAL_BUYER`
（载体 = 在途产品债，归产品线；本会话产品 worktree 保持只读）。
诊断相关标准仍须由真实 Failure 的证据包支撑。据此进入 Phase 4。

## Phase 3 收口（诊断面，ADR-0006 裁定后）

### Leader 独立核验（Gate 3）

- FDP 抽读三处全部吻合：`CrossFrameMergeEvidence.cs:63-67`（几何通道：
  同 PrimitiveKind + IoU≥0.9 即合并，无滚动平移建模）、`:150-153`
  ChannelMatches 同逻辑、`Agent.OpenWorld.cs:873-876`（终态 Fail 模板与
  观测逐字一致）。
- 分类对账独立复核：40+149+0=189 ✓；差额 33=17（worktree .git）+16
  （反射签名债）闭合 ✓；两次全量重跑数字与 Leader 一致 ✓。

### Exit Criteria（诊断面 10 项 + 修复面 3 项 NO_REAL_BUYER）

| # | 判据 | 结果 | 证据 |
|---|---|---|---|
| 1 | 真实 Failure 稳定复现 | PASS | SQ1 3/3 次 75ms；全量 189 两次重跑一致 |
| 2 | Expected / Observed 明确 | PASS | 证据包三段式（含 world Visited 4/8 vs 8/8） |
| 3 | Extension 真实采集/定位 Evidence | PASS | E3 级（trace+状态转移+决策记录） |
| 4 | FDP 来自 Evidence 非猜测 | PASS | 代码行级定位 + Leader 抽读复核 |
| 5 | Owner 由项目 truth 支撑 | PASS | seam=感知证据生产×EvidencePolicy 分组（含 interim 记录引证） |
| 6 | Root Cause 与 FDP/Owner 一致 | PASS | 单因果链解释全部 trace 观测（items=6/admitted=4/锚 seq2/终态） |
| 7 | Root Cause 前未进入 Fix | PASS | 全程零修复（WI scope.write 为空，subagent 实测零文件修改） |
| 8 | Root Cause 后 STOP 过 Gate | PASS | 证据包返回 Leader，无生命周期夺取 |
| 9 | Fix 只在 UniFlow 内发生 | **NO_REAL_BUYER** | ADR-0006（所有者裁决） |
| 10 | Regression RED→GREEN | **NO_REAL_BUYER** | ADR-0006 |
| 11 | Review 与 Verification 执行 | PASS(诊断面) | Leader 独立核验（本节）；修复面随 9/10 豁免 |
| 12 | Extension 未宣布 COMPLETE | PASS | Worker 仅返回 STATUS；COMPLETE 由 Leader 记录于此 |
| 13 | 产品语义停留 Project extension | PASS | 产品代码只读；harness 侧仅新增 evidence/WI/ADR |

### 声明

```text
UNICLAW_DEBUG_EXTENSION_CONFORMANCE_PASS（diagnosis-only，依据 ADR-0006）
```

### 遗留（转 Phase 4 观察清单）

1. Semantic.Tests 项目另有 94 失败（缺本地模型资产疑因）——产品线事项。
2. 冻结基线 156 无逐测试清单——签名级+计数级对账已闭合，集合级 diff 需主
   checkout 冻结态重跑（如需更强证明）。
3. worktree `.git` 文件陷阱（17 个 Architecture guard 失败）——影响任何
   worktree 内的委派执行，记为 dogfood friction 候选（修复属产品测试基建）。

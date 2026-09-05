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

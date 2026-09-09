# RVR-001 — UIW 系列 review 修复（双轴评审 3 项外科修复 + 2 项裁决记录）
lifecycle_state: closed · disposition: none · depth: standard · base: 28ae4c36

## Intent（WHAT/WHY）
对 adddd89d..28ae4c36 六提交系列的双轴 code-review（Standards×Spec 并行
sub-agent，2026-09-08）发现 2 hard + 若干 judgement/缺口；本 change 外科修复
其中三项（避开并行会话 CDS-001/DSE-001 施工面），两项裁决记录不改码。

## Scope（修复项）
- F1 `World/GroundingSeam.cs` + `WorldModel.ResolveCurrent`：
  `CurrentGroundingView.OwningContainerId` 从 consumer 输入回显改为 **owner
  fact**（匹配候选的 OwningContainerId 派生：唯一共同 owner → 该 id；候选
  owner 不一致或缺失 → null）。ADR-0011 原则 4 闭合。
- F2 `WorldModel.ResolveContinuity`：owning-container 缺失级联检查移出
  ReferenceEstablished/SameReferent 分支——所有判别结果（含 Contradicted/
  Ambiguous/Insufficient）统一执行级联；补测试（Contradicted 时容器缺失 →
  item Ended(container-scope-ended)，Contradicted 本身仍不 Ended）。
- F3 `Effects/TargetBinding.cs` + `UniKernel.ActViaCurrentGrounding`：
  `CandidateBinding.ForUiTarget(UiTargetReference, string sourceRevisionId,
  bool isAmbiguous = false)` 工厂——`null!` 压制收拢到工厂一处；调用点删
  null-forgiving。

## 裁决记录（不改码）
- R1 descriptor hint（"role[:desc]" 作 ControlIntent.TargetSubject）= P8
  合法 target hint 通道（hint ≠ binding ≠ UI targeting）；CONTEXT.md
  Avoid 约束的是 binding 通道（UI 通道已恒绑 OccurrenceRef）。在 CONTEXT.md
  Control Intent 词条补一句澄清。
- R2 UI stale 校验「以 UiTarget.SourceRevisionId 为准」维持（UIW-004 已有
  裁决注释：UiTarget 是 UI candidate 的权威 revision 锚）。

## Out of Scope
- EB 双分支拒绝级联去重（= DSE-001 Effects/ 施工面，记入其待办——本 state
  留此行为移交凭据）。
- P-UW 逐条测试注释锚点（改以 UIW-003 state 存对照表即可，不动测试）；
  CanonicalBinding 尾字段 Data Clump 捆包（有 DSE 触碰风险，defer）。

## Acceptance
A1 F1：回显消除——descriptor 给 container 与不给 container 两种调用下，
  view.OwningContainerId 均等于匹配候选派生值；owner 不一致 → null（测试）
A2 F2：Contradicted + 容器缺失 → 级联 Ended；Contradicted 单独 → 不 Ended
  （既有 S7 保持）
A3 F3：工厂存在且 ActViaCurrentGrounding 零 null!；既有 166 全绿
A4 CONTEXT.md Control Intent 词条含 R1 澄清句

## Constraints
最小面；不触碰 EffectBoundary.Dispatch/IEffectDriver（DSE-001 面）、
Slice/OccurrenceFact state（CDS-001 面）；确定性。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: dotnet test（全解决方案）+ grep null! 归零核对
  expected: A1–A4 满足；全解决方案全绿
  actual: >
    177/177 GREEN（Kernel 160 + Agent 17；基线实测 174——并行会话已先行合入
    DSE 侧测试，我方 +3 = S2b/S4b/S10d；Leader 独立复跑确认）。F1：view.
    OwningContainerId 派生 owner fact（唯一非 null owner → 该值，否则 null）；
    F2：级联统一到所有判别结果之后（Contradicted/Ambiguous/Insufficient 同样
    级联），Contradicted 本身仍零终止（S7 保持）；F3：ForUiTarget 工厂，
    grep null! 代码级仅工厂一处；R1 句落 CONTEXT.md。偏离 1 条（A2 无法严格
    RED——Containers 单调继承使前提态不可经公共 API 构造；S10d 以回归锁形态
    落地并注明，F2 实现严格按契约）。CONTEXT.md 与并行 DSE 词条混合 →
    选择性暂存仅提交我方 hunk。
  evidence: dotnet test 输出（2026-09-08，两次独立运行）；grep 输出；逐文件
    归属核验（TargetBinding/UniKernel/WorldModel/GroundingSeam/两测试文件 = 纯我方）
```

## Status log
2026-09-08 · understanding→resolved · 双轴评审报告聚合（Standards 6 发现 /
  Spec 7 发现，1 项经复核撤销）；修复/裁决/移交三分类裁定
2026-09-08 · resolved→persisted→planned · state.md 建立；F1→F2→F3→CONTEXT
  一句→全量回归
2026-09-08 · planned→implemented · 委派 fresh subagent：S2b/S4b/S10d RED →
  三修复 + CONTEXT 句 → GREEN
2026-09-08 · implemented→reviewed · REVIEW：红线遵守（DSE/CDS 施工面零触碰）；
  偏离 1 条（A2 可达性受限→回归锁，如实注明）；基线漂移 166→174 正确识别为
  并行会话合入
2026-09-08 · reviewed→verified→closed · Leader 独立复跑 177/177 + null! 审计 +
  逐文件归属核验；选择性暂存提交（CONTEXT.md 仅我方 hunk，DSE 词条留工作树）

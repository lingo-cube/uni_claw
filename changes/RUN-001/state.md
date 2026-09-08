# RUN-001 — Real Primary Run Identity（内容派生 RunId 注入）
lifecycle_state: closed · disposition: none · depth: standard · base: 10cd12f6

## Intent（WHAT/WHY）
替换 `RunModel.RunId = "run-1"` 的 realization 占位（P18 Reference Realization
注记），为 P18 Runtime Outcome envelope 与 TRC-001 trace correlation root
提供真实 canonical run identity。

## Scope
- RunModel.AdmitContract 首次接受时铸造 RunId = "run-" + accepted
  Contract View 内容的 SHA-256 hex 前 12 位（确定性派生，同构 EvidenceId
  `ev-` / ContainerIdentity `ctr-` 内容寻址先例；canonical 拼法与前缀长度
  = realization）。
- 铸造后 immutable；同 version 幂等 re-admit 不重铸。
- P18 协议基线 Reference Realization 占位注记更新。
- 既有断言 "run-1" 字面量的测试改为计算期望（盘点：仅
  TerminalOutcomeTests.cs:159 断言 kernel 派生值；Agent 侧 2 处
  "run-1" 为构造入参、不依赖 RunModel，保留为不透明 ID）。
- 新增 RunIdentityTests：跨实例确定性 / 区分性 / 集合无序归一 /
  re-admit 不重铸 / 非 "run-1" 形态。

## Out of Scope（禁止）
- 多 Run cardinality / cross-run identity / continuation 语义（基线 §22 不锁定）。
- caller-supplied RunId 注入缝（人工裁决被拒方案）。
- TRC-001 S3 实现（本 change 只解除其阻塞）。
- P1–P22 任何语义修改（仅 P18 Reference Realization 注记更新）。

## Decisions
- 人工裁决（2026-09-09，TRC 序列 G2 gate，二选一）：内容派生方案。
  identity authority 100% 留在 RunModel（sole authority）；caller 不参与
  铸造。被拒：caller-supplied 显式注入（caller 成共同 authority + 新增
  注入缝 + 确定性责任外移到 caller）。
- 同 contract → 同 RunId 为特性（replay / trace 对照稳定），非缺陷；
  单 Run cardinality 下碰撞无害。
- canonical 输入 = Contract View 六字段（Version / Objective / Scope /
  AllowedEffects / ForbiddenEffects / ProofCriteria）；set 字段排序后
  参与哈希（消除插入序影响）；Obligations 不参与（admission 等价以
  View 为准，View 不含 Obligations）。

## Acceptance
1. 同 contract 跨 RunModel 实例 → 同 RunId。（GREEN）
2. 不同 contract → 不同 RunId。（GREEN）
3. RunId 铸造后 immutable；同 version re-admit 不重铸。（GREEN）
4. set 字段插入序不同 → RunId 不变。（GREEN）
5. RuntimeOutcome envelope 携带派生 RunId（TerminalOutcomeTests 改为
   计算期望后 GREEN）。（GREEN）
6. 既有 107 测试零回归；总计 112 = 107 既有 + 5 新增。（GREEN）

## Constraints
不动六个 L2 公开 interface；确定性优先（无 wall-clock / random / ambient
输入参与派生）；不改 TRC-001/ADR-0013 语义。

## Verification
```yaml
verification:
  level: DETERMINISTIC
  method: >
    dotnet test tests/UniClaw.Kernel.Tests + tests/UniClaw.Agent.Tests；
    新增 RunIdentityTests（5 facts）；TDD：RED → 最小实现 → GREEN
  expected: >
    新增 5 测试全 GREEN；既有 107 零回归；envelope RunId =
    run-<hash12> 形态（非 "run-1"）
  actual: >
    RED 精确命中（2 失败：NotPlaceholder / DifferentContract，3 占位下
    巧合通过）→ MintRunId 最小实现 + TerminalOutcomeTests:159 改计算期望
    → 112/112 GREEN（Kernel 95 + Agent 17）。残留 "run-1" 仅 Agent 不透明
    入参 ×2 + RunIdentityTests 防回归断言 ×1（盘点一致）。
  evidence: dotnet test 输出（2026-09-09，95+17 全绿）；RUN-001 commit
```

## Status log
2026-09-09 · understanding→resolved · G2 gate 人工裁决：内容派生；
"run-1" 字面量盘点完成（1 处真断言 / 2 处不透明入参）；EvidenceId 哈希
先例确认（SHA-256 + hex + 前缀）
2026-09-09 · resolved→persisted · state.md 建立（base 10cd12f6；工作树
已收口，仅 .tmp-hf-intake untracked）
2026-09-09 · persisted→planned→implemented · RED（2 失败）→
RunModel.MintRunId（canonical View 六字段，set 排序归一）+
TerminalOutcomeTests 改计算期望 → GREEN
2026-09-09 · implemented→reviewed · REVIEW：六 L2 公开 interface 零触碰；
P18 仅 Reference Realization 注记；Agent "run-1" 如盘点保留为不透明入参；
无未授权改动
2026-09-09 · reviewed→verified→closed · 112/112 GREEN；Acceptance 1–6
逐条满足 → PRIMARY_RUN_IDENTITY_ESTABLISHED（TRC-001 S3 阻塞解除）

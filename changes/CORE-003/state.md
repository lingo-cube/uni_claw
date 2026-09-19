# CORE-003 — Core 对象与最小语义关系
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent

## Intent

先建立 Core 的最小语义对象和关系，再用纯 Core 场景验证；本 Change 不对齐旧模型，
不迁移源码，不冻结最终 API。

## Acceptance

1. Core 对象职责覆盖 Clause、Segment/Slice、Evidence、Claim、Event 语义和
   Effect/Attempt/TargetBinding 执行层次。
2. Segment 与 Slice 能表达连续引用、局部范围、重叠覆盖和不可改写的历史。
3. Evidence、Claim、Event 的来源、判断、发生和因果边界不混淆。
4. Effect、Attempt、TargetBinding 能区分逻辑操作、实际尝试和当次绑定。
5. Unknown、Stale、Ambiguous、Unauthorized、Unverified 不被自动升级。
6. Core 无 UI、设备、Runtime、Harness 或旧模型依赖。
7. Core 语义测试通过；测试不依赖旧类一一映射。
8. 未因本 Change 修改旧模型、迁移源码或冻结最终字段/继承树。

## Verification

```yaml
level: DETERMINISTIC
primary_seam: UniClaw.Core.Tests
method: |
  dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --logger 'console;verbosity=minimal'
  dotnet test UniClaw.Kernel.slnx --logger 'console;verbosity=minimal'
expected: |
  Core object and invariant tests pass；dependency closure remains domain-neutral；
  solution 全量通过。
actual: |
  Core.Tests 11/11；Core projection seam + ProductHostClosureTests 9/9；solution 527/527
  （Agent 17 + Core 11 + Simulation 112 + Kernel 388）。
  Acceptance 1–8 逐条有测试覆盖（见 evidence 覆盖映射）。发现并留痕一处候选摩擦：
  候选记录列表字段（IReadOnlyList）参与 record 相等性按引用比较，跨构造路径
  状态全等需字段级比较（删除/演化检查实际撞上）；未单方面改为内容相等，留待裁决。
  未对齐旧模型、未迁移源码、未新增项目。NU1900 环境警告不影响执行。
  evidence: evidence/2026-09-19-core-003-object-baseline.md
```

## Status log

2026-09-19 · planned · `to-spec` 根据 CORE-001 锁定基线、vNext.1、场景材料和已验证 tracer 整理规格；未开始实现。
2026-09-19 · planned→implementing · 开始实现（DSH 会话）：Clause 增补 Kind 四类；Event 发生语义按 spec 以 Evidence+Claim 组合表达（不建独立类型，CORE-001 §4.4 缺口保持开放）；新增 CoreObjectRelationTests 五项（授权结构证明 / 关系链 tracer / Event 七面 / 删除-演化双路径 / 纯度白名单）。
2026-09-19 · implementing→verified · Core.Tests 8/8；solution 525/525。留痕候选摩擦：列表字段引用相等（跨路径状态全等改用字段级比较，内容相等性是否进入候选协议留待后续裁决）。Event 独立类型无反例不引入；ClauseKind 四词为候选词汇；严格最小性与旧模型对齐（后续 Change）不在本 Change 声明。关闭待 review。
2026-09-19 · verified→implementing · 在 review 收口前移除不可证伪的 `ProducesWorldResult` 常量谓词，并将 `TargetBinding.BasisSliceId` 放宽为可选。
2026-09-19 · implementing→verified · Core.Tests 11/11；Core seam/closure 9/9；solution 527/527；补充显式 Clause 权限、Event 组合语义和固定非 Slice 依据测试。
2026-09-19 · verified→closed · CORE-003 Core 对象基线完成。关闭不表示严格最小性、旧模型对齐或生产 API 冻结。
2026-09-19 · post-close review · grill-with-doc 完成：未发现必须新增 Core 顶层对象的反例；Event 继续组合表达；BasisRef、Attempt/Effect 执行维度和 Slice 参考系列为字段/使用契约待裁决。证据：evidence/2026-09-19-core-003-grill-with-doc.md。

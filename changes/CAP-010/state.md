# CAP-010 — capability 维度抽样检查收口：索引修正、协议指南升格、R5 可替换性执法测试
lifecycle_state: closed · disposition: none · depth: standard · base: 63804f7a

## Intent（WHAT/WHY）

对 capability 维度做组件符合性抽样检查（capability-component skill 判断门：
该组件进 Capability Registry，适用）后发现三个问题，本 change 一次收口：

1. `docs/capability-hub/README.md` 的「当前 Change State / 验证证据」仍指向
   CAP-001，而 capability 事实已推进到 CAP-006 → CAP-007 → CAP-008（均
   CLOSED，2026-10-06）——管理入口索引过期。
2. `docs/capability-hub/customization-integration-development-protocol-v0.1.md`
   头部 `Status: DRAFT / Authority: NONE` 与其被 capability-component skill
   引为真相源的地位不匹配（skill 为 HOW-only，买方五问/协议选择表/感知特殊
   规则的 canonical 出处即本文档）。
3. R5 可替换性（同协议 realization 替换只动 adapter+组合根、L1 消费方零改动）
   缺确定性测试执法——capability 测试目录无「同一 L1 消费闭包喂两个
   realization 实例」形状的测试。

## Scope / Out of Scope

In：上述三处修复；`tests/UniClaw.Kernel.Tests/Capability/` 新增 R5 测试。
Out：不改 Kernel 产品源码与公开面（白名单不受影响）；不追溯感知
description-only 注册（known gap：uni.perception 实例化归在途 CAP-009；
slow.visual 后续）；不做 task-scoped binding（CAP-005 暂缓项）。

## Decisions

- D1：协议指南升格为 `FROZEN / PROTOCOL GUIDE v0.1` + `Authority: COMPONENT`
  （体例先例：`docs/architecture/perception-provider-baseline-v0.1.md` 的
  COMPONENT-BASELINE）。capability-hub 不在 DocsMetadataTests 五目录执法
  范围内，但升格后语义须自洽：架构权威仍在 ADR-0035/0038 与产品基线，
  冲突时以其为准（头部显式声明）。
- D2：R5 测试选 ModelRouting 侧（当前唯一已冻结且有实例的 L1：
  `IModelManagement`）。两个 realization：replay 缺省
  （`SlowReplayProfiles.CreateDefault()`，internal 经 InternalsVisibleTo
  合法访问）与 OpenCode 形状双候选偏好集。消费闭包只写 `IModelManagement`
  （L1），断言可互换行为契约（解析成功 → 健康翻转后回退或 fail-closed →
  未知 profile fail-closed），并经 `CapabilityRegistry` 实例注册-取回运行
  （R1 执法同时覆盖）。感知侧 R5 被 L1 未冻结阻挡：在途 CAP-009 落地
  uni.perception 双协议实例后可补同形状测试，本 change 不越界预做。

## Acceptance

| # | 判据 |
|---|---|
| A1 | docs/capability-hub/README.md 不再以 CAP-001 为「当前」Change State，指向 CAP-008 并注明 CAP-006→008 谱系 |
| A2 | 协议指南头部为 FROZEN / PROTOCOL GUIDE v0.1 + Authority: COMPONENT，并声明架构权威从属关系 |
| A3 | 新增 R5 测试：同一 L1 消费闭包在两个 realization 上行为契约互换成立，且经 registry.Resolve 取回实例运行 |
| A4 | DocsMetadataTests、KernelRuntimeSurfaceWhitelistTests、capability 全套绿 |

## Constraints

- 不修改 Kernel/Host 产品源码。
- 遵循 docs/README.md 五目录分类学（capability-hub 非执法目录，但不得引入
  与其冲突的语义）。

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet test tests/UniClaw.Kernel.Tests --filter 'FullyQualifiedName~Capability|FullyQualifiedName~DocsMetadata|FullyQualifiedName~KernelRuntimeSurfaceWhitelist'"
  expected: "全部通过（含新增 R5 测试）"
  actual: "专项 102/102 绿（含 ModelRoutingReplaceabilityTests 3 项）；全量 solution 复跑 1386/1386 绿；DocsMetadataTests/白名单不受影响（未改产品源码与公开面）"
  evidence: "evidence/cap-010/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 抽样检查产出三问题；所有者指令一并收口；CAP-009 已被在途 perception change 占用，顺延为 CAP-010。
- 2026-10-07 · PERSIST → IMPLEMENT · 两文档修正/升格 + R5 测试落地；未改产品源码。
- 2026-10-07 · IMPLEMENT → VERIFY → CLOSED · 专项 102/102、全量复跑 1386/1386 绿（首跑 2 例场景认证失败为在途 SCN 修改的并行偶发，单独与复跑均绿，判定非本变更引入，记录于 evidence）；INDEX 已再生（148 changes, 2 open）。

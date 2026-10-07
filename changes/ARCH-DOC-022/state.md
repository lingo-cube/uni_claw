# ARCH-DOC-022 — src/ 维度 README 登记执法测试 + Core/Agent.Dsh README 补齐
lifecycle_state: closed · disposition: none · depth: standard · base: 90908c1c

## Intent（WHAT/WHY）

ARCH-DOC-020/AGT-018/HOST-002 建立的每维度 README 体系目前靠文字纪律维持
（根 README 变更规则），无执法；漂移已真实发生（ARCH-DOC-020 期间并行
会话新增 5 文件，靠手工覆盖核对拦截）。把覆盖核对固化为确定性测试，
新文件不登记 = RED。执法需全覆盖，故先补齐仅剩的两个无 README 项目：
`src/UniClaw.Core/`（共享词汇）与 `src/UniClaw.Agent.Dsh/`（DSH
realization adapter，12 文件）。

## Scope / Out of Scope

In：`src/UniClaw.Core/README.md`、`src/UniClaw.Agent.Dsh/README.md`（新）；
`tests/UniClaw.Kernel.Tests/SourceReadmeOwnershipTests.cs`（新执法测试）。
Out：不改既有 README 内容（除登记需要）；不改产品代码；不覆盖
platforms/、dsh/、tools/（后续按需扩）。

## Decisions

- D1：执法范围 = src/ 六项目全部 .cs（排除 obj/bin 生成物）；归属映射
  按已冻结决策硬编码（ARCH-DOC-020 的 13 维度映射 + AGT-018 + HOST-002
  + 本 change 的 Core/Agent.Dsh），映射变更须经 change 修订测试
  （KernelRuntimeSurfaceWhitelistTests 先例）。
- D2：两条执法线：① 每个 .cs 文件所在目录有归属条目（新目录无条目 =
  RED）；② 文件 stem 在归属 README 中点名（未登记 = RED）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | Core/Agent.Dsh README 落地（1 + 12 文件归属） |
| A2 | 执法测试通过：src/ 全部 .cs（当前 ~120）在归属 README 点名 |
| A3 | 负向自证：临时漏登记场景能被测试拦截（evidence 记录方法与输出） |
| A4 | Kernel.Tests 除已知并行在途红（untracked analysis 文档）外全绿 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet test Kernel.Tests --filter SourceReadmeOwnership；负向探针；全量"
  expected: "执法测试绿；负向探针 RED；全量仅既有 1 例并行红"
  actual: "首跑拦截 8 处真实漂移（含并行会话 5 新文件）→ 修复后 156 文件全过；负向探针 RED→复绿；Kernel 全量 846/846"
  evidence: "evidence/arch-doc-022/"
```

## Status log

- 2026-10-07 · UNDERSTAND → RESOLVE → PERSIST · 所有者按推荐 1 授权
  「直接干」；ARCH-DOC-022 开立。
- 2026-10-07 · PERSIST → IMPLEMENT → REVIEW → VERIFY → CLOSED · 首跑
  即拦截真实漂移（映射缺 2 子目录 + 并行会话 5 新文件未登记），当轮修
  复；负向探针自证；全量 846/846 绿；INDEX 已再生。

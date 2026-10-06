# ARCH-DOC-020 证据 — Kernel 维度 README 补齐（按 L0-L3 架构粒度）

## A1/A2 — 维度划分与 README

新增 10 个 README（零代码改动）：

- 六 L2：`Evidence/` `World/` `Run/`（含 `Outcome/` 归属说明）`Control/`
  `Assurance/` `Effects/`（含 `ExecutionSource/`）
- 观测面：`Trace/`、`Diagnostics/`
- `Runtime/`（Uni Kernel 驱动面，公开面=白名单冻结面）
- `src/UniClaw.Kernel/README.md`（根总索引 + `Core/` 投影缝一节 +
  全维度导览）

每篇只做「目录文件 ↔ 基线 §/ADR」归属映射与变更规则，不复述权威内容
（D2 防第二真相源）。

## A3 — 覆盖核对

```sh
python3 覆盖核对脚本（目录↔对应 README 点名）
# expected: 全覆盖
# actual:   MISSING: 0 · coverage: 72/72（2026-10-07，见 coverage-check.txt）
```

期间并行会话向 Run/Control/Assurance 新增 5 文件（RunModel/
ExecutionContract(+View)/DescriptorTargetPolicy/RuntimeAssurance/
PostActionEffectVerification）——已按头注释补入归属表后复核通过。
覆盖为时点快照；后续新文件由根 README 变更规则约束登记。

## A4 — 回归

```sh
dotnet test tests/UniClaw.Kernel.Tests
# expected: 全绿（含 DocsMetadataTests 4 例）
# actual:   已通过! 841/841（2026-10-07，见 kernel-tests.txt）
```

## 文件

- `coverage-check.txt` — 72/72 覆盖输出。
- `kernel-tests.txt` — Kernel 全量 841/841。

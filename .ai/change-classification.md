# Change Classification — UniClaw Agent Runtime

> 定位: 变更风险分级（跨助手，Codex + Claude 共用）。只做分级，不产生架构权威。
> 上级: AGENTS.md「Change Classification」；详细生命周期与 Gate 见 `.ai/development-protocol.md`。
> 与本协议 §17.1 Task Classification（L0-L4，按证据要求分级）互补：本文按**变更范围**分级。

## Small — 单文件 · 无 contract 变化 · 无 architecture impact

- 单文件修改（或同一文件内局部改动）
- 不改变任何 contract / invariant / authority / ownership / dependency
- 不需要 OpenSpec change
- 验证: targeted build + targeted tests

## Medium — 多文件 · 已有 contract 内修改

- 跨多个文件，但仍在已批准 contract 内
- 不引入新 abstraction / 新 boundary / lifecycle 变化
- 通常对应已批准 OpenSpec change 内的 task；若 contract 未覆盖，先 propose
- 验证: 相关套件 + Architecture Guards + `scripts/check-consistency.sh`

## Large — 新 abstraction / 新 boundary / lifecycle change / architecture change

- 新 abstraction、新 capability、新 boundary
- mutable-state ownership / decision authority / dependency direction 变化
- lifecycle、completion、recovery、safety 语义变化
- invariant 修改

**Large 必须:**

1. OpenSpec change（propose → apply → verify → archive，见 `.ai/openspec-workflow.md`）
2. Human Gate（按 `.ai/development-protocol.md` §7 的 material boundary 定义）

## 边界规则

- 分类不确定时，取**更高一级**（fail-safe）。
- 禁止把 Large 拆成多个 Medium 以绕过 gate。
- 本文件不建立新的 Decision 或 Gate — OpenSpec 生命周期与 Human Gate 由 `.ai/development-protocol.md` 定义。

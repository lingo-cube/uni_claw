# ARCH-DOC-024 — 组件分级与工程化管理审查及 P0/P1/P2 优化建议
lifecycle_state: persisted · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

只读梳理当前 UniClaw 的组件分级、职责边界、工程化管理和已验证证据，提出 P0/P1/P2 优化建议，为后续实现 Change 提供可判定入口；不修改产品代码，不把仿真证据升级为产品完整性结论。

## Scope / Out of Scope

In：Product T0–T3、Harness/Provider/测试治理面的组件分级；组件 owner/authority/可替换边界；P0/P1/P2 管理建议；当前验证结果。
Out：任何产品源码、测试、场景、协议状态机、Memory/P24/P25 实现；不经过 grill。

## Decisions

- 依现行产品架构基线、协议基线、仓库职责基线和 CONTEXT.md 读取，不创建第二套组件拓扑。
- 本 Change 只持久化分析，不进入 IMPLEMENT。
- 场景 28/28 与 Kernel 测试只证明已覆盖范围，不证明真机、跨进程或完整产品生命周期。
- 保留工作树已有未提交改动。

## Acceptance

1. 分析文档列出组件分级、职责/authority、工程化管理现状和证据边界。
2. 文档给出可执行、可验收的 P0/P1/P2 建议。
3. 当前现场验证命令与结果准确记录。
4. 未修改产品代码、测试、场景和协议状态机。
5. changes/INDEX.md 保持本 Change 为 persisted。

## Constraints

- 只读分析；不经过 grill。
- 不将代码存在或测试通过单独解释为协议 verified。
- 不清理、重置或覆盖其他未提交文件。

## Verification

verification:
  level: CONTRACT
  method: "python3 tools/scenario_certify.py --check; python3 tools/scenario-coverage.py --run; python3 tools/gen-protocol-machine.py --check; git status --short --branch"
  expected: "认证无违规、覆盖 28/28、协议生成检查通过、既有未提交改动仍保留"
  actual: "认证 PASS（28 files, 0 violations）；覆盖 PASS（28/28，20 golden + 8 descriptive）；协议检查 PASS（5 machines, 28 transitions, 44 edges）；工作树既有未提交文件保留"
  evidence: "docs/analysis/uniclaw-lifecycle-completeness-and-optimization-2026-10-12.md"

## Status log

- 2026-10-12 · UNDERSTAND → PERSIST · 完成只读组件分级与工程化管理审查，形成 P0/P1/P2 优化建议；未改产品代码，保留工作树已有改动。


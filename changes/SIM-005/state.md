# SIM-005 — Scenario coverage mapping performance

lifecycle_state: closed · disposition: none · depth: standard · base: f806d188

## Intent（WHAT/WHY）

`tools/scenario-coverage.py --run` 已经能从新鲜 TRX 推导场景真值，但它为每个场景
重复启动 `dotnet test --list-tests`，并在每次探测中重复构建检查。29 个场景的覆盖率
报告因此接近一分钟以上，容易被短时工具等待误判为卡住。

## Scope / Out of Scope

- Scope：复用 `run_tests()` 已完成的 Simulation 构建；并行执行只读 trait 映射探测；
  保持每个场景独立 filter、FQN join、TRX freshness 和 fail-closed 语义。
- Out of Scope：不改变场景 schema、测试 trait、TRX 内容、认证规则或派生状态规则；
  不并行构建，不改变 Product/Simulation 运行时。

## Decisions

1. `--run` 复用 `run_tests()` 已完成的构建；显式 `--trx` 先执行一次
   `--list-tests --no-restore` 预热（保留缺失二进制时的构建能力），随后每次
   `scenario_test_map()` 探测固定使用 `--no-build --no-restore`。不带参数自动发现
   最新 TRX 时只读使用已有测试二进制，不触发构建。
2. 使用最多 4 个 worker 并行读取测试二进制；结果按 scenario id 映射回字典，输出
   顺序和真值语义不依赖线程完成顺序。
3. 不缓存跨运行结果；每次 coverage 仍重新从当前二进制发现 trait，避免陈旧映射。

## Acceptance

1. 新鲜 `--run` 仍执行 Simulation 并生成 TRX。
2. 29 个场景仍全部由 TRX 派生为 `passing`，认证和 freshness 检查保持通过。
3. 映射探测不触发重复构建，且并行探测不会改变 scenario→FQN 映射。
4. 变更只影响覆盖率工具，不改变场景执行或 Product 代码。

## Verification

```yaml
level: DETERMINISTIC
method: >-
  python3 tools/scenario-coverage.py --run;
  python3 tools/scenario-coverage.py --trx tests/UniClaw.Simulation.Tests/TestResults/coverage.trx;
  python3 tools/scenario-coverage.py; python3 tools/scenario_certify.py --check;
  python3 -m py_compile tools/scenario-coverage.py; git diff --check
expected: >-
  coverage exit 0，29/29 场景 passing，认证 0 violations；脚本语法和差异检查通过。
actual: >-
  coverage exit 0，Simulation 188/188，29/29 passing，certified by CAP-001；
  `--run` 总耗时约 25 秒（修复前约 95 秒），显式 `--trx` 约 17 秒，默认最新
  TRX 路径约 15 秒；认证检查和 py_compile 均通过。
evidence: >-
  /tmp/coverage-fixed-parallel.log；/tmp/coverage-explicit.log；/tmp/coverage-default.log；
  命令输出；git diff --check
```

## Status log

- 2026-10-04 · UNDERSTAND → RESOLVE · 复现确认瓶颈是 29 次串行 list-tests 进程及重复 build 检查；Simulation 单次执行约 5 秒且通过。
- 2026-10-04 · RESOLVE → PLAN · 固定 `--no-build --no-restore` 与最多 4 路只读枚举，不改变映射真值链。
- 2026-10-04 · REVIEW → IMPLEMENT · 自动发现路径改为只读；trait 枚举失败改为 fail-fast，避免静默空映射。
- 2026-10-04 · IMPLEMENT → VERIFY → CLOSED · `--run` 25.34 秒、显式 `--trx` 17.36 秒、默认最新 TRX 14.56 秒实跑，29/29 coverage 和认证检查通过。

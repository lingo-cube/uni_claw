# Host Layer

> **Tier 3 · Layers**: 可执行组合根、场景运行与设备测试资产。Host 可以引用
> Core、Device 和 provider；其他生产项目不反向引用 Host。

## Commands

- `doctor --device <serial>`：只读 readiness，禁止隐式启动 AVD。
- `analyze --device <serial>`：一次只读 `PageAnalysis`，动作计数为 0。
- `run --scenario <file> --device <serial>`：执行单个版本化场景。

## Owned Components

- V1 scenario/policy sealed records、严格 JSON 校验、规范化与 SHA-256 hash。
- `ScenarioPlanCompiler`：scenario → 既有 `TraversalPlan`。
- `LocateScenarioStepPlanner`：绑定当前 page fingerprint 的单动作计划。
- `IncrementalScenarioRunner`：observe → analyze → plan → gate → execute →
  re-observe → verify。
- deterministic safety evaluator/decorators：固定 deny precedence、默认拒绝。
- isolated run assets、stable issue fingerprints、redaction 与 authoritative
  `result.json`。

## Safety and Failure Rules

所有真实 launch/click/back/scroll/input/long-press 都在紧邻 Device executor
之前经过同一 gate。Denied action 不调用 inner executor。未知 package/page、
不可信坐标、危险 semantic/text、越界或预算耗尽均拒绝。

`success` 只在场景 success criteria 有证据时产生。目标未验证、列表末端未
证明、设备/provider/报告失败或取消必须分别报告，不得提升为成功。

## Current Delivery Boundary

当前已交付 deterministic/mock 的 locate offline 闭环及安全导航 emulator
诊断。真实 provider、一级菜单枚举、repeat/10-run 稳定性门禁仍在对应
OpenSpec change 中保持未完成；文档不声明这些能力已可用。

# SETTINGS-COVERAGE-003 — 动态前沿驱动的 Android Settings 可达界面遍历
lifecycle_state: verified · disposition: blocked · depth: decision-heavy · base: 64b5d0b6

## Intent
当前 Settings Coverage 只按静态 `targetPages` 证明局部路径，不能回答完整可达界面是否已经遍历。此 Change 将遍历目标改为由真实 perception 观察产生的安全入口前沿，并持久化每个入口的 observed/entered/verified/skipped/blocked 状态；由 Leader 决策、Host 执行、Kernel 保持运行与效果权威、vision/perception 保持观察权威。

## Scope
- 动态记录根页可安全进入的 `ui.element` 前沿，排除 switch、framework button、back、不可唯一目标和未知危险动作。
- 记录入口跨滚动周期的发现、尝试、成功、失败、跳过原因和当前 checkpoint。
- 逐步支持多层安全页面的 frontier 记录；每轮增加预算前先以真实证据复核。
- 保留语言检测、Slow perception、截图/层级、trace、effect receipt、post-action verification 的现有链路。
- 使用注册模拟器和专用 DSH；最多 8 轮有界修复/重试，禁止替代 Kernel 或视觉能力。

## Out of Scope
- 不把未知危险设置、账号/权限/凭据/调试/破坏性动作自动执行。
- 不把静态配置、截图或脚本决策当作 live perception 或 Kernel 结果。
- 不改 Kernel 状态机、Effect authority、vision provider 语义。
- 不承诺跨 Host 恢复或未被真实设备观察到的系统分支。

## Decisions
- 完整性分母使用真实观察到的安全入口前沿；未观察到的屏外项记为 undiscovered，不能伪造为已覆盖。
- 每个 semantic consultation 使用独立 DecisionId；transport retry 复用 correlation，不新增计数。
- 用户允许由 Leader 修改 Contract/预算；Contract 仍在 Kernel admission 后生效，预算扩展要留痕。
- safe-stop/blocked 永不自动产生 Effect；恢复需显式 checkpoint resume。

## Acceptance
- A1：每一轮都有 environment-preflight、observation、Leader decision、Host guard、effect receipt、post-action verification、ledger checkpoint。
- A2：报告能逐项区分 discovered/entered/verified/failed/skipped/blocked/undiscovered，并给出 reason 与 evidence ref。
- A3：已观察到的安全入口不依赖固定 `targetPages` 才能进入；未知危险入口 fail-closed。
- A4：滚动后发现的入口跨后续滚动保留，不能因新滚动丢失旧候选。
- A5：语言检查和慢感知若启用，结果与对应 capture/route 绑定；缺能力则明确 degraded/blocked。
- A6：真实运行终局只允许 CoverageComplete、BoundedStop、FailClosed、EnvironmentBlocked 四类之一；不得把 Kernel terminal 当 coverage complete。

## Verification
```yaml
verification:
  level: ENVIRONMENT
  method: "registered Android emulator + dedicated DSH + Host.Dsh settings coverage with dynamic frontier, checkpoint, language findings, trace and receipts"
  expected: "all reachable safe Settings entries observed and attempted within explicit budget, or every remaining frontier item has an evidence-backed disposition"
  actual: "Host/Kernel/live perception completed two bounded runs at 100% of the declared finite coverage set; eight Leader-file repair/continuation rounds then reached evidence-insufficient on identity-only Android Settings observations. Full Settings-tree closure is not proven."
  evidence: "evidence/settings-coverage-003/manifest.json; docs/analysis/settings-full-traversal-decision-maker-2026-10-15-v2.md"
```

## Status log
- 2026-10-15 · UNDERSTAND → RESOLVE · 复核 SETTINGS-COVERAGE-002 运行证据，确认其只完成局部 characterization，不是完整遍历。
- 2026-10-15 · RESOLVE → PERSIST · 建立动态前沿 Change，保留 002 证据并修正“无响应”与“全树”误归因。
- 2026-10-15 · PERSIST → IMPLEMENT · 增加动态根/子页 frontier、visited、skip reason、identity-only root fallback 与 Host policy 导航修复。
- 2026-10-15 · IMPLEMENT → VERIFY · 定向测试、真实 emulator/DSH/Leader-file 运行完成；两轮有限集合覆盖通过，深度轮次在无 XML grounding 时 safe-stop。
- 2026-10-15 · VERIFY · BLOCKED · 恢复条件：真实 Settings root 在 identity-only observation 后重新获得可用 hierarchy/scroll container，或提供持久化 checkpoint resume seam；在此之前禁止宣称完整 Settings 树闭包。

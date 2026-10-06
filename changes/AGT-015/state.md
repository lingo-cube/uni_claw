# AGT-015 — 真实 Settings 下一批：未知状态安全停止与错误目标拒绝

lifecycle_state: persisted · disposition: none · depth: standard · base: 04fa7cbc

## Intent（WHAT/WHY）

所有者指令（2026-10-06，AGT-014 收口后）：开始下一组真实 Settings 测试，优先覆盖「未知状态安全停止」与「错误目标拒绝」。

确定性对面已在 AGT-014 故障矩阵全绿（未知页 Defer 耗尽→`bounded-stop:unknown-page`；forbidden/unknown/缺失策略→Guard dispatch 前拒绝；非法目标状态 fail-closed），仿真侧亦有承载（SCN-POLICY-006 guard-unknown 零 dispatch、SCN-WIFI-005 safe-stop）。本 Change 把这两个行为推进到**真实模型 + 真实设备**证据级：真机呈现不可判定的目标状态与错误目标候选时，系统必须安全停止或 dispatch 前拒绝，且全过程可追溯。仿真/确定性 PASS 不替代本轮证据。

## Scope / Out of Scope

### 范围

- 行为一（未知状态安全停止）：真机场景中目标控件状态不可判定（typed Unknown/冲突/部分证据）时——零 effect、有界停止、终局与理由可追溯、不伪造 SafeStop/Completion。
- 行为二（错误目标拒绝）：模型指向非目标控件（撞名/相似控件/越权 action class）时——dispatch 前拒绝（Guard/policy/effect-class/target-identity），零 ADB effect，拒绝理由入 trace。
- 测试集扩展：testsets/android-settings 增补对应 task/fixture/acceptance 声明（经 validate-testset-manifests 校验；具体 ref 形态在 PLAN 定）。
- 复用 AGT-014 基础设施：environment-preflight、emulator supervisor、consultations.json、7890 代理链路、trace 分段计时。
- 局部实现缺陷走复现→修复→回归（deterministic 回归必须先红后绿）。

### 不在本 Change 内

- 不修改产品架构、共享协议、模型路由或更换 Agent 模型。
- 不把 live 失败改成 PASS；设备/DSH/网络不可用时保留 ENVIRONMENT_UNAVAILABLE/BLOCKED 证据，不静默降级。
- 不做无界探索；有界预算沿用测试集与 profile。
- 不顺手处理 SIM-007/008 遗留（均已 CLOSED）或场景库演化。

## Acceptance（后续实现完成的判据）

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | 未知状态→安全停止 | 真实回合零目标 effect；终局为有界停止且理由可追溯（decision/guard/observation 链）；method/expected/actual/evidence 四元组落盘 |
| A2 | 错误目标→dispatch 前拒绝 | 拒绝发生在 Kernel dispatch 前（无 ADB receipt）；拒绝理由入 consultations/trace；同链复用回归证明正确目标仍可执行 |
| A3 | 测试集与追溯同步 | 新任务入 manifest 且校验通过；每动作可追 decision、policy digest、guard verdict、effect receipt、post-action observation |
| A4 | 证据等级诚实 | ENVIRONMENT 级独立记录；环境门未过/不可用如实保留；确定性回归与本轮真实证据分开呈现 |
| A5 | 局部修复有回归 | 若需修 Host/投影局部缺陷：复现→修复→deterministic 回归红转绿；不涉架构/协议/模型 |

## Constraints / Owner-Authority impact

- 不取得 canonical authority；安全策略 digest 沿用 AGT-013 预评审静态资产，测试工具不动态签发产品授权。
- 真实设备证据与仿真/确定性证据分层呈现（SIM-006 D1 沿用）；聚合不得遮蔽任一失败。
- 需要设备/DSH 环境时走既有 preflight/supervisor；当前设备已停止，执行待环境重建（PERSIST 不阻塞于此）。

## Verification（本轮 PERSIST）

```yaml
level: CONTRACT
method: python3 tools/gen-open-changes.py; 引用检查（AGT-012/013/014 与故障矩阵证据路径有效）; git diff --check
expected: AGT-015 为 persisted；索引含本 change；无实现、无设备/模型执行
actual: PASS（仅 PERSIST 文档检查）
evidence: 本 state、changes/INDEX.md
```

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 所有者指令创建；确定性对面（AGT-014 故障矩阵）与仿真承载（POLICY-006/WIFI-005）已绿，本 Change 推进两个行为到真实设备/模型证据级；执行待环境重建。

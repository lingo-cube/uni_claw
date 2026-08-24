# Device Action Execution Semantics Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_DEVICE_ACTION_EXECUTION_SEMANTICS_ANALYSIS — analyze
> whether DeviceAction fully expresses the timing / motion semantics real
> device execution requires. **Analysis only — no code changed.** Constraints
> honored: no Agent authority / Traversal ownership / Semantic Capability /
> scenario-knowledge changes.

---

## 1. Current Model

`DeviceAction`（语义动作，6 类）：

| type | fields | translator |
|------|--------|-----------|
| `LaunchApp` | ApplicationId, LaunchIntentAction? | `am start` / `monkey -p` |
| `Tap` | TargetElementIndex?, TargetBounds? | `input tap x y` |
| `SetSwitch` | TargetElementIndex?, TargetState, TargetBounds? | `input tap x y`（同 Tap） |
| `ScrollForward` | StepFraction = 1.0f | `input swipe`（固定 duration） |
| `ScrollBackward` | StepFraction = 1.0f | `input swipe`（反向） |
| `SystemBack` | — | `input keyevent 4` |

**无 Input/type（文字输入）动作**——模型面不含键盘输入类。

## 2. Hidden Execution Semantics

| 动作 | 所需物理参数 | 当前隐藏/固定在哪里 |
|------|--------------|---------------------|
| Tap / SetSwitch | press duration、release timing、post-action timing | `input tap` 是 adb 瞬时点击（无 press duration）；post-action 等待 = Traversal 固定 `DefaultPostActionSettleDelay = 300ms` |
| ScrollForward/Backward | distance、duration、velocity | distance 由 StepFraction 派生（0.4×h×f）；**duration 固定 = adb `input swipe` 默认 300ms**；velocity = distance/300ms 随步长增长（1024→2048px/s）——duration 隐藏在 adb 默认值 |
| SystemBack | press timing、transition settle | `input keyevent 4` 瞬时按键；transition settle = 外部边界 bounded settle（6 帧）/ 普通返回 settle（300ms/帧） |
| LaunchApp | — | 无 timing 建模 |

**共性**：Traversal 对**所有动作类型**用同一固定 300ms post-action delay——没有按动作类型/设备状态区分的 timing 语义。

## 3. Ownership Analysis

- **DeviceAction（语义层）**：正确保持语义纯净（"做什么"）——Agent 只发动作类型 + 坐标/步长，不感知物理细节——分层正确。
- **Translator / Adapter（物理层）**：所有 timing/motion 参数隐藏在这里（duration=adb 默认、press=瞬时、无 velocity 建模）——**物理执行语义的 owner**。
- **Traversal**：post-action timing（固定 300ms）——组合策略 owner。
- **Agent**：不感知执行细节（正确）；但 Agent 的"动作后 300ms"假设对全部动作/设备状态无差别——Scroll 高速时已由 stability 补偿、Tap 转场已由 settle 补偿——**补偿机制在 Agent 侧工作，物理参数缺口在 Adapter 侧**。

## 4. Architecture Impact

- 引入 **ExecutionProfile / MotionProfile 层** = ADDITIVE（Adapter 内部）：
  - 按动作类型 + StepFraction 生成合理 duration/velocity（如 `duration ∝ distance`、velocity 上限、Tap 可选 press duration）——**Agent 语义不变**，仅物理执行参数更完整。
  - 不需要新状态 owner（无状态映射）；不需要跨层契约（Agent 不消费物理参数）。
- 或 DeviceAction 增加**可选** timing 字段（默认行为不变）——Model 层 ADDITIVE，但污染语义层（不推荐首选）。

## 5. Recommended Boundary

1. **首选**：MotionProfile/ExecutionProfile 在 **Adapter 层内部**实现——Translator 按动作类型与 StepFraction 映射 duration/velocity（velocity 上限 ~800-1000px/s），Tap/Back 保持瞬时（无需 press duration 的场景）；Agent 语义层完全不动。
2. **Agent 侧**：继续依赖已有的 bounded settle 补偿机制（post-action settle / scroll stability / external transition settle）——这些已是正确的组合策略。
3. **跨层边界**：物理参数不进入 DeviceAction 语义模型（除非未来出现 Agent 必须控制的执行语义——届时走 OpenSpec 决策）；velocity 上限等物理约束属 Adapter 内部实现细节。
4. **验证**：motion profile 落地后 EBD/Capstone 真机复验（乱码率对比）+ 确定性回归（不受影响）。

## Remaining Risk

- Adapter 层 duration 变化影响真机滚动总时长（探索变慢）。
- velocity 上限需真机标定（过低增加帧数、过高无效）。
- Tap/Back 的 press timing 当前无场景需要（无长按/按键时长语义）——**不在本次引入**，避免过度建模。

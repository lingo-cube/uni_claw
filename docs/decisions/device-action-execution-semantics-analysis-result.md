# PROJECT_LEADER_DEVICE_ACTION_EXECUTION_SEMANTICS_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_DEVICE_ACTION_EXECUTION_SEMANTICS_ANALYSIS — analyze
> whether DeviceAction expresses the timing / motion semantics real device
> execution requires. **Analysis only; no code changed.**
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE** (analysis; the recommended
> MotionProfile is an Adapter-internal execution parameter, not a contract,
> ownership, or Agent-authority change).

---

## 1. Current Model

6 类语义动作：`LaunchApp`、`Tap`、`SetSwitch`（同 Tap 执行）、`ScrollForward`、
`ScrollBackward`、`SystemBack`。**无 Input/type 动作**。DeviceAction 只表达
"做什么"（类型 + 坐标/步长）——语义层纯净，Agent 不感知物理细节（分层正确）。

## 2. Hidden Execution Semantics

| 动作 | 物理需求 | 隐藏位置 |
|------|----------|----------|
| Tap/SetSwitch | press duration / release / post timing | adb 瞬时点击（无 duration）；post 等待 = Traversal 固定 300ms |
| Scroll | distance / duration / velocity | distance 由 StepFraction 派生；**duration 固定 adb 默认 300ms**；velocity 派生（1024→2048px/s）——**duration 隐藏在 adb 默认值** |
| SystemBack | press timing / transition settle | keyevent 瞬时；settle 在 Agent 侧（外部边界 bounded / 普通返回 settle） |
| LaunchApp | — | 无 timing 建模 |

共性缺口：Traversal 对全部动作用同一固定 300ms post-action delay——无按动作
类型/设备状态的 timing 语义。

## 3. Ownership Analysis

- DeviceAction（语义层）：保持纯净 ✓。
- **Translator/Adapter（物理层）：timing/motion 参数的 owner——当前隐藏**
  （adb 默认 duration、瞬时 press）——物理执行语义缺口所在。
- Traversal：post-action timing（组合策略，固定 300ms）。
- Agent：正确不感知物理；依赖 bounded settle 补偿（post-action settle /
  scroll stability / external transition settle）——补偿机制已工作。

## 4. Architecture Impact

引入 ExecutionProfile/MotionProfile = **ADDITIVE（Adapter 内部）**：按动作类型
+ StepFraction 映射 duration/velocity（如 `duration ∝ distance`、velocity 上限
~800-1000px/s）；无新状态 owner、无跨层契约、Agent 语义不变。DeviceAction 增加
可选 timing 字段为备选（污染语义层，不首选）。

## 5. Recommended Boundary

1. **MotionProfile 在 Adapter 层内部**实现（Translator 映射 duration/velocity）；
   Agent 语义层完全不动。
2. Agent 侧继续用既有 bounded settle 补偿（已正确）。
3. 物理参数不进 DeviceAction 语义模型（除非未来 Agent 必须控制执行语义——
   届时走 OpenSpec 决策）。
4. 落地后 EBD/Capstone 真机复验（乱码率对比）+ 确定性回归。

## Remaining Risk

- duration 增大 → 真机滚动总时长增加。
- velocity 上限需真机标定（过低帧数增、过高无效）。
- Tap/Back 的 press timing 当前无场景需要——不引入长按/按键时长建模，避免
  过度设计。

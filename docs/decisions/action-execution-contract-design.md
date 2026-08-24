# Action Execution Contract Design

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_ACTION_EXECUTION_CONTRACT_DESIGN — design the Runtime
> Action Execution Contract boundary based on the DeviceAction execution
> semantics analysis. **Design only — no code changed.** Constraints honored:
> no Agent authority / Planner / GoalEvidence / Semantic Capability changes.

---

## 1. Current Boundary

```
Agent（决策：做什么 + 目标坐标/步长）
   │  DeviceAction（语义，6 类，纯净）
   ▼
Traversal（执行 + post-action timing 300ms 固定 + bounded settle）
   │  DeviceAction
   ▼
DeviceActionTranslator（DeviceAction → AdbOperation，直译）
   │  AdbOperation（Swipe/Tap/KeyEvent/Launch）
   ▼
AdbDispatchTarget（adb 命令）
   ▼
真实设备
```

责任分布（当前）：
- **Action semantic**：Agent 决策（类型 + 坐标/StepFraction）→ DeviceAction。
- **Physical execution**：Translator 直译 + **AdbOperation 已存在**（adapter 内部
  执行描述），但 **duration/velocity 未建模**（Swipe 无 duration → adb 默认
  300ms；Tap/KeyEvent 瞬时）。
- **Transition verification**：Traversal/Agent 的 bounded settle（post-action
  settle、scroll stability、external transition settle）——组合策略已工作。

## 2. Responsibility Gap

| 层面 | 现状 | 缺口 |
|------|------|------|
| Action semantic | DeviceAction 纯净 ✓ | — |
| Physical execution | Translator 直译；AdbOperation 无 timing 字段 | **duration/velocity/press timing 隐藏**（adb 默认值、瞬时）；velocity 随 StepFraction 失控（1024→2048px/s） |
| Transition verification | bounded settle ✓ | settle 与执行参数（duration/velocity）**无关联**——无法按运动特征调整验证策略 |

耦合问题（已确认）：Scroll duration 固定 → velocity 随距离变化 → 高速 fling →
运动模糊 → OCR 乱码 → 归一化失败；时序问题在 Translator（参数隐藏）与
Traversal（固定 300ms）两侧。

## 3. Proposed Contract Boundary

```
DeviceAction（语义，不变）
   │
   ▼
ExecutionContract（NEW — Translator 内部显式物理参数）
   ├─ 从 DeviceAction + 设备尺寸派生：distance / duration / velocity / coordinates
   ├─ duration ∝ distance（velocity 上限 ~800-1000px/s）——Scroll
   ├─ Tap/Back：press duration 保持瞬时（无场景需求，不建模）
   ├─ velocity 校验（超上限 → 重算 duration，而非拒绝——机制内部）
   └─ 输出仍为 AdbOperation（Swipe 增加 Duration 字段；其余不变）
   │
   ▼
AdbDispatchTarget（adb 命令，Swipe 带显式 duration）
```

- **谁创建**：`DeviceActionTranslator`（Adapter 层内部；DeviceAction +
  displayWidth/Height → ExecutionContract → AdbOperation）。Agent 不可见。
- **谁消费**：`AdbDispatchTarget`（物理执行）；`Traversal` 不消费（保持
  post-action settle 不变——transition verification 与物理参数解耦，避免
  新耦合）。
- **Agent 是否可见**：**不可见**——语义层保持纯净；Agent 只发"做什么"。
- **是否需要持久化**：**否**——瞬态执行参数，随动作生命周期存在；不进入
  Observation / Memory / 状态。
- **是否影响 Trace**：**可选诊断字段**——trace 记录动作时附带
  `duration`/`velocity`（如 "ScrollForward duration=600ms velocity=768px/s"），
  仅用于真机问题排查（乱码与速度的证据关联），不改变决策语义。

## 4. Ownership

- **ExecutionContract 的 owner：DeviceActionTranslator（Adapter 层）**——物理
  参数映射的单一 owner；Agent / Traversal / Semantic 不接触。
- AdbOperation（执行描述）扩展 Swipe.Duration —— Model/Adapter 内部类型，无
  跨层影响。
- Transition verification（settle）保持 Traversal/Agent 现状——**不与
  ExecutionContract 耦合**（避免把物理参数引入决策路径）。

## 5. ArchitectureDelta

**ADDITIVE（Adapter 内部）**：
- 新类型/映射：ExecutionContract（或直接扩展 AdbOperation.Swipe 加
  Duration + Translator 内 duration/velocity 计算与校验）。
- 无新状态 owner、无跨层契约、无 Agent/Planner/GoalEvidence/Semantic 变化。
- 不改 fail-closed、不加场景知识、不改语义动作模型。
- Trace 仅加诊断字段（可观测性，非决策）。

## 6. Migration Risk

| 风险 | 等级 | 缓解 |
|------|------|------|
| Translator 重构（直译 → contract 派生） | 低 | 内部重构；语义动作不变；deterministic 测试（contract 单测：duration ∝ distance、velocity 上限） |
| Swipe 加 Duration 改变真机行为 | 中 | 分步：先 Scroll 引入（真机乱码率对比验证），Tap/Back 保持瞬时；velocity 上限真机标定 |
| 现有 translator/E2E 测试适配 | 低 | AdbOperation.Swipe 增加可选 Duration（默认 null → 保持 adb 默认，兼容） |
| Trace 字段扩展 | 低 | 纯附加字段，无断言依赖 |

## 7. Recommendation

**分步引入（推荐）**：
1. **第一步（最小）**：ExecutionContract 仅覆盖 **Scroll**——Translator 内
   duration ∝ distance（velocity 上限 ~800-1000px/s），AdbOperation.Swipe 增加
   Duration；Trace 附加 duration/velocity 诊断。Tap/Back 保持现状（无 press
   duration 场景需求）。回归：deterministic（无真实 swipe 不受影响）+ EBD/
   Capstone 真机乱码率对比。
2. **第二步（评估后）**：若 Tap/Back 出现时序需求，再扩展 contract（走相同
   Translator 内部模式）；否则维持最小面。
3. **不引入**：DeviceAction 携带物理参数、跨层 ExecutionContract（Agent 可见）、
   settle 与物理参数耦合——均过度设计，保持语义层纯净 + 验证层独立。

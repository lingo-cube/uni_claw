# PROJECT_LEADER_ACTION_EXECUTION_CONTRACT_DESIGN_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_ACTION_EXECUTION_CONTRACT_DESIGN — design the Runtime
> Action Execution Contract boundary. **Design only; no code changed.**
>
> **AuthorityDelta: NONE — ArchitectureDelta: ADDITIVE** (Adapter-internal
> execution-parameter layer; no Agent / Planner / GoalEvidence / Semantic
> changes; no cross-layer contract).

---

## 1. Current Boundary

Agent（语义决策）→ DeviceAction（纯净）→ Traversal（执行 + 固定 300ms
post-action timing + bounded settle）→ Translator（直译）→ AdbOperation →
AdbDispatchTarget（adb）。三类责任：Action semantic（Agent/DeviceAction）、
Physical execution（Translator/AdbOperation——timing 隐藏）、Transition
verification（Traversal/Agent settle——已工作）。

## 2. Responsibility Gap

- **Physical execution 参数隐藏**：Swipe 无 duration（adb 默认 300ms）、Tap/
  Back 瞬时、velocity 随 StepFraction 失控（1024→2048px/s）→ 高速 fling →
  运动模糊 → OCR 乱码（EBD 真机证据）。
- Transition verification（settle）与物理参数**无关联**（固定 300ms 与
  duration/velocity 无关）——但引入关联属过度设计，保持解耦。

## 3. Proposed Contract Boundary

DeviceAction（不变）→ **ExecutionContract（Translator 内部：distance /
duration / velocity / coordinates；duration ∝ distance、velocity 上限
~800-1000px/s；Tap/Back 瞬时不变）** → AdbOperation（Swipe 增加 Duration）→
AdbDispatchTarget。

- **谁创建**：DeviceActionTranslator（Agent 不可见）。
- **谁消费**：AdbDispatchTarget；Traversal 不消费（settle 与物理解耦）。
- **Agent 可见**：否（语义层纯净）。
- **持久化**：否（瞬态）。
- **Trace**：可选诊断字段（duration/velocity）——仅排查用，非决策。

## 4. Ownership

ExecutionContract 的 owner = **DeviceActionTranslator（Adapter 层）**；
AdbOperation.Swipe.Duration = Model/Adapter 内部扩展；Transition verification
保持 Traversal/Agent 现状（不耦合物理参数）。

## 5. ArchitectureDelta

**ADDITIVE（Adapter 内部）**——无新状态 owner、无跨层契约、无 Agent/Planner/
GoalEvidence/Semantic 变化；不改 fail-closed、不加场景知识、不改语义动作模型；
Trace 仅加诊断字段。

## 6. Migration Risk

- Translator 重构：低（内部、语义不变、deterministic 单测）。
- Swipe 加 Duration 改变真机行为：中（分步：先 Scroll + 真机乱码率对比；
  Tap/Back 不动；Duration 可选默认 null 兼容）。
- 现有 translator/E2E 测试适配：低。
- Trace 字段扩展：低（纯附加）。

## 7. Recommendation

**分步引入**：第一步仅 Scroll（duration ∝ distance + velocity 上限 + Trace
诊断），Tap/Back 保持瞬时；第二步视 Tap/Back 时序需求评估扩展；不引入
DeviceAction 携带物理参数、跨层 ExecutionContract（Agent 可见）、settle 与
物理耦合——均过度设计。

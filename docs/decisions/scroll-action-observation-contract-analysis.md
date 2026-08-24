# Scroll Action Observation Contract Analysis

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_SCROLL_ACTION_OBSERVATION_CONTRACT_ANALYSIS — analyze
> whether the Scroll Action's motion parameters satisfy the quality
> requirements of downstream Observation / Grounding. **Analysis only — no code
> changed.** Constraints honored: no OCR / Vision / Semantic / ADB-XML / DFS-FSM
> changes.

---

## Human Symptom

高速滑动后的观测帧出现 OCR 乱码（如 "Securitv&nrivacy"），导致 EBD 真机
Root 探索的 source normalization 随机失败（"Source normalization is
unresolved"）。页面停稳确认（scroll stability）已工作，但**模糊帧的文本污染
在截图那一刻已固化**——稳定确认无法消除。滑动越快，失败率越高。

## Current Scroll Behavior (code-verified)

| 层面 | 参数/行为 |
|------|-----------|
| Action 暴露 | `ScrollForward(float StepFraction = 1.0f)` / `ScrollBackward` —— **只有 StepFraction 一个参数**（clamp [0.1, 2.0]）；无 duration / velocity / motion profile |
| distance | `displayHeight × 0.2 × StepFraction`（单侧）→ 全 swipe 距离 = `0.4 × height × fraction`（0.4→307px，0.8→614px @1080×1920） |
| duration | **固定 300ms**（`adb shell input swipe` 未指定 duration → adb 默认） |
| velocity | **隐式派生**：distance / 300ms —— 随 StepFraction 线性增长（0.4→1024px/s，0.8→2048px/s） |
| post-action 等待 | Traversal 固定 `DefaultPostActionSettleDelay = 300ms` 后再取 post-action observation |
| stability 确认 | Agent 侧 bounded re-observe 直到连续两帧签名集相同 + 位置稳定（无固定 sleep，帧间隔 = 采集耗时） |

## Evidence Timeline (EBD real device)

- 步长序列 0.4→0.5→0.6→0.7→0.8（每 scroll 位移 307→614px，速度 1024→2048px/s）。
- 乱码帧（"Securitv&nrivacy"、漏检行、重复检出）集中在**滚动后的帧**，随滚动深度增多。
- Capstone（同步长、短列表、大按钮、滚动少）几乎无乱码——**差异在列表密度与滚动次数**。
- stability CONFIRMED 帧仍含乱码（乱码稳定出现 → 判据放行）→ 归一化断。
- 运动物理：300ms 内 swipe 614px → fling 惯性大 → 截图时页面仍在高速移动 → 运动模糊 → OCR 字符级噪声。

## Answers

**1. Scroll Action 暴露哪些参数？** — 仅 `StepFraction`。distance 由它派生；duration 固定（adb 默认 300ms）；velocity 隐式派生——**Action 层没有 motion profile 表达**。

**2. distance / duration / velocity 关系？** — distance = f(StepFraction)；duration = 常量；**velocity = distance/duration 随步长线性增长**——distance 与 duration **解耦**，这是 motion 失控的核心：步长增大只增加距离、不增加时间 → 速度飙升。

**3. Observation 是否依赖 Scroll motion profile？** — Observation 是静态截图，**不直接消费** motion 参数；但**帧质量事实依赖**：velocity 高 → fling 惯性大 → 截图时运动模糊 → OCR 乱码。stability 确认只等"内容稳定"，不修复已固化的模糊污染——**观察质量对 motion 的依赖无契约、无防护**。

**4. 问题归属？**
- **Vision capability（主）**：OCR 在模糊/密集帧上的检测鲁棒性不足（乱码根因）。
- **Action execution（贡献、可修）**：motion profile 缺失——duration 不随 distance 缩放 → 高速滑动人为加剧模糊；这是执行机制参数问题。
- **Observation acceptance（缺口）**：stability 判据只看内容稳定性，**不看帧质量**（模糊/清晰）——模糊帧被接受为决策依据。

## Ownership

- **Action execution**（Operator/Translator 层）：motion profile（distance/duration/velocity 关系）的显式化。
- **Vision capability**：模糊帧检测鲁棒性（**不在本任务范围，禁止修改**）。
- **Observation acceptance**（Agent 侧）：可选增加帧质量判据（但属 Vision 能力依赖，超出范围）。

## 是否需要 Scroll Motion Contract？

**是——最小 motion contract 值得引入**：`duration 与 distance 成比例`（恒定或上限 velocity），使滑动物理上更温和（更慢、更少 fling 惯性、更清晰截图）。这是 **Action 执行机制参数契约**——不涉及 OCR/Vision/Semantic/ADB-XML/DFS-FSM，不改变 fail-closed、不加场景知识、不是等待 hack（是让运动本身变温和，而非事后等待）。

- 形式：translator 层把 swipe duration 设为 `distance / velocityCap`（如 velocityCap ≈ 800-1000px/s），或 Scroll Action 显式携带 duration。
- 影响面：真机滚动速度变慢（每 scroll 增加 ~300-600ms）——可接受；确定性 world 不受影响（无真实 swipe）。

## 推荐下一步

1. **最小实验（不改生产）**：临时把 swipe duration 与 distance 成比例（velocity 上限 ~1000px/s），EBD 真机连跑多次对比乱码率/归一化失败率——验证 motion 是主因。
2. **若验证成立**：引入 Scroll Motion Contract（duration ∝ distance，velocity 有上限）作为最小生产改动 + 回归（确定性不受影响 + EBD/Capstone 真机复验）。
3. Vision capability 的模糊帧鲁棒性单独上报（本分析范围外）。

## Remaining Risk

- duration 增大 → 滚动更慢 → 探索总时长增加（EBD 真机已 1-2 分钟级）。
- velocity 上限过低可能让长列表探索帧数增加（每 scroll 位移减小）——需权衡。
- OCR 乱码在静止密集帧也可能偶发——motion contract 降低概率、不消除根源（Vision 层负责）。

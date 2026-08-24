# PROJECT_LEADER_SCROLL_ACTION_OBSERVATION_CONTRACT_ANALYSIS_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_SCROLL_ACTION_OBSERVATION_CONTRACT_ANALYSIS — analyze
> whether the Scroll Action's motion parameters satisfy Observation/Grounding
> quality requirements. **Analysis only; no code changed.**
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE** (analysis; the recommended
> motion contract is an Action-layer execution parameter, not a contract or
> ownership change).

---

## 1. Human Symptom

高速滑动后的观测帧出现 OCR 乱码 → EBD 真机 source normalization 随机失败。
Scroll stability 已识别页面停止，但模糊帧的文本污染在截图时已固化，稳定确认
无法消除；滑动越快失败率越高。

## 2. Current Scroll Behavior

- Action 暴露：仅 `StepFraction`（无 duration / velocity / motion profile）。
- distance = 0.4 × height × fraction（0.4→307px，0.8→614px @1920）。
- **duration = 固定 300ms**（adb input swipe 默认）。
- **velocity = distance/300ms 随步长线性增长**（0.4→1024px/s，0.8→2048px/s）——
  distance 与 duration 解耦。
- Traversal 固定 300ms post-action delay；stability 确认 bounded re-observe
  （帧间隔 = 采集耗时）。

## 3. Evidence Timeline

乱码帧集中在滚动后、随滚动深度增多（EBD 长列表 + 0.4→0.8 大步长）；Capstone
（短列表、大按钮、少滚动）几乎无乱码；stability CONFIRMED 帧仍含乱码
（稳定乱码被放行）；300ms 内 swipe 614px → 高速 fling → 运动模糊 → OCR 噪声。

## 4. Ownership

- **Action execution**（Translator/Operator）：motion profile 缺失（可修）。
- **Vision capability**：模糊帧检测鲁棒性（主因，**本任务范围外**）。
- **Observation acceptance**：stability 判据不含帧质量（缺口，依赖 Vision）。

## 5. 是否需要 Scroll Motion Contract

**是**——最小 motion contract：`duration ∝ distance`（velocity 有上限，如
~800-1000px/s），使滑动物理更温和（慢速、少惯性、清晰截图）。Action 层执行
参数契约；不涉及 OCR/Vision/Semantic/ADB-XML/DFS-FSM；不改变 fail-closed；
非等待 hack（让运动本身变温和）。确定性 world 不受影响（无真实 swipe）。

## 6. 推荐下一步

1. **最小实验（不改生产）**：swipe duration 与 distance 成比例（velocity 上限
   ~1000px/s），EBD 真机多次对比乱码率——验证 motion 是否主因。
2. **验证成立** → 引入 Scroll Motion Contract 作为最小生产改动 + 回归
   （确定性不受影响 + EBD/Capstone 真机复验）。
3. Vision 层模糊帧鲁棒性单独上报（范围外）。

## 7. Remaining Risk

- duration 增大 → 探索更慢（EBD 已 1-2 分钟级）。
- velocity 上限过低 → 每 scroll 位移小、帧数增——需权衡。
- motion contract 降低乱码概率、不消除根源（静止密集帧仍可能偶发，Vision 负责）。

# PROJECT_LEADER_PHYSICAL_SETTINGS_TO_WIFI_MULTI_LEVEL_GRADUATION_DECISION

- **Authority**: `PROJECT_LEADER_ARCHIVE_PHYSICAL_SETTINGS_TO_WIFI_MULTI_LEVEL_TRAVERSAL`
- **Date**: 2026-08-14
- **Input**: `INDEPENDENT_ARCHITECTURE_REVIEW`（仓库真相核对，8 项 AUDIT 全 PASS）
- **Mode**: Documentation closure and archive only. No implementation performed.
- **Predecessor**: `docs/decisions/physical-wifi-slice2-graduation-decision.md`（GRADUATED_PHYSICAL_WIFI_MINIMUM_SEMANTIC_LOOP）

---

## 决策：**GRADUATED**

```
SemanticGoal
→ Container A
→ Agent navigation decision
→ fresh Observation
→ Container B
→ same SemanticGoal continuation
→ final SetEnabled chain
→ fresh GoalEvidence
→ Satisfied
```

成立。RealityLevel = **EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP**。

---

## 1. Proven（仓库代码级证据）

| 边 | 证据（源码真相） |
|---|---|
| SemanticGoal 跨容器存活 | `Agent.SemanticRun.cs` `goal` 参数贯穿；导航后 `continue` 回 `:91` 循环，无 `new SemanticGoalInput` 重建 |
| 导航决策在 Agent | `ResolveNavigationPage`（`:298`）/ `ResolveNavigationAnchor`（`:331`）为 Agent 私有方法；Traversal 只执行+验证序列，Container 无导航方法，PageAnalysis 是纯函数 |
| fresh Observation | `Traversal.ExecuteLoweredActionAsync`（`Traversal.cs:107`）强制 `fresh.SequenceNumber > observation.SequenceNumber`；D5 再以 `ProvesNavigationTransition`（`Agent.SemanticRun.cs:378`）证明页面变更 ∧ `!IsStillMine` |
| Container B 只从 fresh 观测派生 | `CreateContainer(nextPage)` + `Bind(navigationObs)`（`:188-190`）；`Container.Bind`（`Container.cs:162`）重置绑定/局部进度/状态信念 |
| 最终 SetEnabled 链复用 | 导航分支不触碰 SELECT/AUTHORIZE/LOWER/GoalEvidence 任何一行（`:200-245` 毕业路径零改动） |
| fresh GoalEvidence → Satisfied | `CompleteSemantic`（`:385`）+ `GoalEvidence(SourceObservationSequence = fresh seq)` |

## 2. Explicitly NOT proven（非阻断，不转为实施任务）

- real-device timing（真机转场/视觉时序）
- arbitrary depth（任意深度遍历）
- scroll traversal（滚动遍历）
- popup handling（弹窗处理）
- cross-app traversal（跨应用遍历）
- general route planning（通用路线规划）

以上均为 Non-Goals 边界，记录为未来场景压力，不构成本次毕业缺口。

## 3. Documentation Drift — **RESOLVED**

导航识别词汇与容器身份词汇是**有意区分的两套词汇**（详见 design.md §D6 补充段）：

- **Navigation Recognition Criteria（导航识别）**：正锚（positive page anchors），用于"当前可见页面/候选是什么"——下一跳识别。
- **Container Identity Criteria（容器身份）**：正锚 + negative 锚，用于"这个观测是否仍属于本容器"——页面/容器身份消歧。

两者都不编码"应执行什么有序路线"。设计文档已对齐仓库真相，无代码改动。

## 4. Remaining Risks（非阻断）

1. **emulator-only timing calibration** — settle 常量 `4×500ms` 是 swiftshader 校准值，真机转场时长可能不同。
2. **recognition-anchor drift** — 每页 anchor/negative 消歧是 caller-injected；真机页面文本漂移会导致 0/多候选 fail closed（安全，但影响可达性）。
3. **bounded navigation settle window** — 页面转场慢于 2s 时可能误判失败；有界重观测的固有权衡。
4. **real-device perception variance** — 真机视觉对共享标题文本的检测确定性未验证。

## 5. OpenSpec / Archive

- `openspec validate physical-settings-to-wifi-multi-level-traversal`：**PASS**。
- `scripts/check-consistency.sh`：**ALL PASS**。
- tasks.md：5.1 / 5.2 / 5.3 全部 `[x]`。
- 已归档：`openspec/changes/archive/physical-settings-to-wifi-multi-level-traversal/`（仓库惯例：无日期前缀直移，同 `switch-state-reading` / `physical-wifi-off-to-on-minimum-semantic-loop` 先例）。
- RealityLevel：**EMULATOR_REALITY_MULTI_LEVEL_SEMANTIC_LOOP**（不称 REAL_DEVICE_PROVEN，§33 emulator-only）。

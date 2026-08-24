# PROJECT_LEADER_AI_CODING_DEVELOPMENT_PROTOCOL_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_AI_CODING_DEVELOPMENT_PROTOCOL_INTEGRATION — integrate
> the Evidence-driven Workflow into the project-level AI Coding development
> protocol so Worker Agents follow it by default.
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE.** Zero Runtime production
> code touched; no Architecture ownership changed; no new execution capability
> added. STOP conditions not triggered.

---

## 1. Protocol Structure

`.ai/development-protocol.md` 已存在（v1.0，16 节）——采用**追加集成**而非重写，
新增 **Section 17 "Evidence-Driven AI Coding Workflow"**，形成统一规则链：

```
Task Classification (17.1 L0-L4)
→ Evidence Requirement (per level, skill 定义 E0-E4)
→ Execution Rules (17.2 七步 Worker 流)
→ Validation Rules (17.4 能力测试 + 机械检查)
→ Review Rules (17.5 change-review 四象限)
```

同时更新 Document Map（§16）登记：`evidence-driven-debugging` skill、
`runtime-behavior-debugging` skill、`.ai/reviews/change-review.md`。

## 2. Task Classification

| 等级 | 任务 | 要求 |
|------|------|------|
| L0 | 文档、格式、简单修改 | 无需 Evidence workflow（明确豁免，避免强制所有简单任务走流程） |
| L1 | 普通代码修改 | 明确目标、影响范围、测试 |
| L2 | 模块行为修改（状态、异步、数据流） | E1-E2 evidence |
| L3 | Runtime/Architecture 修改（Agent、FSM、Traversal、Semantic、Recovery、Lifecycle） | E3 evidence + AuthorityDelta + ArchitectureDelta |
| L4 | 系统集成修改（Real Device、E2E、Flaky、Environment） | E4 evidence |

## 3. Worker Workflow

L2-L4 任务默认执行 7 步：Identify scope → Identify evidence → Identify owner
→ Design minimal change → Implement → Validate invariant → Regression。
Failure 分类（Discovery/Grounding/Authorization/Execution/Recovery/
Environment）必须先证明；禁止归因捷径（"Child missing ⇒ DFS bug"、
"Test fail ⇒ Production bug"）。
输出格式统一为 `PROJECT_LEADER_<TASK>_RESULT`，必须含：Decision /
AuthorityDelta / ArchitectureDelta / Evidence used / Change summary /
Validation result / Remaining risk。

## 4. Review Integration

- 新建 `.ai/reviews/change-review.md`（通用四象限：Authority / Evidence /
  Boundary / Testing），支持三类变更：
  - **Runtime change**（E3/E4 证据要求、AuthorityDelta/ArchitectureDelta）
  - **Test change**（能力模型检查）
  - **Architecture change**（STOP：须走 Architecture/Human Gate，评审清单
    不能授权）
- `.ai/reviews/runtime-change-review.md` 改为 superseded 指针（保留向后引用，
  新评审一律用 change-review.md）；两 skill 的评审引用已更新到 change-review.md。

## 5. Skill Decision

**保留两个 Skill（Option A）——不合并。** 理由（按任务原则"不要为了统一而
合并；职责不同则保留"）：

| skill | 职责 |
|-------|------|
| `evidence-driven-debugging` | **通用方法论**：E0-E4 证据分级、L0-L4 任务风险分类、Worker 流、Test Design、评审集成——适用于仓库内任意代码任务 |
| `runtime-behavior-debugging` | **Runtime 专属应用**：Runtime 失败分类（A-F）、Agent/FSM/Traversal/真机 seam、E4 非确定失败处理 |

职责边界不同（方法论 vs 领域应用），合并会丢失各自的针对性。已互引
（各 skill 头部注明 relationship + 统一规则所在 `development-protocol.md §17`）。

## 6. Migration Impact

| 影响面 | 说明 |
|--------|------|
| 生产代码 | 零改动（AuthorityDelta/ArchitectureDelta NONE） |
| 既有 16 节协议 | 原样保留，仅追加 §17 与 §16 Document Map 登记 |
| Skill 生态 | 5 个既有 skill 不受影响；两个证据驱动 skill 职责显式化、互引 |
| 评审流程 | runtime-change-review.md 保留为指针；新评审入口 change-review.md |
| 机械检查 | `check-consistency.sh` ALL PASS；`git diff --check` clean |
| 后续建议 | ① 将 §17 纳入 CI/PR 完成检查（当前为文档规则）；② L0 豁免范围定期复核防漂移；③ 两个 skill 若长期稳定可合并归档，但当前职责不同不合并 |

---

## 本任务改动清单

| 文件 | 内容 |
|------|------|
| `.ai/development-protocol.md` | 追加 Section 17（统一规则链）+ Document Map 登记 |
| `.ai/reviews/change-review.md` | 新建：通用变更评审（Runtime/Test/Architecture 三类） |
| `.ai/reviews/runtime-change-review.md` | 改为 superseded 指针（向后兼容） |
| `.ai/skills/evidence-driven-debugging/SKILL.md` | 评审引用更新为 change-review.md |
| `.ai/skills/runtime-behavior-debugging/SKILL.md` | 补 relationship 说明（职责分工 + 协议/评审入口） |

**生产代码变更：零。** STOP 条件未触发。

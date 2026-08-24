# PROJECT_LEADER_AI_CODING_EVIDENCE_DRIVEN_WORKFLOW_RESULT

> Date: 2026-08-23
> Role: Implementation Worker (DeepSeek-V4-Flash)
> Task: PROJECT_LEADER_AI_CODING_EVIDENCE_DRIVEN_WORKFLOW_DESIGN — codify the
> UniClaw Runtime's validated engineering experience into an AI Coding workflow
> (Evidence → Diagnosis → Ownership → Minimal Change → Validation).
>
> **AuthorityDelta: NONE — ArchitectureDelta: NONE.** This task touched zero
> Runtime production logic, changed no Agent/FSM/Traversal/GoalEvidence
> ownership, and added no architecture capability. Deliverables are process
> artifacts only (skill + review checklist + this result).

---

## 1. 原则提炼

从已验证工程经验（Capstone revisit-coverage 排查与修复：E4 证据 → 类型 E /
根因 C 分类 → Agent 归属 → Option A+B 最小修改 → 1957/1958 回归）提炼为五步：

```
Evidence → Diagnosis → Ownership → Minimal Change → Validation
```

三条硬性禁止：
- 无证据猜测 Runtime 行为（禁止 "Child missing ⇒ DFS bug" 式归因捷径）
- 根据失败现象直接修改代码（先分类、先证明）
- 通过弱化测试绕过真实问题（测试必须验证能力，不验证脚本）

## 2. Evidence Level

按任务风险选择证据等级（不强制所有任务用最高级）：

| 等级 | 适用 | 必须证据 |
|------|------|----------|
| E0 | 编译/格式/简单修改 | compiler error/message |
| E1 | 单元测试/局部组件 | stack trace、assertion、input |
| E2 | 状态组件/异步流程 | state snapshot、execution history、action/result sequence |
| E3 | Runtime/Agent/FSM/Traversal/Lifecycle | trace + state transition + observation + decision record |
| E4 | 真机/集成/非确定失败 | trace timeline + observation frames + environment state + action history + reproduction context |

## 3. 适用范围

**必须 Evidence-first**：Agent loop、FSM、Traversal、Runtime behavior、
Recovery、Async workflow、Real device、Flaky integration。

**不强制**：文档、格式、简单 rename、DTO 机械修改。

## 4. Worker Rules

复杂任务执行流（7 步）：理解架构边界 → 收集已有 Evidence → 分类 Failure
Type → 判断 Owner → 提出最小修改 → 验证 Invariant → 执行 Regression。

Failure 分类：Discovery / Grounding / Authorization / Execution / Recovery /
Environment——**必须先证明**。Runtime 核心修改必须输出
`AuthorityDelta: NONE|CHANGED` 与 `ArchitectureDelta: NONE|ADDITIVE|BREAKING`，
并说明对 Agent authority / FSM / Traversal / GoalEvidence / 场景知识的影响。

## 5. Skill

创建 `.ai/skills/evidence-driven-debugging/SKILL.md`（已注册 DSH 发现，
catalog 可见；`setup-dsh-skills.sh` 校验全部 SKILL.md 可达）。

- 触发：Runtime failure / Agent behavior / FSM / Traversal / Async /
  Real device / Flaky test
- 流程：Failure → Evidence Check → Evidence Collection → Failure
  Classification → Owner Analysis → Minimal Fix
- 与上一任务的 `.ai/skills/runtime-behavior-debugging` 互补：前者是通用
  方法论，后者是 Runtime 专属应用（同一 E0-E4 分级与失败分类，互引）。

## 6. Review Checklist

创建 `.ai/reviews/runtime-change-review.md`（新增 `.ai/reviews/` 目录，
接续 `.ai/skills/` 的既有文档结构）。

四象限检查（每项带 checkbox）：
- **Authority**：是否新增执行权限？是否绕过 Agent/FSM/GoalEvidence？
  是否引入新决策 owner？是否显式声明 AuthorityDelta/ArchitectureDelta？
- **Knowledge Boundary**：是否引入场景知识？是否把 Fixture 变生产逻辑？
  语义是否 scenario-neutral？
- **Evidence**：是否基于收集到的证据（按 E0-E4 等级）？是否存在隐藏假设
  （设备行为/OCR 稳定性/swipe 物理）并显式声明其证据？失败是否先分类后修改？
- **Testing**：是否验证能力（coverage/authorization/consistency/
  fail-closed/evidence sufficiency）？是否含固定点击数/固定 ActionHistory/
  固定页面路径/固定坐标/固定 UI 文案？是否用 EvidenceFixture +
  ExpectedSpecification + Runtime Execution + Evidence Evaluation？

结论：APPROVE / APPROVE-WITH-NOTES / REJECT（Authority 违规、场景知识、
脚本化测试 = blocking）。

## 7. 后续迁移建议

1. **接入开发协议**：将五步流程与四象限检查清单引用进
   `.ai/development-protocol.md`（Authority Order / 两车道模型旁），使
   Codex + Claude 共享，避免每助手各维护一份。
2. **评审门禁**：在 CI/PR 流程或 agent 完成检查中挂接
   `.ai/reviews/runtime-change-review.md`（当前为人工/代理自检表单，未自动化）。
3. **Skill 收敛**：`evidence-driven-debugging` 与 `runtime-behavior-debugging`
   内容高度互补，若后续被采纳为常驻流程，可合并为单一 skill 并归档另一份
   （或保留双入口、统一互引，避免漂移）。
4. **经验回填**：将 Capstone 排查（E4 证据链：trace + 帧时间线 + coverage
   ledger + 终端原因 → 类型 E / 根因 C → Option A+B）作为
   `evidence-driven-debugging` 的 canonical example 章节回填，让后续 AI
   看到完整证据→修复链。
5. **文档一致性**：`check-consistency.sh` ALL PASS（本任务零生产改动，
   宪章/Contract/导航未受影响）；后续若把工作流并入宪章或协议文档，需同步
   该脚本的检查项。

---

## 本任务改动清单

| 文件 | 内容 |
|------|------|
| `.ai/skills/evidence-driven-debugging/SKILL.md` | 新建：Evidence Level E0-E4 / 适用范围 / Worker 7 步流 / Failure 分类 / Runtime 修改检查（AuthorityDelta/ArchitectureDelta）/ Test Design / STOP |
| `.dsh/skills/evidence-driven-debugging` | 符号链接（DSH 发现注册） |
| `.ai/reviews/runtime-change-review.md` | 新建：四象限评审清单（Authority / Knowledge Boundary / Evidence / Testing）+ Verdict |

**生产代码变更：零。** STOP 条件未触发（无需修改 Runtime 架构 / Authority /
Agent/FSM/Traversal ownership）。

---
name: tdd
description: 默认实现纪律。Behavior → 识别 public seam → RED → 最小实现 → GREEN → Refactor。适用于所有实现类 WorkItem；Bugfix 走回归 RED 变体。
---

# tdd

## Trigger

- 任何实现类 WorkItem 的默认纪律（无需显式要求）。
- Bugfix 场景使用下方回归变体（配合 diagnosing-bugs 的产出）。

## Inputs

- WorkItem：objective / acceptance / forbidden / anchors。
- 已识别的 public seam（来自 codebase-design 或既有结构）。

## Procedure

### 标准循环

1. **Behavior**：用一句话陈述可观察行为（不是实现细节）。
2. **Identify public seam**：行为在哪个公共接缝上可观察/可测试。
3. **RED**：写失败测试；运行并确认它**确实失败、且失败原因正确**
   （不是拼写错误或环境问题）。
4. **Minimum implementation**：只写让该测试通过的最少代码。
5. **GREEN**：运行测试通过；不在此步加无关功能。
6. **Refactor**：在保持 GREEN 的前提下整理代码；行为不变。

### Bugfix 变体

```text
Minimal Reproduction → Regression RED → Fix → GREEN → 原场景复验
```

（复现与归因由 diagnosing-bugs 完成；本 Skill 只负责回归保护下的修复。）

## Verification

- RED 证据留存（失败输出/命令记录）→ 进 `evidence/`。
- GREEN 后相关套件全绿；无被跳过/删除的测试。
- 没有为通过测试而弱化断言、放宽 fixture 或偷改 acceptance。
- 测试验证 behavior：不固定调用次数、页面路径、坐标、UI 文案。

## Failure / Escalation

- 无法写出 RED（行为不可观察）→ 先补可观测性或测试缝；仍不行则升级，
  不得直接写实现。
- 测试必须触碰私有细节才能通过 → public seam 选错了，回到步骤 2。
- 最小正确实现超出 WorkItem scope → 返回 UniFlow（scope 越界）。

## Constraints

- 测试是行为规约，不是实现镜像；重构不改测试意图。
- 不创建第二套 task system；不决定 Model Routing。

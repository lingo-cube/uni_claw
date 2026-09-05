---
name: diagnosing-bugs
description: 唯一通用 debugging 主入口。Bug → 可靠 RED → 最小复现 → 可证伪假设 → 判别性证据 → First Divergence Point → Owner → Root Cause → 回归 RED → 最小修复 → GREEN → 原场景复验。
---

# diagnosing-bugs

## Trigger

- 任何 bug、失败、回归、flaky、非确定行为的调查——**唯一入口**，不存在平级
  的第二 debugging Skill。
- 实现类 WorkItem 执行中遇到的未知原因失败。

## Inputs

- 失败现象（症状 ≠ 诊断）。
- 可用的证据材料：日志、trace、测试输出、观测帧、环境状态。

## Procedure（完整管线）

```text
Bug
→ Reliable RED                 先获得稳定可复现的失败
→ Minimal Reproduction         最小化到仍保留问题的最小用例
→ Falsifiable Hypotheses       列出可被证据推翻的候选假设
→ Collect Discriminating Evidence  只收集能区分假设的证据
→ FDP (First Divergence Point) 定位期望与现实首次分歧的位置
→ Owner                        确认该分歧属于哪个 seam 的责任
→ Root Cause                   解释全部已知证据（不只是触发路径）
→ Regression RED               把根因固化为回归测试
→ Minimal Fix                  最小修复（不动无关代码）
→ GREEN                        回归测试转绿
→ Original Scenario Verification  原始场景复验通过
```

### 证据等级（按需升级，V1 保留语义）

| 级别 | 证据 |
|---|---|
| E0 | 编译器/解释器/工具消息 |
| E1 | 定向日志 + 针对性复现 |
| E2 | 隔离测试级证据（最小用例内） |
| E3 | trace 时间线 + 观测帧 + 动作历史 |
| E4 | E3 + 环境状态 + 复现上下文（真机/端到端/flaky） |

### 归因纪律

先证明，不猜测。禁止归因捷径：「子元素缺失 ⇒ 遍历 bug」「测试失败 ⇒ 生产
bug」「症状在 A ⇒ 原因在 A」。失败先分类：fixture / test / runtime / 环境。

## 强制规则（不可绕过）

```text
No reliable RED               → No fix
No falsifiable hypothesis     → Continue diagnosis
No sufficient evidence        → Continue diagnosis
No FDP / Owner                → No implementation WorkItem
No RED → GREEN regression     → Not proven fixed
```

## Verification

- 修复前存在可靠 RED；修复后 RED→GREEN 转变有记录（进 `evidence/`）。
- 根因解释全部已知证据；无法解释的证据说明根因不完整。
- 最小 diff：无趁机重构、无预防性改动。
- 原始场景复验通过（不是只过了新回归测试）。

## Failure / Escalation

- 证据不可得（环境/权限限制）→ 显式记录能力限制，升级；不得以推测代替证据。
- FDP 指向其他 owner/seam → 路由到 owning seam 的新 WorkItem，不越界修。
- 同一修复两次尝试仍不能 GREEN → 停止，回到假设列表重新诊断。

## Constraints

- 本 Skill 是诊断与修复方法；阶段推进（是否进入实现）由 UniFlow 决定。
- 特定领域失败分类学（如 UniClaw Runtime 的
  Discovery/Grounding/Authorization/Execution/Recovery/Environment 六类）作为
  本 Skill 的 domain 附录使用，不另立入口。
- 不创建第二套 task system；不决定 Model Routing。

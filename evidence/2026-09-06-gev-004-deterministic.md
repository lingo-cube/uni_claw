# Evidence — GEV-004 DETERMINISTIC verification

- Date: 2026-09-06
- Change: `changes/GEV-004/state.md`
- Command: `dotnet test UniClaw.Kernel.slnx`（.NET SDK 10.0.400；net10.0）
- Level: DETERMINISTIC（纯内存；hand-crafted envelope + scripted kernel
  替身，零 IO/设备/时钟依赖）

## Result

```text
已通过! - 失败: 0，通过: 36，已跳过: 0，总计: 36  [UniClaw.Kernel.Tests.dll]
已通过! - 失败: 0，通过: 17，已跳过: 0，总计: 17  [UniClaw.Agent.Tests.dll]
合计 53/53 GREEN [dotnet test, 2026-09-06]
```

36 = E2B-001 8（零改动）+ C2E-002 10（零改动）+ OUT-003 18（零改动）；
17 = GEV-004 GoalEval1..GoalEval17。no-drift 双面实证：

```text
git diff --stat src/UniClaw.Kernel          → EMPTY（byte-level 零改动）
git status tests/UniClaw.Kernel.Tests       → EMPTY（既有 36 用例零改动）
```

构建：Agent assembly `dotnet build` 零 error / 零 CS 警告（全部公共成员
带 XML 文档）。环境级 NU1900 NuGet 漏洞库缓存不可写，与 E2B/C2E/OUT
记录一致，与代码无关。

## TDD 过程（失败尝试是证据，保留）

- RED（桩 + 测试先行）：Agent.Tests 失败 13 / 通过 4。13 个行为用例
  （GoalEval1-8/12-15/17）全部因 `NotImplementedException` 或缺校验失败
  ——关键监督场景可见失败。4 个通过项为结构断言（GoalEval9/10/11/16），
  其中 GoalEval10 首跑失败暴露测试自身 walk 缺陷（嵌套 criterion 子类型
  未枚举），修复 walk 后 RED 定格 13/4——该测试自此真正行使结构检查。
- GREEN（最小实现）：Agent.Tests 失败 1 / 通过 16。GoalEval13 暴露
  record 默认相等对 `IReadOnlyList` 属性按引用比较——实现本身确定性成立，
  改为逐字段 + xUnit 集合断言（CriterionResults 序列比较）。同时补齐
  显式 ctor record 的公共成员 XML 文档（CS1591 清零）。
- REVIEW 修复后：失败 0 / 通过 53（Kernel 36 + Agent 17）。

## REVIEW（fresh SubAgent，六轴）与修复

独立评审结论：**APPROVE**（六轴全 PASS；无 authority 违规、无 Kernel
漂移、无 scope creep）。复核实跑 53/53 GREEN。发现与处置：

| # | 级别 | 发现 | 处置 |
|---|---|---|---|
| F1 | minor | `PrimaryGoal` 直接保存调用方 `IReadOnlyList` 引用——调用方持可变 List 构造后突变，会在 EvaluationId 不变前提下产出不同记录，击穿幂等（验收 11） | 已修：ctor 内 `Array.AsReadOnly(required.ToArray())` 防御性拷贝（Preferred 同理），幂等无别名逃逸 |
| F2 | nit | GoalEval15 的 `Assert.Same(envelope, envelope)` 恒真 | 已修：删除；值不变断言与「记录不含 envelope 对象」属性扫描保留 |
| F3 | nit | GoalEval9 注释称「全程序集」实扫 public 方法 | 已修：注释改为「唯一公共产出路径」（OUT-003 Outcome16 先例同法；grep 全程序集 `new GoalEvaluation(` 仅 UniAgent.Evaluate 一处，事实无第二产出） |

轴级结论摘要：轴1 第二 Goal Evaluation Authority——PASS（Agent 唯一
构造点 `UniAgent.Evaluate`；Kernel 零 `GoalEvaluation` 命中）；轴2
criterion 吃 envelope 结构——PASS（形状知识全收敛于 private
`UniAgent.Judge`；criterion 图仅 `TerminalClassification`）；轴3 评价
回写——PASS（零赋值模式；记录只持 refs）；轴4 镜像映射——PASS
（GoalEval5/6/7 三例证伪 Completion↔Satisfied 镜像）；轴5 幂等——
PASS（F1 加固后无死角）；轴6 Kernel 反向引用——PASS（csproj/metadata
双面 + slnx 方向断言）。

## 验收 ↔ 反例 ↔ 用例映射（实跑核销）

| 验收 | 反例 | 用例 | 结果 |
|---|---|---|---|
| ① 唯一 Authority | —— | GoalEval9 | GREEN |
| ② 输入仅 Goal+Outcome+Context | D | GoalEval11 | GREEN |
| ③ 对 envelope 只读 | F | GoalEval15 | GREEN |
| ④ Kernel 零改动 + 单向引用 | G | GoalEval16 + git diff | GREEN/EMPTY |
| ⑤ criterion 语义身份 | A | GoalEval10 | GREEN |
| ⑥ vacuous guard | B | GoalEval12 | GREEN |
| ⑦ 缺席=Unverifiable≠Unmet | C | GoalEval4（对照 3） | GREEN |
| ⑧ 判定格四档穷举 | —— | GoalEval1/2/3/4 | GREEN |
| ⑨ 反镜像双例 | E | GoalEval5/6 | GREEN |
| ⑩ policy 四组合 + 结构只锁 U⇒NF | I | GoalEval1/2/3/4（U⇒NF 构造拒绝断言在 4） | GREEN |
| ⑪ 幂等 | H | GoalEval13 | GREEN |
| ⑫ 无 envelope 无评价 | J | GoalEval14 | GREEN |
| 附加 SafeStop/Escalation 可评价 | —— | GoalEval7/8 | GREEN |
| 附加 真实 emission 消费 | —— | GoalEval17（UniKernel→terminal→agent） | GREEN |

## 结论

GEV-004 验收 12 条逐条 GREEN；反例 A-J 全覆盖；E2B/C2E/OUT 36 用例
零改动保持 GREEN；`src/UniClaw.Kernel` byte-level diff empty（ADR-0008
单向依赖由 csproj + metadata + slnx 三面断言）；幂等、反镜像、fail-closed
均有确定性证据。UniAgent 成为 Runtime Outcome 的第一个消费者，L0 监督弧
（§1 第四能力、不变量 40/41 的 UniAgent 侧）闭合。

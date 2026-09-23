# Programmer-style 工作汇报规则

> 性质：所有者指令（2026-09-23 起，对所有工作生效）。
> 适用面：agent 向人交付的一切汇报——Gate 汇报、评审处置汇报、完成
> 汇报、阶段 handoff。仓库持久记录（`changes/<id>/state.md` 的 status
> log、spec 处置表、evidence）继续以 ID 索引组织，不受本规则约束；
> 编号在汇报里降级为括号引用。

不要以 Gate、Acceptance 编号、评审编号作为汇报主线。

这些编号可以作为引用，但主线必须按照一个正常程序员理解代码和修改代码的方式组织。

---

## 1. 先说我在改什么

用 1-2 句话说明具体代码问题。

例如：

> RunModel 接收 ExecutionContract 时没有把预算字段带进 View，所以用户声明的 64 次咨询最后变成默认 16 次。
>
> 同时当前幂等判断只比较 Version，因此同版本但预算不同的合同也会被错误当成同一个合同。

不要写：

> Gate 2 处理 F5 / D8。

编号放括号里即可：

> （对应 F5 / D8）

---

## 2. 再说我为什么确定这是问题

说明实际观察到的代码行为。

优先使用：

- 调用链
- 输入 → 输出
- 测试结果
- 实际状态变化

例如：

```text
Contract(MaxConsultations=64)
    ↓ AdmitContract
View(MaxConsultations=16)
```

以及：

```text
Contract A: version=1, budget=16
Contract B: version=1, budget=64

当前结果：
B 被当成 A 的幂等重复

期望：
B 被识别为合同冲突
```

这比写一串规范编号更重要。

---

## 3. 说明准备怎么改

像 code review description 一样描述修改面。

例如：

> 我准备做三件事：
>
> 1. AdmitContract 创建 View 时带入两个预算字段；
> 2. 幂等判断从 Version 比较改成 Contract Signature 比较；
> 3. RunId 的 canonical input 加入最终解析后的预算。
>
> 不改 KernelRunDriver，不碰 Defer 状态机。

这里一定要说明 **不改什么**。

---

## 4. 改完以后说实际改了什么

不要重复计划。

按照真实 diff 汇报：

```text
RunModel.cs
- AdmitContract 现在保留 MaxConsultations / MaxTotalSteps
- 新增 contract signature 比较
- 同 version 异 signature 返回 conflict

RunIdentity.cs
- canonical form 加入 resolved budgets
```

如果实际实现与原计划不同，要直接说：

> 原计划准备修改 X，但追代码后发现职责实际上在 Y，因此最终没有改 X。

这类信息很有价值。

---

## 5. 用测试说明结果，而不是只说“测试通过”

人类程序员更关心：

> 我修的那个 bug 真的消失了吗？

所以优先汇报行为：

> 修复前：
>
> `64 → 16`
>
> 修复后：
>
> `64 → 64`

以及：

> 修复前：同 version 异预算 → idempotent
> 修复后：同 version 异预算 → contract conflict

然后再补：

```text
相关新测试：2/2 passed
原有测试：17/17 passed
其他已知 RED：保持不变
```

---

## 6. 新发现单独说

如果改代码过程中发现新问题：

不要立即顺手修。

写：

> 顺着这条调用链我还发现一个问题：
>
> XXX。
>
> 它不影响本次修复，所以没有修改。
> 建议放到下一步处理。

如果新问题会让当前方案无效，则写：

> 这个发现改变了原来的实现假设，我建议先停，不继续当前修改。

---

## 7. 最后只说下一步工程动作

不要写：

> 建议进入 Gate 3。

写成：

> 下一步应该修 Defer 流程。
>
> 目前第一个合法 Defer 就会被误判为嵌套 Defer，所以应先修这个判断；确认第一次 Defer 能正常进入第二轮咨询后，再实现 MaxRounds 和 exhaustion。

括号里可以补：

> （流程编号：Gate 3）

流程编号是索引，不是叙事主体。

---

# 每次完成工作的推荐输出格式

## 做了什么

一句话说明本次代码目标。

## 原来的问题

用代码路径或输入输出说明 bug。

## 这次修改

说明真实 diff 和边界。

## 验证结果

说明关键行为修复前后变化 + 测试结果。

## 新发现

没有就写“无”。

## 下一步

说明下一段代码应该处理什么，以及为什么。

---

# 风格要求

像一个 senior engineer 给另一个 engineer 做 handoff。

优先说：

- 数据怎么流
- 状态怎么变
- 哪个职责属于哪里
- 哪个条件判断错了
- 哪条调用链断了
- 修改影响哪些模块
- 为什么选择这个实现
- 哪些东西刻意没动

少说：

- 大量流程编号
- 大量评审编号
- 管理术语
- “已闭环”“完成处置”“验收承载”等抽象词

这些词可以作为补充 metadata，但不能代替工程解释。

---

# 最重要的一条

报告必须让我看完以后能回答：

> 如果我现在打开 IDE，我应该去看哪几个文件、哪条调用链、原来哪里错、现在怎么改、下一步准备碰哪里？

如果做不到，这份汇报就不合格。

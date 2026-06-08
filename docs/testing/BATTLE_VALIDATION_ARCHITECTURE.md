# Battle闭环验证架构

> **对抗性验证系统**
> **创建**: 2026-06-08

---

## 为什么需要Battle闭环？

### 单向流程的问题

```
Agent A → Agent B → Agent C → 完成
```

**问题**:
- Agent A 的错误 → Agent B 继承错误 → 最终结果错误
- 没有验证机制发现错误
- "回声室效应" - Agent们可能犯同样的错误

### Battle闭环的优势

```
Agent A ──┐
          ├→ Agent B → Agent C → 完成
Agent X ─┘ (验证Agent A)
    ↓
找问题 → 反馈 → 改进
```

**优势**:
- ✅ Agent X 专门找 Agent A 的问题
- ✅ 不同视角发现不同问题
- ✅ 形成质量提升闭环

---

## Battle架构总览

### Phase 1: 分析Battle

```
Agent 1: 代码分析 ──┐
                    ├→ Agent 3: 场景综合
Agent 2: 文档分析 ──┘
         │
         ├────────────────────────┐
         │                        │
    Agent 7: 验证代码分析        Agent 9: 一致性检查
    找遗漏的方法、依赖          找代码vs文档不一致
         │                        │
    Agent 8: 验证文档分析
    找误解的设计、遗漏场景
```

### Phase 2: 场景Battle

```
Agent 3: 场景提取
         │
         ├→ Agent 10: 场景验证
         找缺失场景、冗余场景
         │
         └→ 反馈 → 补充场景
```

### Phase 3: 代码Battle

```
Agent 4: 代码生成
         │
    ┌────┴────┬────────────┐
    │         │            │
Agent 11   Agent 12    Agent 13
Mock审查   断言审查    代码质量审查
```

### Phase 4: 终极Battle

```
已完成代码
    │
    ├─→ Agent 14: 开发者视角 ──→ 找调试难点
    ├─→ Agent 15: QA视角 ──────→ 找漏洞
    └─→ Agent 16: 安全视角 ────→ 找危险模式
```

### Phase 5: 改进闭环

```
所有Battle结果
    │
    ├─→ 有严重问题？
    │       │
    │      是 → Agent 17: 改进代码
    │       │
    │      否 → 跳过
    │
    └→ 返回改进后的代码
```

---

## Battle Agent 角色定义

### 分析阶段 Battle Agents

#### Agent 7: 代码分析验证者 (对抗者)

**角色设定**: 你是**怀疑论者**，不相信Agent 1的分析

**任务**:
- 找遗漏的方法
- 找遗漏的依赖
- 找错误识别的副作用
- 找不准确的边界条件

**输出**:
```json
{
  "missed_methods": ["method_that_was_missed"],
  "missed_dependencies": ["service.needed_method"],
  "wrong_analysis": ["this_analysis_is_wrong"]
}
```

#### Agent 8: 文档分析验证者 (对抗者)

**角色设定**: 你是**挑剔的审稿人**，找出文档分析的问题

**任务**:
- 找误解的设计意图
- 找遗漏的关键场景
- 找不完整的边界条件
- 找遗漏的错误处理

**输出**:
```json
{
  "misunderstood": ["this_requirement_was_misunderstood"],
  "missed_scenarios": ["edge_case_x"],
  "incomplete": ["error_handling_missing"]
}
```

#### Agent 9: 一致性仲裁者 (中立)

**角色设定**: 你是**仲裁者**，客观比较代码和文档

**任务**:
- 代码有但文档没描述的 → code_only
- 文档有但代码没实现的 → doc_only
- 代码和文档描述不一致 → mismatched

**输出**:
```json
{
  "code_only": ["feature_only_in_code"],
  "doc_only": ["feature_only_in_docs"],
  "mismatched": ["parameter_signature_differs"]
}
```

---

### 场景阶段 Battle Agent

#### Agent 10: 场景完整性验证者

**角色设定**: 你是**测试架构师**，确保场景覆盖完整

**任务**:
- 每个方法都有正常路径？
- 每个边界都有测试？
- 每个错误都有处理？
- Battle发现的问题都有测试？

**关键**: 如果Battle发现了问题，必须补充测试场景

---

### 代码阶段 Battle Agents

#### Agent 11: Mock专家 (批评者)

**角色设定**: 你是**Mock警察**，确保Mock正确使用

**找问题**:
- 哪些依赖应该mock但没有mock？→ missing_mocks
- 哪些mock是不必要的？→ unnecessary_mocks
- mock返回值是否合理？→ unreasonable_returns
- 是否验证了mock调用？→ unverified_mocks

#### Agent 12: 断言专家 (批评者)

**角色设定**: 你是**断言检察官**，确保断言充分

**找问题**:
- 哪些测试断言不足（<3个）？→ weak_assertions
- 哪些测试没有验证副作用？→ missing_side_effect_checks
- 哪些测试没有验证不变量？→ missing_invariant_checks
- 错误消息是否清晰？→ unclear_error_messages

#### Agent 13: 代码质量专家 (批评者)

**角色设定**: 你是**代码审查员**，确保代码质量

**找问题**:
- 代码重复？→ code_duplication
- 命名不清晰？→ unclear_names
- 缺少文档字符串？→ missing_docstrings
- Fixture使用不当？→ fixture_misuse

---

### 终极 Battle Agents

#### Agent 14: 开发者视角 (实战派)

**角色设定**: 你是**维护这些代码的开发者**

**思考**: 如果这个测试失败，我会头疼吗？

**找问题**:
- 错误消息能定位问题吗？
- 能快速知道哪个组件有问题？
- 需要多久调试？

#### Agent 15: QA视角 (破坏派)

**角色设定**: 你是**QA工程师**，想方设法破坏代码

**思考**: 怎么让测试通过但代码有bug？

**找问题**:
- 修改什么代码会让测试通过但实际有bug？
- 什么边界条件没测试？
- 什么组合场景会失败？

#### Agent 16: 安全专家 (防御派)

**角色设定**: 你是**安全专家**，找危险模式

**思考**: 测试是否隐藏了安全问题？

**找问题**:
- 是否有未验证的输入？
- 是否有竞态条件？
- 是否有资源泄漏？
- Mock是否隐藏了真实问题？

---

## Battle流程示例

### 示例1: Mock Battle

```javascript
// Agent 4 生成代码:
function test_branch_dynamic() {
    const node = createNode();
    const context = createContext();
    // ❌ 缺少 engine mock
    const result = fsm._handle_branch(context);
    assert(result === FRAME_COMPLETE);
}

// Agent 11 (Mock专家) 审查:
{
    "missing_mocks": [
        {
            "reason": "_handle_branch 需要调用 engine._get_next_unvisited_child",
            "location": "line 5",
            "impact": "测试无法正确执行DYNAMIC_MATCH路径"
        }
    ]
}

// 改进后:
function test_branch_dynamic() {
    const node = createNode();
    const context = createContext();
    const mockEngine = {
        _get_next_unvisited_child: jest.fn().mockReturnValue(null)
    };
    // ✅ 添加了 mock
    const result = fsm._handle_branch(context, mockEngine);
    assert(result === FRAME_COMPLETE);
    assert(mockEngine._get_next_unvisited_child.called);  // 验证调用
}
```

### 示例2: QA Battle

```javascript
// Agent 15 (QA) 审查:
{
    "vulnerabilities": [
        {
            "scenario": "修改代码让测试通过但引入bug",
            "exploit": [
                "1. 修改 _handle_branch 总是返回 FRAME_COMPLETE",
                "2. 测试仍然通过",
                "3. 但实际功能失效 - DYNAMIC_MATCH 节点无法遍历"
            ],
            "root_cause": "测试没有验证 visited_children 的变化",
            "severity": "HIGH"
        }
    ]
}
```

---

## Battle vs 非Battle 对比

### 非Battle流程

```
Agent: 生成测试
↓
完成
```

**质量**: 64% (B-)

**问题**:
- Mock缺失 → 2/10
- 断言不足 → 6/10
- Fixture差 → 2/10

### Battle流程

```
Agent: 生成测试
↓
Battle Agents: 找问题
├─ Agent 11: 缺少mock
├─ Agent 12: 断言不足
├─ Agent 13: 代码重复
└─ Agent 15: 有漏洞
↓
Agent: 改进代码
↓
Battle Agents: 验证改进
↓
完成
```

**预期质量**: 85%+ (A)

**改进**:
- Mock完整 → 8/10
- 断言充分 → 9/10
- Fixture良好 → 8/10

---

## 关键设计原则

### 1. 对抗性角色设定

每个Battle Agent都有"人设"：
- **怀疑论者** - 不相信前一个Agent
- **批评者** - 专门找问题
- **破坏派** - 想方设法破坏
- **防御派** - 找安全隐患

### 2. 多视角验证

不同Agent从不同角度验证：
- 开发者视角 - 调试难度
- QA视角 - 漏洞和破坏
- 安全视角 - 危险模式
- 代码审查 - 质量问题

### 3. 闭环改进

Battle结果 → 改进 → 再次验证
```
问题 → 改进 → 验证改进 → 完成
```

### 4. 累积验证

后续Battle基于前面Battle的结果：
```
Phase 1 Battle结果 → Phase 2 场景
Phase 2 Battle结果 → Phase 3 代码
Phase 3 Battle结果 → Phase 4 终极验证
```

---

## 实施指南

### 创建新的Battle Agent

```javascript
const customBattleAgent = async (workToVerify: any) => {
  // 1. 设定角色
  const role = "你是[对抗者/批评者/破坏派]";

  // 2. 明确任务
  const task = "你的任务是找[特定类型]的问题";

  // 3. 输入前一个Agent的结果
  const input = JSON.stringify(workToVerify);

  // 4. 返回结构化问题列表
  const output = "{found_issues: [...], severity: 'HIGH'}";

  return await agent(`${role}\n\n${task}\n\n${input}\n\n返回${output}`);
};
```

### Battle Agent提示词模板

```markdown
你是 **{角色名称}**。

## 你的角色设定
{角色描述}

## 你的任务
审查以下 {工作产物}：

```
{input}
```

## 找这些问题
1. {问题类型1}
2. {问题类型2}
3. {问题类型3}

## 输出格式
```json
{
  "found_issues": [
    {
      "type": "issue_type",
      "location": "file:line",
      "description": "问题描述",
      "severity": "HIGH|MEDIUM|LOW",
      "suggestion": "改进建议"
    }
  ],
  "summary": {
    "total_issues": 0,
    "critical": 0,
    "overall_assessment": "PASS|FAIL"
  }
}
```
```

---

## 文件

**Workflow**: [`.claude/workflows/multi-agent-test-validation-with-battle.js`](.claude/workflows/multi-agent-test-validation-with-battle.js)

**使用**:
```bash
/Workflow multi-agent-test-validation-battle state_machine
```

---

**维护者**: Uni-Claw Development Team
**核心理念**: 对抗性验证创造更高质量的测试

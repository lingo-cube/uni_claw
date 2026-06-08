# Multi-Agent Test Validation Architecture

> **多Agent测试验证架构**
> **创建**: 2026-06-08

---

## 架构概览

### 核心思想

**将测试验证分解为多个专门Agent，每个Agent负责特定维度的验证，通过Workflow协调并行执行。**

```
┌─────────────────────────────────────────────────────────────┐
│                    Workflow Orchestrator                     │
│                   (multi-agent-test-validation)             │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
   ┌────▼────┐         ┌────▼────┐         ┌────▼────┐
   │ Phase 1 │         │ Phase 2 │         │ Phase 3 │
   │ Analyze │         │ Extract │         │ Generate│
   └────┬────┘         └────┬────┘         └────┬────┘
        │                    │                    │
   ┌────┴──────────┐  ┌────┴──────────┐  ┌────┴──────────┐
   │ Agent 1       │  │ Agent 3       │  │ Agent 4       │
   │ 代码实现分析   │  │ 测试场景综合   │  │ 测试代码生成   │
   │               │  │               │  │               │
   │ Agent 2       │  │               │  │               │
   │ 设计文档分析   │  │               │  │               │
   └───────────────┘  └───────────────┘  └───────────────┘
                                                  │
                                           ┌──────▼──────┐
                                           │  Phase 4    │
                                           │  Verify     │
                                           │  (并行4个Agent)│
                                           └──────┬──────┘
                                                  │
                     ┌──────────────────────────────┼──────────────────────────────┐
                     │              │              │              │
              ┌──────▼──────┐ ┌────▼──────┐ ┌────▼──────┐ ┌────▼──────┐
              │ Agent 5.1   │ │ Agent 5.2  │ │ Agent 5.3  │ │ Agent 5.4  │
              │ Mock验证    │ │ 断言验证   │ │ 覆盖度验证 │ │ Fixture验证│
              └─────────────┘ └───────────┘ └───────────┘ └───────────┘
                                                  │
                                           ┌──────▼──────┐
                                           │  Phase 5    │
                                           │  Report     │
                                           │             │
                                           │ Agent 6     │
                                           │ 报告生成    │
                                           └─────────────┘
```

---

## Agent 角色定义

### Phase 1: 分析阶段 (并行执行2个Agent)

#### Agent 1: 代码实现分析专家

**职责**: 分析源代码实现，提取实际行为

**输入**: 模块名称
**输出**: 代码行为规范 JSON

```json
{
  "classes": [
    {
      "name": "TraversalStateMachine",
      "methods": [
        {
          "name": "_handle_branch",
          "signature": "stack, context, engine=None",
          "external_dependencies": ["engine._get_next_unvisited_child"],
          "side_effects": ["modifies state"],
          "invariants": ["stack.size() >= 0"],
          "returns": "TraversalState"
        }
      ]
    }
  ]
}
```

**关键任务**:
- 定位核心源文件
- 分析方法签名和参数
- 识别外部依赖 (需要mock)
- 识别状态变更 (副作用)
- 识别不变量 (不应改变)

#### Agent 2: 设计文档分析专家

**职责**: 从设计文档提取规范和场景

**输入**: 模块名称
**输出**: 设计规范 JSON

```json
{
  "behavior_spec": {
    "should": ["所有子节点访问完返回FRAME_COMPLETE"],
    "should_not": ["DYNAMIC_MATCH不应总是返回True"]
  },
  "parameters": {
    "required": ["stack"],
    "optional": ["engine"]
  },
  "scenarios": [
    {
      "id": "BRANCH-001",
      "description": "静态节点无子节点",
      "expected": "返回FRAME_COMPLETE"
    }
  ]
}
```

---

### Phase 2: 提取阶段

#### Agent 3: 测试场景综合专家

**职责**: 结合代码和设计，生成完整测试场景

**输入**: 代码分析 + 设计分析
**输出**: 测试场景 JSON

```json
{
  "scenarios": [
    {
      "id": "TEST-001",
      "method": "TraversalStateMachine._handle_branch",
      "type": "normal|boundary|error",
      "description": "静态节点无子节点返回FRAME_COMPLETE",
      "given": "node with empty static_children",
      "when": "call _handle_branch",
      "then": "returns FRAME_COMPLETE",
      "mocks": [
        {
          "service": "engine._get_next_unvisited_child",
          "return": "null"
        }
      ],
      "side_effects_to_verify": [
        "stack.size() unchanged",
        "visited_children unchanged"
      ],
      "invariants_to_check": [
        "stack != null",
        "context != null"
      ]
    }
  ]
}
```

**关键改进**:
- 标注每个场景需要的 mock
- 指定需要验证的副作用
- 指定需要检查的不变量

---

### Phase 3: 生成阶段

#### Agent 4: 测试代码生成专家

**职责**: 根据测试场景生成高质量pytest代码

**输入**: 测试场景 JSON
**输出**: Python测试代码

**要求**:
1. 使用 pytest.fixture 减少重复
2. 正确 mock 所有外部依赖
3. 验证副作用和不变式
4. 清晰的测试命名和文档字符串

---

### Phase 4: 验证阶段 (并行执行4个Agent)

#### Agent 5.1: Mock验证专家

**职责**: 验证测试的Mock质量

**检查项**:
- 所有外部依赖都有 mock
- mock 返回值合理
- mock 被正确验证
- 没有 over-mocking

**输出**: `{ mock_score: 0-10, missing_mocks: [...] }`

#### Agent 5.2: 断言验证专家

**职责**: 验证测试的断言质量

**检查项**:
- 每个测试至少3个断言
- 断言有明确的错误消息
- 验证了副作用
- 验证了不变量

**输出**: `{ assertion_score: 0-10, missing_assertions: [...] }`

#### Agent 5.3: 覆盖度验证专家

**职责**: 验证测试场景的覆盖度

**检查项**:
- 所有方法都有测试
- 正常路径有测试
- 边界条件有测试
- 错误场景有测试

**输出**: `{ coverage_score: 0-10, missing_scenarios: [...] }`

#### Agent 5.4: Fixture验证专家

**职责**: 验证Fixture使用质量

**检查项**:
- 使用了 pytest.fixture
- fixture 命名清晰
- fixture 作用域正确
- 没有 fixture 滥用

**输出**: `{ fixture_score: 0-10, suggestions: [...] }`

---

### Phase 5: 报告阶段

#### Agent 6: 报告生成专家

**职责**: 汇总所有验证结果，生成完整报告

**输入**: 所有Agent的输出
**输出**: Markdown格式报告

**报告包含**:
1. 执行摘要
2. 代码 vs 设计 对照
3. 测试覆盖度分析
4. 质量评分 (总分/70分)
5. 改进建议
6. 下一步行动

---

## 使用方式

### 命令行

```bash
# 使用 Workflow 工具
/Workflow multi-agent-test-validation state_machine

# 或使用 Agent 工具指定
Agent({
  subagent_type: 'workflow',
  description: 'Run multi-agent test validation',
  args: ['state_machine']
})
```

### 参数

- `args[0]`: 模块名称 (如 `state_machine`, `graph`, `traversal`)

### 输出

1. 测试代码文件: `tests/{module}/test_{feature}.py`
2. 验证报告: `docs/reports/{MODULE}_TEST_VALIDATION_{DATE}.md`

---

## 与现有流程的对比

### 当前流程 (单Agent)

```
Claude (单Agent)
  ├─ 读取设计文档
  ├─ 提取测试场景
  ├─ 生成测试代码
  └─ 完成
```

**问题**: 只覆盖设计文档，不验证代码实现

### 新流程 (多Agent)

```
Agent 1 (代码分析) ─┐
                    ├─→ Agent 3 (场景综合) → Agent 4 (代码生成)
Agent 2 (文档分析) ─┘                                    │
                                                          ├─→ Agent 6 (报告)
Agent 5.1-5.4 (并行验证) ───────────────────────────────┘
```

**优势**:
- ✅ 代码和文档并行分析，效率高
- ✅ 场景基于代码实现，不是设计描述
- ✅ 多维度并行验证，质量高
- ✅ 自动评分和改进建议

---

## 质量评分标准

### 总分: 70分

| 维度 | Agent | 分值 | 评分标准 |
|------|-------|------|----------|
| Mock质量 | 5.1 | 10分 | 所有依赖都有mock且正确验证 |
| 断言质量 | 5.2 | 10分 | 每个测试3+断言，有错误消息 |
| 覆盖度 | 5.3 | 10分 | 所有方法、边界、错误都覆盖 |
| Fixture质量 | 5.4 | 10分 | 有效使用fixture减少重复 |
| 场景完整性 | 3 | 10分 | 包含正常、边界、错误场景 |
| 代码vs设计一致性 | 6 | 10分 | 测试验证代码符合设计 |
| 测试可维护性 | 4 | 10分 | 代码清晰、命名规范 |

**等级**:
- A: 63-70分 (90%+)
- B: 56-62分 (80-89%)
- C: 49-55分 (70-79%)
- D: 42-48分 (60-69%)
- F: <42分 (<60%)

---

## 实施计划

### Phase 1: Prototype (当前)

- [x] 创建 Workflow 脚本
- [ ] 测试单个模块
- [ ] 验证 Agent 协作

### Phase 2: 优化

- [ ] 优化 Agent prompt
- [ ] 添加更多验证维度
- [ ] 改进报告格式

### Phase 3: 集成

- [ ] 集成到 `/skill test-extraction`
- [ ] 添加到开发流程
- [ ] 自动化执行

---

## 预期效果

### 质量提升

| 指标 | 当前 | 目标 |
|------|------|------|
| Mock质量 | 2/10 | 8/10 |
| 断言质量 | 6/10 | 9/10 |
| Fixture质量 | 2/10 | 8/10 |
| 综合得分 | 45/70 | 60/70 (85%) |

### 效率提升

- 代码分析和文档分析: 并行执行，时间减半
- 质量验证: 4个Agent并行，时间减少75%

---

**维护者**: Uni-Claw Development Team
**相关文档**:
- UNIT_TEST_QUALITY_ROOT_CAUSE_ANALYSIS.md
- TEST_EXTRACTION_SOLIDIFICATION.md
- MULTI_AGENT_TEST_VALIDATION.JS (Workflow脚本)

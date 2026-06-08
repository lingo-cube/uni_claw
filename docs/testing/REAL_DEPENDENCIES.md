# Workflow真实依赖验证

> **版本**: V1.0 | **日期**: 2026-06-08
> **目的**: 明确workflow中哪些依赖是真实可用的

---

## 依赖分类

### ✅ 真实可用的依赖

| 依赖 | 类型 | 可用方式 | 验证状态 |
|------|------|----------|----------|
| 设计文档 | 文本 | Agent读取 | ✅ 已验证 |
| 源代码 | 文本 | Agent读取 | ✅ 已验证 |
| 测试规则YAML | 文本 | Agent理解 | ✅ 已验证 |
| 测试方法论 | 文本 | Agent理解 | ✅ 已验证 |

### ❌ 不可直接调用的依赖

| 依赖 | 类型 | 问题 |
|------|------|------|
| rule-engine.js | Node.js模块 | Workflow无法执行JavaScript |
| YAML解析器 | 程序 | Workflow无法直接解析 |

---

## Phase 3和Phase 4的真实依赖

### Phase 3: Generate

**真实可用**:
- ✅ 设计文档内容 (Agent可读取)
- ✅ 源代码 (Agent可分析)
- ✅ 测试规则YAML (Agent可理解文本)

**执行方式**:
```javascript
// 不是：调用rule-engine.js
// 而是：让Agent理解规则并应用

const testCode = await agent(`
  生成测试代码，遵循以下规则：

  ${yamlContent}  // 直接把YAML内容传给Agent

  命名规范: test_{feature}_{condition}
  断言要求: 每个测试至少3个断言
  Mock要求: 所有外部依赖都要mock
`, { model: 'opus' });
```

### Phase 4: Verify

**真实可用**:
- ✅ Agent可以验证代码质量
- ✅ Agent可以理解规则要求

**执行方式**:
```javascript
// 不是：ruleEngine.validate(testCode)
// 而是：Agent验证

const verification = await agent(`
  验证以下测试代码的质量：

  代码: ${testCode}

  检查项：
  1. 是否所有外部依赖都有mock？
  2. 每个测试是否至少3个断言？
  3. 命名是否符合规范？

  返回: {score: 0-100, issues: [...]}
`, { model: 'haiku' });
```

---

## 规则引擎的实际作用

`rule-engine.js` 的真实作用是：

1. ✅ **文档参考** - 展示如何理解和应用规则
2. ✅ **人类使用** - 开发者可以运行它来验证
3. ❌ **Workflow调用** - Workflow无法直接执行

---

## 可靠的执行方式

### 方式1: 规则内容内联

```javascript
// 读取YAML内容作为文本
const rulesContent = await readFile('docs/rules/testing-rules.yaml');

// 传给Agent
const agent = await agent(`
  遵循以下规则生成测试：

  ${rulesContent}
`);
```

### 方式2: 关键规则提取

```javascript
// 预先提取关键规则
const KEY_RULES = `
命名: test_{feature}_{condition}
断言: 最少3个
Mock: 所有外部依赖
覆盖: 目标85%
`;

const agent = await agent(`
  遵循规则: ${KEY_RULES}
`);
```

### 方式3: 分步验证

```javascript
// 每个验证项单独运行
const mockCheck = await agent(`检查Mock使用...`);
const assertCheck = await agent(`检查断言数量...`);
const namingCheck = await agent(`检查命名规范...`);
```

---

## 结论

### 原声称

```
Phase 3: Generate → 依赖 testing-rules.yaml + rule-engine.js
Phase 4: Verify → 依赖 rule-engine.js
```

### 真实情况

```
Phase 3: Generate → 依赖 YAML内容 (Agent理解)
Phase 4: Verify → 依赖 Agent验证能力
```

### 可靠性评分

| Phase | 原可靠性 | 实际可靠性 |
|-------|----------|------------|
| Phase 1 | ✅ | ✅ 文件检查 |
| Phase 2 | ✅ | ✅ Agent提取 |
| Phase 3 | ❓ | ✅ Agent生成 |
| Phase 4 | ❌ | ✅ Agent验证 |
| Phase 5 | ✅ | ✅ Agent报告 |

---

## 修正后的依赖图

```
┌─────────────────────────────────────────┐
│  Phase 3: Generate                      │
│  依赖:                                   │
│  ✅ 设计文档 (Agent读取)                 │
│  ✅ 源代码 (Agent分析)                   │
│  ✅ YAML规则 (Agent理解)                 │
└─────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────┐
│  Phase 4: Verify                        │
│  依赖:                                   │
│  ✅ Agent验证能力                        │
│  ✅ 规则文本 (参考)                      │
└─────────────────────────────────────────┘
```

---

**关键结论**: Workflow不依赖程序执行规则引擎，而是依赖Agent理解规则并执行验证。

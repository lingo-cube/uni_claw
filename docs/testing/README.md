# Uni-Claw 测试生成系统

> **版本**: V1.0 | **日期**: 2026-06-08
> **目的**: 完整的测试生成和验证闭环

---

## 快速开始

### 生成测试 (多Agent工作流)

```bash
/Workflow multi-agent-test-validation-tiered <module>
```

**9个阶段**: Plan → Execute → Battle → Extract → Generate → Verify → Refine → FinalVerify → Report

**示例**:
```bash
/Workflow multi-agent-test-validation-tiered state_machine
/Workflow multi-agent-test-validation-tiered graph
```

### 运行测试

```bash
/Skill module-test <module>
```

---

## 在开发流程中使用

### /opsx:apply 完整闭环

详见: [../../WORKFLOW_INTEGRATION_GUIDE.md](../../WORKFLOW_INTEGRATION_GUIDE.md)

```
实现任务 → 测试生成 → 运行测试 → 验证覆盖率 → 确认完成
```

**使用示例**:
```bash
# 1. 实现代码后
/Workflow integrated-test-gen <module>

# 2. 运行测试
/Skill module-test <module>

# 3. 检查覆盖率
pytest tests/<module>/ --cov=src/<module>
```

---

## 核心文档

| 文档 | 用途 |
|------|------|
| [WORKFLOW_COMPLETE_GUIDE.md](WORKFLOW_COMPLETE_GUIDE.md) | 📘 Workflow完整使用指南 |
| [WORKFLOW_EXECUTION_GUIDE.md](WORKFLOW_EXECUTION_GUIDE.md) | 执行流程详解 |
| [TEST_REPORT_MANAGEMENT.md](TEST_REPORT_MANAGEMENT.md) | 报告存放规范 |
| [CLOSED_LOOP_VERIFICATION.md](CLOSED_LOOP_VERIFICATION.md) | 闭环验证报告 |
| [REAL_DEPENDENCIES.md](REAL_DEPENDENCIES.md) | 真实依赖分析 |
| [TEST_EXTRACTION_METHODOLOGY.md](TEST_EXTRACTION_METHODOLOGY.md) | 5步测试提取方法论 |

---

## 执行流程

```
用户输入: /Workflow integrated-test-gen <module>
    │
    ▼
┌─────────────────────────────────────┐
│  Phase 1: Check                     │
│  检查设计文档是否有测试场景           │
└──────────────┬──────────────────────┘
               │
      ┌────────┴────────┐
      │                 │
      ▼                 ▼
   有场景             无场景
      │                 │
      │            ┌────▼────┐
      │            │ Extract │
      │            │ 5步提取  │
      │            └────┬────┘
      │                 │
      └────────┬────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Phase 2: Generate                  │
│  多Agent并行生成测试代码              │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Phase 3: Verify                   │
│  验证Mock、断言、覆盖度              │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│  Phase 4: Report                    │
│  生成质量报告                        │
└─────────────────────────────────────┘
```

---

## 模块状态

| 模块 | 设计文档 | 测试场景 | 状态 |
|------|----------|----------|------|
| state_machine | ✅ | ✅ | 可直接生成 |
| graph | ✅ | ✅ | 可直接生成 |
| traversal | ✅ | ❌ | 首次生成自动提取 |
| exception | ✅ | ❌ | 首次生成自动提取 |
| 其他模块 | ✅ | ❌ | 首次生成自动提取 |

---

## 依赖文件

### 必需

- `docs/architecture/modules/{module}-design.md` - 设计文档
- `docs/testing/TEST_EXTRACTION_METHODOLOGY.md` - 提取方法论
- `docs/rules/testing-rules.yaml` - 测试规则
- `src/{module}/` - 源代码

### Workflow

- `.claude/workflows/integrated-test-gen.js` - 主执行workflow

---

## 真实依赖说明

本系统**不依赖**外部程序执行（如Node.js规则引擎），而是：

- ✅ Agent读取文件内容
- ✅ Agent理解规则文本
- ✅ Agent执行验证逻辑

详细说明见 [REAL_DEPENDENCIES.md](REAL_DEPENDENCIES.md)

---

## 维护者

Uni-Claw Development Team

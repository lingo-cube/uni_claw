# CLAUDE.md Modular Refactor - Design Document

## Context

### 当前状态

Uni-claw 项目当前使用单一 `CLAUDE.md` 文件 (390 行) 作为 AI 工作的主要上下文文档。该文件混合了多种职责：

1. **项目身份与原则** (~50 行) - 项目描述、技术栈、设计原则
2. **模块地图** (~40 行) - 核心模块及其职责
3. **导航表格** (~150 行) - 完整文档索引链接
4. **状态信息** (~50 行) - 版本、OpenSpec 变更、验证状态
5. **工作流** (~40 行) - 开发命令和流程
6. **约定规范** (~20 行) - 隐含的代码约定
7. **文档贡献指南** (~30 行) - 文档维护规范
8. **历史记录** (~50 行) - 文档重组说明

同时，项目存在严重的文档混乱问题：
- **PRD 版本混乱**: 10+ 个 PRD_V6_*.md 文件，命名不一致（V6_1, V6_9.1, V6_9_2），缺 V6.5
- **测试文档重叠**: 6 个测试相关文档边界不清（TEST_GUIDE, TESTING_QUICK_REFERENCE, TESTING_FLOWCHARTS, TESTING_DOCS_INDEX, TESTING_STANDARDS, TESTING_WORKFLOWS）
- **70+ 文档文件**: 缺乏明确生命周期和所有者
- **归档目录缺失**: CLAUDE.md 引用 `docs/archive/prd/` 但目录不存在

### 问题场景

| 场景 | 当前状态 | 理想状态 |
|------|----------|----------|
| AI 开始新会话 | 解析 390 行，大量导航内容 | 解析 ~100 行核心指导 |
| AI 需要查找文档 | 在 390 行中搜索，效率低 | CLAUDE.md 指引，按需加载 |
| 添加新 PRD | 散落在 docs/，命名混乱 | 统一 docs/prd/，结构清晰 |
| 临时测试文件 | 散落在项目各处 | 统一 temp/ 目录 |
| 文档过期 | 无检测机制 | 自动扫描提醒 |

### 约束条件

- 不破坏现有文档内容（仅重组）
- 向后兼容（链接可重定向）
- AI 可独立使用（不依赖外部工具）
- 维护成本低（自动化检查）

## Goals / Non-Goals

**Goals:**
- CLAUDE.md 精简到 ~100 行核心内容
- 建立清晰的文档组织结构
- 统一临时文件管理
- 建立长期维护机制防止退化
- 所有现有内容保留（仅重组）

**Non-Goals:**
- 删除有价值的历史文档
- 修改文档内容（仅组织）
- 引入复杂文档生成系统
- 改变代码结构或 API

## Decisions

### 决策 1: 模块化文件结构

**选择**: 将 CLAUDE.md 拆分为 5 个单一职责文件

**理由**:
- 单一职责：每个文件有明确目的
- 按需加载：AI 只加载任务相关文件
- 易维护：更新一个不影响其他

**文件结构**:
```
CLAUDE.md                 # ~100 行，核心 AI 指导
CLAUDE_STATUS.md          # ~50 行，项目状态（易变）
CLAUDE_WORKFLOW.md        # ~60 行，工作流与命令
CLAUDE_CONVENTIONS.md     # ~80 行，代码规范
docs/INDEX.md             # ~200 行，完整导航
```

**替代方案**: 保持单一文件
- 拒绝原因：无法解决 token 效率和发现困难问题

### 决策 2: 文档归一化组织

**选择**: PRD 按版本组织，测试文档按职责组织

**PRD 结构**:
```
docs/
├── prd/                    # V6 系列（当前）
│   ├── PRD_V6_1-*.md
│   ├── PRD_V6_2-*.md
│   └── ...
├── archive/
│   └── prd/                # V5 及更老（归档）
└── PRD_UNIFIED.md          # 统一入口
```

**测试文档结构**:
```
docs/testing/
├── README.md               # 总入口（原 TEST_GUIDE.md）
├── STANDARDS.md            # 质量标准
├── WORKFLOWS.md           # 工作流
└── QUICK_REFERENCE.md     # 快速查询
```

**替代方案**: 全部归档，只保留 PRD_UNIFIED.md
- 拒绝原因：V6 是当前版本，不应全部归档

### 决策 3: 统一临时目录

**选择**: 所有临时文件放入 `temp/` 目录

**理由**:
- 简单：单一位置，无需判断
- 清理方便：可删除整个目录
- .gitignore：不提交临时内容

**temp/ 结构**:
```
temp/
├── tests/                  # 临时测试文件
├── reports/               # 临时报告
├── verification/          # 验证输出
└── analysis/              # 临时分析
```

**文件放置规则**:
| 文件类型 | 位置 |
|---------|------|
| CLAUDE 配置 | 项目根目录 |
| 文档 | docs/ |
| 测试文档 | docs/testing/ |
| Spec 文档 | docs/superpowers/specs/ |
| 脚本 | scripts/ |
| 测试 | tests/ |
| **所有临时文件** | **temp/** |

### 决策 4: 维护脚本设计

**选择**: 创建 3 个独立脚本，各有侧重

**脚本职责**:
```
verify_docs.py       # 结构合规检查（CI/本地）
doc_freshness.py     # 过期文档扫描（月度）
doc_audit.py         # 综合审计（月度）
```

**检查项**:
- CLAUDE 模块化文件存在
- Testing 结构正确
- PRD 结构正确（V6 在 prd/，老版本在 archive/prd/）
- 根目录无散乱文件
- 临时文件都在 temp/
- temp/ 在 .gitignore
- 无破损内部链接

**替代方案**: 单一复杂脚本
- 拒绝原因：职责不清，难以维护

## Implementation Strategy

### Phase 0: 文档清理归一化

**目标**: 建立清晰的文档基础

**步骤**:
1. 创建目录结构
   - `docs/prd/`
   - `docs/archive/prd/`
   - `docs/testing/`

2. PRD 重组
   - `PRD_V6_*.md` → `docs/prd/`
   - `PRD_V5_*.md` 及更老 → `docs/archive/prd/`
   - 保留 `PRD_UNIFIED.md` 在 `docs/`

3. 测试文档重组
   - `TEST_GUIDE.md` → `docs/testing/README.md`
   - `TESTING_STANDARDS.md` → `docs/testing/STANDARDS.md`
   - `TESTING_WORKFLOWS.md` → `docs/testing/WORKFLOWS.md`
   - `TESTING_QUICK_REFERENCE.md` → `docs/testing/QUICK_REFERENCE.md`
   - 删除 `TESTING_DOCS_INDEX.md`, `TESTING_FLOWCHARTS.md`

4. 清理临时文档
   - 评估并归档/删除临时过程文档

### Phase 1: 创建模块化文件

**目标**: 建立新的文件结构

**新建文件**:
- `docs/INDEX.md` - 从 CLAUDE.md 提取导航表格
- `CLAUDE_STATUS.md` - 从 CLAUDE.md 提取状态信息
- `CLAUDE_WORKFLOW.md` - 从 CLAUDE.md 提取工作流
- `CLAUDE_CONVENTIONS.md` - 编写代码规范
- `temp/` - 创建临时目录结构
- 更新 `.gitignore` - 添加 `temp/`

### Phase 2: 重写 CLAUDE.md

**目标**: 精简到核心内容

**新 CLAUDE.md 结构** (~100 行):
```markdown
# Uni-Claw AI Context

## Project Identity
[项目描述、技术栈、架构风格]

## Core Design Principles
[6 条核心设计原则]

## Essential Module Map
[核心模块地图]

## Before You Work
[工作前检查清单]

## File Placement Rules ⭐
[文件放置规则表格]

## Quick Reference
[快速参考链接]
```

### Phase 3: 创建维护脚本

**目标**: 建立自动化检查

**新建脚本**:
- `scripts/verify_docs.py`
- `scripts/doc_freshness.py`
- `scripts/doc_audit.py`

### Phase 4: 验证

**目标**: 确保无破坏

**验证步骤**:
1. 运行 `pytest` 确保测试通过
2. 运行 `verify_docs.py` 检查结构
3. 测试典型 AI 场景

### Phase 5: 建立维护流程

**目标**: 长期维护机制

**步骤**:
1. 更新 `CLAUDE_CONVENTIONS.md` 添加文档规范
2. 更新 `CLAUDE_WORKFLOW.md` 添加 AI 工作流
3. （可选）设置 pre-commit hook

### Phase 6: 归档与收尾

**目标**: 完成迁移

**步骤**:
1. 归档旧 `CLAUDE.md` 到 `docs/archive/CLAUDE.md.pre-refactor`
2. 提交所有变更
3. 生成验证报告

## File Structure

### 新建文件

```
CLAUDE_STATUS.md                   # ~50 行
CLAUDE_WORKFLOW.md                 # ~60 行
CLAUDE_CONVENTIONS.md              # ~80 行
docs/
├── INDEX.md                       # ~200 行
├── prd/                           # 新目录
│   └── PRD_V6_*.md               # 移动至此
├── archive/
│   └── prd/                       # 新目录
│       └── PRD_V*_md (V5及更老)  # 移动至此
└── testing/                       # 新目录
    ├── README.md                  # 原 TEST_GUIDE.md
    ├── STANDARDS.md               # 移动
    ├── WORKFLOWS.md              # 移动
    └── QUICK_REFERENCE.md        # 移动
scripts/
├── verify_docs.py                 # 新建
├── doc_freshness.py              # 新建
└── doc_audit.py                  # 新建
temp/                              # 新目录（.gitignore）
├── tests/
├── reports/
├── verification/
└── analysis/
```

### 修改文件

```
CLAUDE.md                          # 重写为 ~100 行
.gitignore                         # 添加 temp/
```

### 删除文件

```
docs/TESTING_DOCS_INDEX.md
docs/TESTING_FLOWCHARTS.md
[其他评估后需删除的临时文档]
```

## Success Criteria

1. **Token 效率**: CLAUDE.md 从 390 行减少到 ~100 行
2. **发现速度**: AI 可在 <2 次文件读取内找到相关文档
3. **结构合规**: verify_docs.py 检查通过
4. **无信息丢失**: 所有原始内容保留（仅重组）
5. **测试通过**: pytest 全部通过

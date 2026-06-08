# CLAUDE.md Modular Refactor & Documentation Cleanup

## Why

当前 CLAUDE.md (390 行) 对 AI 开发不友好，存在以下问题：

1. **上下文过载** - 关键 AI 指导信息被导航表格淹没
2. **Token 效率低** - AI 每次会话都需解析 390 行，其中大量是导航内容
3. **维护负担** - 单文件混合了稳定参考与易变状态信息
4. **发现困难** - AI 难以快速找到"如何工作"的指导
5. **文档混乱** - 70+ 文档文件缺乏明确生命周期：
   - PRD 版本混乱（V6_1, V6_9.1, V6_9_2 命名不一致，缺 V6.5）
   - 测试文档重叠（6 个测试文档边界不清）
   - 缺少 docs/archive/prd/ 目录但 CLAUDE.md 引用了它

通过模块化重构和文档清理：
- **提升 AI 效率** - CLAUDE.md 减少到 ~100 行，任务相关文件按需加载
- **建立长期秩序** - 文档归一化结构，维护机制防止退化
- **提高可维护性** - 单一职责文件，更新一个不影响其他

## What Changes

### Phase 0: 文档清理归一化 (1-2 天)

1. **PRD 重组**
   - 创建 `docs/prd/` 和 `docs/archive/prd/`
   - V6 PRD → `docs/prd/` (当前版本)
   - V5 及更老 → `docs/archive/prd/` (归档)
   - 保留 `docs/PRD_UNIFIED.md` 作为统一入口

2. **测试文档重组**
   - 创建 `docs/testing/` 目录
   - `TEST_GUIDE.md` → `docs/testing/README.md` (总入口)
   - `TESTING_STANDARDS.md` → `docs/testing/STANDARDS.md` (质量标准)
   - `TESTING_WORKFLOWS.md` → `docs/testing/WORKFLOWS.md` (工作流)
   - `TESTING_QUICK_REFERENCE.md` → `docs/testing/QUICK_REFERENCE.md` (快速查询)
   - 删除 `TESTING_DOCS_INDEX.md` 和 `TESTING_FLOWCHARTS.md`

3. **临时/过程文档清理**
   - 评估并归档/删除临时过程文档

### Phase 1-2: CLAUDE 模块化 (2 天)

1. **创建模块化文件**
   - `docs/INDEX.md` - 完整文档导航
   - `CLAUDE_STATUS.md` - 项目状态与易变信息
   - `CLAUDE_WORKFLOW.md` - 开发工作流与命令
   - `CLAUDE_CONVENTIONS.md` - 代码规范与模式
   - `temp/` 目录 - 统一临时文件位置

2. **重写 CLAUDE.md**
   - 精简为 ~100 行核心 AI 指导
   - 项目身份、设计原则、模块地图
   - 文件放置规则
   - 交叉引用其他文件

### Phase 3-6: 维护机制与验证 (3 天)

1. **创建维护脚本**
   - `scripts/verify_docs.py` - 结构合规检查
   - `scripts/doc_freshness.py` - 过期文档扫描
   - `scripts/doc_audit.py` - 月度审计

2. **建立维护流程**
   - 更新 CLAUDE_CONVENTIONS.md 添加文档规范
   - 更新 CLAUDE_WORKFLOW.md 添加 AI 工作流
   - 设置自动化检查（可选）

3. **验证与收尾**
   - 运行测试确保无破坏
   - 生成验证报告
   - 归档旧 CLAUDE.md

## Capabilities

### New Capabilities

- **doc-structure-verification**: 自动检查文档结构合规性
- **doc-freshness-scanning**: 扫描过期文档
- **unified-temp-directory**: 统一临时文件管理

### Modified Capabilities

- **AI-context-loading**: CLAUDE.md 精简，按需加载任务相关文件
- **documentation-maintenance**: 建立长期维护机制

## Impact

### 代码影响

- `CLAUDE.md` - 完全重写（精简到 ~100 行）
- `.gitignore` - 添加 `temp/` 目录
- `scripts/` - 新增 3 个维护脚本

### 文档影响

**移动/重命名**:
- 所有 `PRD_V*.md` → `docs/prd/` 或 `docs/archive/prd/`
- 测试文档 → `docs/testing/`

**新建**:
- `docs/INDEX.md`, `CLAUDE_STATUS.md`, `CLAUDE_WORKFLOW.md`, `CLAUDE_CONVENTIONS.md`
- `scripts/verify_docs.py`, `scripts/doc_freshness.py`, `scripts/doc_audit.py`

**删除**:
- `TESTING_DOCS_INDEX.md`, `TESTING_FLOWCHARTS.md`
- 其他冗余文档

### 数据影响

- 无数据格式变更
- 现有状态文件完全兼容

### API 影响

- 无 API 变更
- 纯文档结构调整

### 性能影响

- AI 每次会话初始加载减少 ~290 行
- 任务相关文件按需加载

### 向后兼容性

- 完全兼容
- 仅文档组织变化，无代码行为变更

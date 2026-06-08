# Implementation Tasks - CLAUDE.md Modular Refactor & Documentation Cleanup

> **总预估时间**: 5-7 天
> 
> **Phase 分解**: 
> - Phase 0: 文档清理归一化 (1-2 天)
> - Phase 1: 创建模块化文件 (1 天)
> - Phase 2: 重写 CLAUDE.md (1 天)
> - Phase 3: 创建维护脚本 (1 天)
> - Phase 4: 验证 (0.5 天)
> - Phase 5: 建立维护流程 (0.5 天)
> - Phase 6: 归档与收尾 (0.5 天)

## Phase 0: 文档清理归一化

### 0.1 PRD 重组

- [ ] 0.1.1 创建 `docs/prd/` 目录
- [ ] 0.1.2 创建 `docs/archive/prd/` 目录
- [ ] 0.1.3 移动所有 `docs/PRD_V6_*.md` 到 `docs/prd/`
- [ ] 0.1.4 移动所有 `docs/PRD_V5_*.md` 及更老版本到 `docs/archive/prd/`
- [ ] 0.1.5 验证 `docs/PRD_UNIFIED.md` 存在且内容完整
- [ ] 0.1.6 验证 PRD 结构正确（V6 在 prd/，老版本在 archive/prd/）

### 0.2 测试文档重组

- [ ] 0.2.1 创建 `docs/testing/` 目录
- [ ] 0.2.2 移动 `docs/TEST_GUIDE.md` 到 `docs/testing/README.md`
- [ ] 0.2.3 移动 `docs/TESTING_STANDARDS.md` 到 `docs/testing/STANDARDS.md`
- [ ] 0.2.4 移动 `docs/TESTING_WORKFLOWS.md` 到 `docs/testing/WORKFLOWS.md`
- [ ] 0.2.5 移动 `docs/TESTING_QUICK_REFERENCE.md` 到 `docs/testing/QUICK_REFERENCE.md`
- [ ] 0.2.6 删除 `docs/TESTING_DOCS_INDEX.md`（将被 INDEX.md 取代）
- [ ] 0.2.7 评估 `docs/TESTING_FLOWCHARTS.md`：合并到 WORKFLOWS.md 或删除
- [ ] 0.2.8 更新所有测试文档内的交叉引用到新路径

### 0.3 临时文档清理

- [ ] 0.3.1 评估 `docs/DEPENDENCY_FIX.md`：归档（已解决的问题）
- [ ] 0.3.2 评估 `docs/EXPECTEDBEHAVIOR_YAML_REFERENCE.md`：保留或归档
- [ ] 0.3.3 评估 `docs/PROBLEM_DETECTOR_REFERENCE.md`：保留或归档
- [ ] 0.3.4 评估 `docs/PAGEANALYSIS_FIELD_MAPPING.md`：保留或归档
- [ ] 0.3.5 创建 `docs/archive/temporary/` 存档已解决的临时文档

### 0.4 验证文档整合

- [ ] 0.4.1 保留 `docs/validation/final_report.md`
- [ ] 0.4.2 保留 `docs/validation/system_infrastructure_analysis.md`
- [ ] 0.4.3 归档/合并 `docs/validation/` 中的进度报告
- [ ] 0.4.4 归档/合并 `docs/validation/` 中的累积指南

## Phase 1: 创建模块化文件

### 1.1 创建 docs/INDEX.md

- [ ] 1.1.1 从当前 CLAUDE.md 提取所有导航表格内容
- [ ] 1.1.2 创建 `docs/INDEX.md` 包含完整文档导航
- [ ] 1.1.3 添加架构文档部分
- [ ] 1.1.4 添加模块设计文档部分（17+ 模块）
- [ ] 1.1.5 添加测试文档部分
- [ ] 1.1.6 添加 PRD 文档部分（包含版本历史）
- [ ] 1.1.7 添加 API 文档部分

### 1.2 创建 CLAUDE_STATUS.md

- [ ] 1.2.1 创建 `CLAUDE_STATUS.md`
- [ ] 1.2.2 添加当前版本信息（V6.3）
- [ ] 1.2.3 添加最后更新日期
- [ ] 1.2.4 添加活跃的 OpenSpec 变更表格
- [ ] 1.2.5 添加验证状态（V6 实施、V6.3 Trace、测试覆盖率）
- [ ] 1.2.6 添加已知问题章节（模板）

### 1.3 创建 CLAUDE_WORKFLOW.md

- [ ] 1.3.1 创建 `CLAUDE_WORKFLOW.md`
- [ ] 1.3.2 添加"开始开发"章节
- [ ] 1.3.3 添加常用命令章节（验证、测试、Dashboard）
- [ ] 1.3.4 添加 OpenSpec 工作流章节
- [ ] 1.3.5 添加测试哲学章节

### 1.4 创建 CLAUDE_CONVENTIONS.md

- [ ] 1.4.1 创建 `CLAUDE_CONVENTIONS.md`
- [ ] 1.4.2 添加强类型规范（MANDATORY ⭐）
  - [ ] 1.4.2.1 函数必须有类型注解
  - [ ] 1.4.2.2 使用具体类型，禁用 Any
  - [ ] 1.4.2.3 泛型类型需要边界
  - [ ] 1.4.2.4 返回类型必须显式
- [ ] 1.4.3 添加设计模式章节
  - [ ] 1.4.3.1 接口优先（Interface-First）
  - [ ] 1.4.3.2 依赖注入示例
- [ ] 1.4.4 添加命名规范章节
- [ ] 1.4.5 添加文件组织章节
- [ ] 1.4.6 添加测试约定章节
- [ ] 1.4.7 添加临时文件章节（ALL go to temp/）
- [ ] 1.4.8 添加文件放置约定（File Placement Conventions ⭐）

### 1.5 创建 temp/ 目录

- [ ] 1.5.1 创建 `temp/` 目录
- [ ] 1.5.2 创建 `temp/tests/` 子目录
- [ ] 1.5.3 创建 `temp/reports/` 子目录
- [ ] 1.5.4 创建 `temp/verification/` 子目录
- [ ] 1.5.5 创建 `temp/analysis/` 子目录
- [ ] 1.5.6 更新 `.gitignore` 添加 `temp/`

## Phase 2: 重写 CLAUDE.md

### 2.1 创建新 CLAUDE.md

- [ ] 2.1.1 备份当前 `CLAUDE.md` 内容
- [ ] 2.1.2 创建新 `CLAUDE.md`（~100 行）
- [ ] 2.1.3 添加项目身份章节
  - [ ] 2.1.3.1 What: Mobile UI automation traversal framework, AI-driven
  - [ ] 2.1.3.2 Tech Stack: Python 3.10+, ADB, DeepSeek/Anthropic AI
  - [ ] 2.1.3.3 Architecture Style: Interface-driven, dependency injection, event-driven
- [ ] 2.1.4 添加核心设计原则章节（6 条）
  - [ ] 2.1.4.1 Interface-first
  - [ ] 2.1.4.2 Dependency injection
  - [ ] 2.1.4.3 State separation
  - [ ] 2.1.4.4 Observability-first
  - [ ] 2.1.4.5 Simulation优先 (V6)
  - [ ] 2.1.4.6 Testing discovers problems
- [ ] 2.1.5 添加核心模块地图章节
  - [ ] 2.1.5.1 AI服务 - src/ai/
  - [ ] 2.1.5.2 Traversal - src/traversal/
  - [ ] 2.1.5.3 GraphEngine (V6) - src/traversal/graph_engine.py
  - [ ] 2.1.5.4 Simulation (V6) - src/simulation/
  - [ ] 2.1.5.5 State - src/state/, src/state_machine/
  - [ ] 2.1.5.6 Exception - src/exception/
  - [ ] 2.1.5.7 Observability - src/trace/, src/analysis/
- [ ] 2.1.6 添加"Before You Work"章节
  - [ ] 2.1.6.1 Read relevant module README
  - [ ] 2.1.6.2 Follow code conventions
  - [ ] 2.1.6.3 Check current status
  - [ ] 2.1.6.4 Use workflow
- [ ] 2.1.7 添加文件放置规则章节（File Placement Rules ⭐）
  - [ ] 2.1.7.1 NEVER create files at project root
  - [ ] 2.1.7.2 文件类型表格（CLAUDE files, Documentation, Architecture, Testing, Scripts, Temporary, etc.）
  - [ ] 2.1.7.3 temp/ directory 特性说明
  - [ ] 2.1.7.4 Before creating any file 检查清单
- [ ] 2.1.8 添加快速参考章节（Quick Reference）
  - [ ] 2.1.8.1 Full doc index: docs/INDEX.md
  - [ ] 2.1.8.2 Current status: CLAUDE_STATUS.md
  - [ ] 2.1.8.3 Workflow: CLAUDE_WORKFLOW.md
  - [ ] 2.1.8.4 Conventions: CLAUDE_CONVENTIONS.md
  - [ ] 2.1.8.5 Testing: docs/testing/README.md

## Phase 3: 创建维护脚本

### 3.1 创建 scripts/verify_docs.py

- [ ] 3.1.1 创建 `scripts/verify_docs.py`
- [ ] 3.1.2 添加 CLAUDE 模块化文件存在检查
  - [ ] 3.1.2.1 CLAUDE.md 存在
  - [ ] 3.1.2.2 CLAUDE_STATUS.md 存在
  - [ ] 3.1.2.3 CLAUDE_WORKFLOW.md 存在
  - [ ] 3.1.2.4 CLAUDE_CONVENTIONS.md 存在
  - [ ] 3.1.2.5 docs/INDEX.md 存在
- [ ] 3.1.3 添加 Testing 结构检查
  - [ ] 3.1.3.1 docs/testing/ 目录存在
  - [ ] 3.1.3.2 docs/testing/README.md 存在
  - [ ] 3.1.3.3 docs/testing/STANDARDS.md 存在
  - [ ] 3.1.3.4 docs/testing/WORKFLOWS.md 存在
  - [ ] 3.1.3.5 docs/testing/QUICK_REFERENCE.md 存在
- [ ] 3.1.4 添加 PRD 结构检查
  - [ ] 3.1.4.1 docs/prd/ 目录存在
  - [ ] 3.1.4.2 docs/archive/prd/ 目录存在
  - [ ] 3.1.4.3 docs/ 根目录下无孤儿 PRD 文件（只有 PRD_UNIFIED.md）
- [ ] 3.1.5 添加破损链接检查
  - [ ] 3.1.5.1 检查所有 Markdown 文件中的内部链接
  - [ ] 3.1.5.2 验证链接目标存在
- [ ] 3.1.6 添加根目录散乱文件检查
  - [ ] 3.1.6.1 检查项目根目录
  - [ ] 3.1.6.2 允许：CLAUDE_*.md, README.md, .gitignore 等
  - [ ] 3.1.6.3 报告其他文件
- [ ] 3.1.7 添加临时文件位置检查
  - [ ] 3.1.7.1 检查临时文件是否在 temp/
  - [ ] 3.1.7.2 报告散落的临时文件
- [ ] 3.1.8 添加 .gitignore 检查
  - [ ] 3.1.8.1 验证 temp/ 在 .gitignore 中
- [ ] 3.1.9 添加命令行接口
  - [ ] 3.1.9.1 支持运行 `python scripts/verify_docs.py`
  - [ ] 3.1.9.2 返回码 1 如果发现违规

### 3.2 创建 scripts/doc_freshness.py

- [ ] 3.2.1 创建 `scripts/doc_freshness.py`
- [ ] 3.2.2 添加过期文档扫描
  - [ ] 3.2.2.1 扫描 >90 天未更新的文档
  - [ ] 3.2.2.2 默认天数可通过 --days 参数配置
- [ ] 3.2.3 添加代码-文档同步检查
  - [ ] 3.2.3.1 检查文档 last_updated 与相关代码修改时间
  - [ ] 3.2.3.2 报告文档过时可能
- [ ] 3.2.4 添加状态检查
  - [ ] 3.2.4.1 检查 deprecated/draft 状态文档
  - [ ] 3.2.4.2 报告 >30 天的 deprecated/draft 文档
- [ ] 3.2.5 添加命令行接口
  - [ ] 3.2.5.1 支持 `python scripts/doc_freshness.py --days=90`

### 3.3 创建 scripts/doc_audit.py

- [ ] 3.3.1 创建 `scripts/doc_audit.py`
- [ ] 3.3.2 调用 verify_docs.py 检查
- [ ] 3.3.3 调用 doc_freshness.py 检查
- [ ] 3.3.4 添加代码-文档覆盖率检查
- [ ] 3.3.5 添加命名约定合规性检查
- [ ] 3.3.6 生成综合报告
  - [ ] 3.3.6.1 输出到 `docs/reports/doc_audit_YYYY-MM-DD.md`
  - [ ] 3.3.6.2 包含所有检查结果

## Phase 4: 验证

### 4.1 测试验证

- [ ] 4.1.1 运行 `pytest tests/ -v`
- [ ] 4.1.2 验证所有测试通过
- [ ] 4.1.3 检查测试覆盖率

### 4.2 脚本验证

- [ ] 4.2.1 运行 `python scripts/verify_docs.py`
- [ ] 4.2.2 验证结构检查通过
- [ ] 4.2.3 运行 `python scripts/doc_freshness.py`
- [ ] 4.2.4 运行 `python scripts/doc_audit.py`

### 4.3 AI 场景测试

- [ ] 4.3.1 测试快速问答场景（仅 CLAUDE.md）
- [ ] 4.3.2 测试功能开发场景（CLAUDE.md + STATUS + WORKFLOW + module README）
- [ ] 4.3.3 测试 Bug 修复场景（CLAUDE.md + CONVENTIONS + exception docs）
- [ ] 4.3.4 测试架构探索场景（CLAUDE.md + INDEX.md + specific doc）
- [ ] 4.3.5 验证 AI 可在 <2 次跳转内找到相关文档

### 4.4 内容保留验证

- [ ] 4.4.1 验证所有原始内容已保留
- [ ] 4.4.2 验证无信息丢失

## Phase 5: 建立维护流程

### 5.1 更新 CLAUDE_CONVENTIONS.md

- [ ] 5.1.1 添加文档约定章节
  - [ ] 5.1.1.1 文件命名规则
  - [ ] 5.1.1.2 新文档放置位置
  - [ ] 5.1.1.3 元数据要求（last_updated, status, version）
  - [ ] 5.1.1.4 何时更新文档

### 5.2 更新 CLAUDE_WORKFLOW.md

- [ ] 5.2.1 添加 AI 文档工作流章节
  - [ ] 5.2.1.1 AI 修改代码时应更新文档
  - [ ] 5.2.1.2 提交前运行 verify_docs.py
  - [ ] 5.2.1.3 如何处理文档引用

### 5.3 设置自动化（可选）

- [ ] 5.3.1 评估是否需要 pre-commit hook
- [ ] 5.3.2 添加月度 doc_audit.py 到日历提醒
- [ ] 5.3.3 在项目维护指南中记录

## Phase 6: 归档与收尾

### 6.1 归档旧 CLAUDE.md

- [ ] 6.1.1 创建 `docs/archive/` 目录
- [ ] 6.1.2 归档旧 CLAUDE.md 到 `docs/archive/CLAUDE.md.pre-refactor`
- [ ] 6.1.3 添加归档说明

### 6.2 提交变更

- [ ] 6.2.1 检查所有变更
- [ ] 6.2.2 提交所有变更
  - [ ] 6.2.2.1 git add 所有新建/修改文件
  - [ ] 6.2.2.2 git rm 所有删除文件
  - [ ] 6.2.2.3 git commit -m "refactor: CLAUDE.md modular refactor and documentation cleanup"
- [ ] 6.2.3 创建 Git tag（可选）

### 6.3 生成验证报告

- [ ] 6.3.1 生成最终验证报告
- [ ] 6.3.2 记录迁移笔记

## Design Docs

> Auto-generated from proposal Impact section.
> Implementation agents: read these before starting.

| Module | Design Doc |
|--------|------------|
| docs/ | docs/architecture/ARCHITECTURE.md |
| testing/ | docs/TESTING_STANDARDS.md, docs/TESTING_WORKFLOWS.md |
| scripts/ | (General Python conventions) |

**Note**: This is primarily a documentation reorganization. No code module changes are expected, so no specific module design docs need to be read for implementation.

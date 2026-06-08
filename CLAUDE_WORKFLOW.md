# Claude Development Workflow Guide

> Quick reference for AI-assisted development in Uni-Claw
> Created: 2026-06-08

## Starting Development

**Pre-Development:**
1. Read `CLAUDE.md` for project overview
2. Review `docs/architecture/ARCHITECTURE.md`
3. Check module README in `docs/architecture/modules/`
4. Verify tests pass: `pytest src/ -v`

**Design Principles:** Interface-driven, Dependency injection, Event-driven, Simulation-first

## Common Commands

```bash
# Verification & Testing
/skill module-test                    # Run module tests with analysis
/skill validation-documentation       # Generate validation reports from unit tests
pytest src/ -v                         # Unit tests
pytest tests/ -v                       # Integration tests
pytest src/ --cov=src --cov-report=term-missing
pytest tests/v6/ -v                    # V6 simulation tests
python scripts/verify_docs.py         # Verify documentation structure
python scripts/verify_simulation_system.py  # Verify simulation system

# Dashboard
python dashboards/simple_dashboard.py

# OpenSpec
/skill brainstorming                   # Explore ideas before proposing
/opsx:propose                          # Propose new change
/opsx:apply                            # Implement tasks from change
/opsx:archive                          # Archive completed change
/opsx:explore                          # Explore ideas
```

**Validation Workflow:**
1. `/skill module-test` - Run tests and collect results
2. `/skill validation-documentation` - Generate validation reports with visualizations from unit test results

## PRD File Organization

**PRD Storage Structure:**
```
docs/
├── PRD_UNIFIED.md              # PRD统一入口
├── prd/                        # 当前V6系列PRD
│   ├── PRD_V6_1-*.md
│   ├── PRD_V6_2-*.md
│   └── ...
└── archive/
    └── prd/                    # 历史版本PRD（V5及更老）
```

**Creating PRDs:**
1. Use `/skill brainstorming` to explore ideas
2. Use `/opsx:propose` to generate formal PRD with design/spec/tasks
3. PRD stored in `openspec/changes/<name>/proposal.md`
4. Archived PRDs moved to `docs/archive/prd/`

## OpenSpec Workflow

**Lifecycle:** Explore → Propose → **Self-Driven Execution** → Archive

1. `/skill brainstorming` - Explore ideas and requirements
2. `/opsx:propose` - Generates design/spec/tasks in `openspec/changes/<name>/`
3. **`/Workflow self-driven-task-execution <change>`** - Self-driven task execution
   - 自动获取任务列表
   - Multi-agent实现和验证
   - 问题追踪和需求演化
   - 循环完成所有任务
4. `/opsx:archive` - Archive completed change to `openspec/changes/archive/`

### Self-Driven Task Execution

详见: [architecture/workflows/SELF_DRIVEN_TASK_EXECUTION.md](architecture/workflows/SELF_DRIVEN_TASK_EXECUTION.md)

```bash
# 启动自我驱动任务执行
/Workflow self-driven-task-execution prd-v6-9-1-test-refactor

# Workflow会自动:
# 1. 获取任务列表
# 2. 循环执行每个任务:
#    - Opus实现任务
#    - Multi-agent验证
#    - Agent对抗验证
#    - Opus裁决
#    - 标记完成
# 3. 所有任务完成
```
任务实现 → /Workflow multi-agent-test-validation-tiered → /Skill module-test → 验证覆盖率 → 确认完成
```

**闭环检查清单**:
- [ ] 代码实现
- [ ] 多Agent测试生成 (`/Workflow multi-agent-test-validation-tiered <module>`)
- [ ] Battle闭环验证
- [ ] 测试通过 (`/Skill module-test <module>`)
- [ ] 覆盖率达标 (≥85%)
- [ ] 生成报告

## Testing Philosophy

**Core:** Test in isolation, Fail fast, Mock dependencies, Trace everything

**Organization:** `tests/unit/`, `tests/integration/`, `tests/simulation/`, `tests/v6/`

**Before Committing:** `/skill module-test` + `/skill validation-documentation`

## Quick Reference

| Task | Command | Docs |
|------|---------|------|
| Brainstorm | `/skill brainstorming` | Generate PRD ideas |
| Propose | `/opsx:propose` | OpenSpec |
| Apply | `/opsx:apply` | Implement tasks |
| Architecture | `CLAUDE.md` | Overview |
| Module docs | `docs/architecture/modules/*.md` | Design specs |
| PRD docs | `docs/prd/`, `docs/PRD_UNIFIED.md` | Requirements |
| Test | `/skill module-test` | Run module tests |
| Validation | `/skill validation-documentation` | Generate validation reports |
| Explore | `/opsx:explore` | Explore mode |

**Related:** [CLAUDE.md](CLAUDE.md), [CLAUDE_CONVENTIONS.md](CLAUDE_CONVENTIONS.md), [docs/testing/STANDARDS.md](docs/testing/STANDARDS.md), [docs/architecture/ARCHITECTURE.md](docs/architecture/ARCHITECTURE.md)

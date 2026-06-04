# Documentation Reorganization Design

> **Design Date**: 2026-06-04
> **Project**: Uni-Claw - AI驱动的移动UI自动化遍历框架
> **Type**: Documentation Reorganization
> **Approach**: Phased Migration

---

## Problem Statement

The Uni-Claw project has accumulated extensive documentation across multiple directories with inconsistent organization:

- Module design docs scattered in `docs/modules/`
- Architecture documents mixed with PRDs in main `docs/`
- Historical PRDs cluttering the main documentation area
- CLAUDE.md not fully leveraging existing comprehensive module documentation

This makes it difficult to:
- Find relevant architecture information quickly
- Understand the complete module design
- Maintain documentation consistency
- Distinguish current documentation from historical records

## Solution Overview

Reorganize all architecture and module documentation into a centralized `docs/architecture/` structure using a phased migration approach, followed by aggressive cleanup of obsolete content.

## Target Structure

```
docs/architecture/
├── ARCHITECTURE.md              # Main architecture overview
├── ARCHITECTURE_V6.md           # V6 simulator and state machine architecture
├── modules/
│   ├── README.md               # Module documentation index
│   ├── ai-design.md            # AI service provider design
│   ├── adb-design.md           # ADB client design
│   ├── analysis-design.md      # Analysis module design
│   ├── config-design.md        # Configuration management design
│   ├── context-design.md       # Traversal context design
│   ├── exception-design.md     # Exception handling design
│   ├── graph-design.md         # Graph model design
│   ├── models-design.md        # Core models design
│   ├── safety-design.md        # Safety filtering design
│   ├── simulation-design.md    # Simulation testing design
│   ├── state-design.md         # State management design
│   ├── state_machine-design.md # State machine design
│   ├── trace-design.md         # Distributed tracing design
│   ├── traversal-design.md     # Traversal engine design
│   ├── utils-design.md         # Utilities design
│   └── vision-design.md        # Vision service design
└── concepts/
    ├── hierarchical-state-machine.md    # Hierarchical state machine concepts
    ├── state-machine-design.md         # State machine detailed design
    ├── observability.md                 # Observability system design
    └── graph-model.md                   # Graph model concepts
```

## Implementation: Phased Migration

### Phase 1: Structure & Module Migration

**Goal**: Establish new structure and migrate most important content

**Actions**:
1. Create `docs/architecture/` directory structure
2. Move all module design docs from `docs/modules/` to `docs/architecture/modules/`
3. Move main architecture docs:
   - `docs/ARCHITECTURE.md` → `docs/architecture/ARCHITECTURE.md`
   - `docs/ARCHITECTURE_V6.md` → `docs/architecture/ARCHITECTURE_V6.md`
4. Update CLAUDE.md references to new paths
5. Verify all links work correctly

**Deliverables**:
- New architecture directory structure
- All module docs in new location
- Updated CLAUDE.md with working references

### Phase 2: Architecture Consolidation

**Goal**: Consolidate remaining architecture-related documents

**Actions**:
1. Move concept documents to `docs/architecture/concepts/`:
   - `docs/hierarchical_state_machine.md` → `docs/architecture/concepts/hierarchical-state-machine.md`
   - `docs/state_machine_design.md` → `docs/architecture/concepts/state-machine-design.md`
   - `docs/OBSERVABILITY.md` → `docs/architecture/concepts/observability.md`
   - `docs/GRAPH_MODEL.md` → `docs/architecture/concepts/graph-model.md`
2. Update CLAUDE.md references
3. Update any cross-references between documents

**Deliverables**:
- All concept docs consolidated
- All references updated
- Clean separation of overview, modules, and concepts

### Phase 3: Aggressive Cleanup

**Goal**: Archive historical content and remove duplicates

**Actions**:
1. Create `docs/archive/prd/` directory
2. Archive historical PRDs:
   - `docs/PRD_V5_0-initial.md`
   - `docs/PRD_V5_1-ai-integration.md`
   - `docs/PRD_V5_2-flattened-screen.md`
   - `docs/PRD_V5_3-unibrain-refactoring.md`
   - `docs/PRD_V6_0-simulation-testing.md`
3. Keep active: `docs/PRD_UNIFIED.md` as the primary PRD
4. Consolidate testing documentation:
   - Review `docs/TEST_GUIDE.md`, `docs/TESTING_WORKFLOWS.md`, `docs/TESTING_ARCHITECTURE.md`
   - Remove redundant content, consolidate into clear hierarchy
5. Archive old design specs from `docs/superpowers/specs/`:
   - `2026-05-31-unibrain-design.md` (implemented)
   - `2026-06-02-trace-system-improvements.md` (implemented)
   - `2026-06-02-v6-executor-state-machine-simulator.md` (implemented)
6. Remove `docs/modules/` directory after successful migration

**Deliverables**:
- Clean documentation structure
- Archived historical content
- No duplicate documentation
- Clear separation of current vs. historical content

## CLAUDE.md Updates

Update the documentation navigation section to reference new structure:

```markdown
### 系统架构

| 文档 | 路径 | 说明 |
|------|------|------|
| **架构总览** | [docs/architecture/ARCHITECTURE.md](docs/architecture/ARCHITECTURE.md) | 完整系统架构说明 |
| **V6架构** | [docs/architecture/ARCHITECTURE_V6.md](docs/architecture/ARCHITECTURE_V6.md) | V6 仿真器与状态机架构 |

### 架构模块设计

| 模块 | 设计文档 | 说明 |
|------|----------|------|
| **AI模块** | [docs/architecture/modules/ai-design.md](docs/architecture/modules/ai-design.md) | AI服务设计 |
| **ADB模块** | [docs/architecture/modules/adb-design.md](docs/architecture/modules/adb-design.md) | ADB客户端设计 |
| **遍历引擎** | [docs/architecture/modules/traversal-design.md](docs/architecture/modules/traversal-design.md) | 遍历引擎设计 |
| **图模型** | [docs/architecture/modules/graph-design.md](docs/architecture/modules/graph-design.md) | 图模型设计 |
| **仿真模块** | [docs/architecture/modules/simulation-design.md](docs/architecture/modules/simulation-design.md) | 仿真测试设计 |
| **状态管理** | [docs/architecture/modules/state-design.md](docs/architecture/modules/state-design.md) | 状态管理设计 |
| **异常处理** | [docs/architecture/modules/exception-design.md](docs/architecture/modules/exception-design.md) | 异常处理设计 |
| **可观测性** | [docs/architecture/modules/trace-design.md](docs/architecture/modules/trace-design.md) | 追踪系统设计 |
| **视觉服务** | [docs/architecture/modules/vision-design.md](docs/architecture/modules/vision-design.md) | 视觉服务设计 |
| **安全过滤** | [docs/architecture/modules/safety-design.md](docs/architecture/modules/safety-design.md) | 安全过滤设计 |
| **上下文管理** | [docs/architecture/modules/context-design.md](docs/architecture/modules/context-design.md) | 上下文管理设计 |
| **配置管理** | [docs/architecture/modules/config-design.md](docs/architecture/modules/config-design.md) | 配置管理设计 |
| **分析模块** | [docs/architecture/modules/analysis-design.md](docs/architecture/modules/analysis-design.md) | 分析模块设计 |
| **工具模块** | [docs/architecture/modules/utils-design.md](docs/architecture/modules/utils-design.md) | 工具模块设计 |
| **核心模型** | [docs/architecture/modules/models-design.md](docs/architecture/modules/models-design.md) | 核心模型设计 |
| **状态机** | [docs/architecture/modules/state_machine-design.md](docs/architecture/modules/state_machine-design.md) | 状态机设计 |

### 架构概念

| 文档 | 路径 | 说明 |
|------|------|------|
| **层级状态机** | [docs/architecture/concepts/hierarchical-state-machine.md](docs/architecture/concepts/hierarchical-state-machine.md) | 层级状态机概念 |
| **状态机设计** | [docs/architecture/concepts/state-machine-design.md](docs/architecture/concepts/state-machine-design.md) | 状态机详细设计 |
| **可观测性** | [docs/architecture/concepts/observability.md](docs/architecture/concepts/observability.md) | 可观测性系统设计 |
| **图模型** | [docs/architecture/concepts/graph-model.md](docs/architecture/concepts/graph-model.md) | 图模型概念 |
```

## Benefits

1. **Improved Discoverability**: All architecture documentation in one location
2. **Better CLAUDE.md Integration**: Comprehensive reference to existing high-quality module docs
3. **Historical Clarity**: Clear separation between current and historical content
4. **Easier Maintenance**: Consistent structure makes updates easier
5. **Reduced Confusion**: No duplicate documentation, clear hierarchy

## Risk Mitigation

- **Phased approach**: Each phase can be tested independently
- **Reference validation**: Verify all links work after each phase
- **Git preservation**: Historical content preserved in archive/, not deleted
- **Rollback capability**: Each phase is atomic and can be reverted if needed

## Success Criteria

1. All architecture and module documentation consolidated in `docs/architecture/`
2. CLAUDE.md accurately references all module design docs
3. No broken links or dead references
4. Historical PRDs archived but preserved
5. Duplicate documentation removed
6. Clear distinction between overview, modules, and concepts

---

**Design completed**: 2026-06-04
**Next steps**: Create OpenSpec proposal for implementation

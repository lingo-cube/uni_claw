# Documentation Reorganization Design

> **Change**: documentation-reorganization
> **Based on**: `docs/superpowers/specs/2026-06-04-documentation-reorganization-design.md`

---

## Target Architecture

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

docs/archive/prd/
├── PRD_V5_0-initial.md
├── PRD_V5_1-ai-integration.md
├── PRD_V5_2-flattened-screen.md
├── PRD_V5_3-unibrain-refactoring.md
└── PRD_V6_0-simulation-testing.md
```

## Implementation Strategy: Three-Phase Migration

### Phase 1: Structure & Module Migration

**Goal**: Establish new structure and migrate most important content

**Actions**:
1. Create `docs/architecture/` directory with subdirectories:
   - `docs/architecture/modules/`
   - `docs/architecture/concepts/`
2. Move all module design docs from `docs/modules/` to `docs/architecture/modules/`:
   - All 17 module design files
3. Move main architecture docs:
   - `docs/ARCHITECTURE.md` → `docs/architecture/ARCHITECTURE.md`
   - `docs/ARCHITECTURE_V6.md` → `docs/architecture/ARCHITECTURE_V6.md`
4. Update CLAUDE.md references to point to new architecture paths
5. Verify all internal links work correctly

**Validation Criteria**:
- All 17 module docs accessible in new location
- CLAUDE.md references updated and working
- No broken links to architecture documents

### Phase 2: Architecture Consolidation

**Goal**: Consolidate remaining architecture-related documents

**Actions**:
1. Create `docs/architecture/concepts/` directory
2. Move concept documents:
   - `docs/hierarchical_state_machine.md` → `docs/architecture/concepts/hierarchical-state-machine.md`
   - `docs/state_machine_design.md` → `docs/architecture/concepts/state-machine-design.md`
   - `docs/OBSERVABILITY.md` → `docs/architecture/concepts/observability.md`
   - `docs/GRAPH_MODEL.md` → `docs/architecture/concepts/graph-model.md`
3. Update CLAUDE.md references to new concept paths
4. Update any cross-references between documents

**Validation Criteria**:
- All concept docs consolidated
- CLAUDE.md fully updated
- No dead references between documents

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
   - Remove redundant content
   - Ensure clear hierarchy
5. Archive implemented design specs from `docs/superpowers/specs/`:
   - `2026-05-31-unibrain-design.md` (implemented)
   - `2026-06-02-trace-system-improvements.md` (implemented)
   - `2026-06-02-v6-executor-state-machine-simulator.md` (implemented)
6. Remove empty `docs/modules/` directory after successful migration

**Validation Criteria**:
- Historical PRDs archived but preserved
- No duplicate documentation remaining
- Empty directories removed
- Archive structure organized

## Technical Approach

**File Movement Strategy**:
- Use `git mv` to preserve file history
- Maintain relative internal links within documents
- Update absolute references in CLAUDE.md

**Reference Updates**:
- Update all markdown links in CLAUDE.md
- Verify no broken internal links
- Test that documentation navigation works

**Archival Strategy**:
- Create proper archive directory structure
- Preserve historical content for reference
- Update CLAUDE.md to indicate archived content location

## Risk Mitigation

**Low Risk Factors**:
- Content remains unchanged, only locations change
- Historical content preserved, not deleted
- Git history allows easy rollback

**Phase Gates**:
- Each phase must pass validation before proceeding
- Link verification after each phase
- Ability to rollback individual phases if needed

**Validation Strategy**:
- Manual verification of CLAUDE.md links
- Check for broken internal references
- Verify directory structure correctness

## CLAUDE.md Integration

The CLAUDE.md file will be updated to become the comprehensive navigation hub:

**New Sections**:
- **系统架构**: References to main architecture docs
- **架构模块设计**: Complete table of all 17 module design docs
- **架构概念**: References to conceptual architecture documents
- **PRD文档**: Reference to current PRD_UNIFIED.md and archive location

**Reference Format**:
```markdown
### 架构模块设计

| 模块 | 设计文档 | 说明 |
|------|----------|------|
| **AI模块** | [docs/architecture/modules/ai-design.md](docs/architecture/modules/ai-design.md) | AI服务设计 |
| **ADB模块** | [docs/architecture/modules/adb-design.md](docs/architecture/modules/adb-design.md) | ADB客户端设计 |
...
```

## Success Metrics

1. **Organization**: 100% of architecture docs under `docs/architecture/`
2. **Integration**: CLAUDE.md references all module design docs
3. **Quality**: Zero broken internal links
4. **Preservation**: Historical content preserved in archive/
5. **Cleanup**: No duplicate documentation
6. **Usability**: Clear hierarchy between overview, modules, and concepts

---

**Design completed**: 2026-06-04
**Next steps**: Implementation via tasks.md

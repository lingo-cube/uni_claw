# Documentation Reorganization Implementation Tasks

> **Change**: documentation-reorganization
> **Approach**: Three-Phase Migration
> **Estimated Complexity**: Medium (file moves and reference updates)

---

## Phase 1: Structure & Module Migration

### Task 1.1: Create Directory Structure
**Priority**: High | **Estimated Time**: 5 minutes

**Steps**:
1. Create `docs/architecture/` directory
2. Create `docs/architecture/modules/` directory
3. Verify directory creation success

**Command**:
```bash
mkdir -p docs/architecture/modules
```

**Validation**:
```bash
ls -la docs/architecture/
```

---

### Task 1.2: Move Module Design Documents
**Priority**: High | **Estimated Time**: 10 minutes

**Steps**:
1. Move all 17 module design docs from `docs/modules/` to `docs/architecture/modules/`:
   ```bash
   git mv docs/modules/README.md docs/architecture/modules/README.md
   git mv docs/modules/ai_design.md docs/architecture/modules/ai-design.md
   git mv docs/modules/adb_design.md docs/architecture/modules/adb-design.md
   git mv docs/modules/analysis_design.md docs/architecture/modules/analysis-design.md
   git mv docs/modules/config_design.md docs/architecture/modules/config-design.md
   git mv docs/modules/context_design.md docs/architecture/modules/context-design.md
   git mv docs/modules/exception_design.md docs/architecture/modules/exception-design.md
   git mv docs/modules/graph_design.md docs/architecture/modules/graph-design.md
   git mv docs/modules/models_design.md docs/architecture/modules/models-design.md
   git mv docs/modules/safety_design.md docs/architecture/modules/safety-design.md
   git mv docs/modules/simulation_design.md docs/architecture/modules/simulation-design.md
   git mv docs/modules/state_design.md docs/architecture/modules/state-design.md
   git mv docs/modules/state_machine_design.md docs/architecture/modules/state-machine-design.md
   git mv docs/modules/trace_design.md docs/architecture/modules/trace-design.md
   git mv docs/modules/traversal_design.md docs/architecture/modules/traversal-design.md
   git mv docs/modules/utils_design.md docs/architecture/modules/utils-design.md
   git mv docs/modules/vision_design.md docs/architecture/modules/vision-design.md
   ```

**Validation**:
```bash
ls -la docs/architecture/modules/
```

---

### Task 1.3: Move Architecture Overview Documents
**Priority**: High | **Estimated Time**: 5 minutes

**Steps**:
1. Move main architecture docs to `docs/architecture/`:
   ```bash
   git mv docs/ARCHITECTURE.md docs/architecture/ARCHITECTURE.md
   git mv docs/ARCHITECTURE_V6.md docs/architecture/ARCHITECTURE_V6.md
   ```

**Validation**:
```bash
ls -la docs/architecture/
```

---

### Task 1.4: Update CLAUDE.md Architecture References
**Priority**: High | **Estimated Time**: 15 minutes

**Steps**:
1. Update CLAUDE.md system architecture section to reference new paths
2. Update main architecture doc references:
   - `[docs/ARCHITECTURE.md]` → `[docs/architecture/ARCHITECTURE.md]`
   - `[docs/ARCHITECTURE_V6.md]` → `[docs/architecture/ARCHITECTURE_V6.md]`

**Files to Modify**:
- `CLAUDE.md`

**Validation**:
- Review CLAUDE.md for correct paths
- Test links in markdown viewer

---

### Task 1.5: Add Module Design References to CLAUDE.md
**Priority**: High | **Estimated Time**: 20 minutes

**Steps**:
1. Create new "架构模块设计" section in CLAUDE.md
2. Add comprehensive table referencing all 17 module design docs:
   ```markdown
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
   ```

**Files to Modify**:
- `CLAUDE.md`

**Validation**:
- Review CLAUDE.md for completeness
- Test several module doc links

---

### Task 1.6: Phase 1 Validation
**Priority**: High | **Estimated Time**: 10 minutes

**Steps**:
1. Verify all module docs are in new location
2. Test CLAUDE.md links to architecture docs
3. Test sample of module doc links
4. Ensure no broken references

**Validation Commands**:
```bash
# Check structure
ls -R docs/architecture/

# Verify git tracked files properly
git status
```

**Success Criteria**:
- All 17 module docs accessible
- CLAUDE.md links working
- No broken references

---

## Phase 2: Architecture Consolidation

### Task 2.1: Create Concepts Directory
**Priority**: Medium | **Estimated Time**: 2 minutes

**Steps**:
1. Create `docs/architecture/concepts/` directory

**Command**:
```bash
mkdir -p docs/architecture/concepts
```

---

### Task 2.2: Move Concept Documents
**Priority**: Medium | **Estimated Time**: 5 minutes

**Steps**:
1. Move concept docs to `docs/architecture/concepts/`:
   ```bash
   git mv docs/hierarchical_state_machine.md docs/architecture/concepts/hierarchical-state-machine.md
   git mv docs/state_machine_design.md docs/architecture/concepts/state-machine-design.md
   git mv docs/OBSERVABILITY.md docs/architecture/concepts/observability.md
   git mv docs/GRAPH_MODEL.md docs/architecture/concepts/graph-model.md
   ```

**Validation**:
```bash
ls -la docs/architecture/concepts/
```

---

### Task 2.3: Update CLAUDE.md Concept References
**Priority**: Medium | **Estimated Time**: 10 minutes

**Steps**:
1. Create new "架构概念" section in CLAUDE.md
2. Update references to concept docs:
   ```markdown
   ### 架构概念

   | 文档 | 路径 | 说明 |
   |------|------|------|
   | **层级状态机** | [docs/architecture/concepts/hierarchical-state-machine.md](docs/architecture/concepts/hierarchical-state-machine.md) | 层级状态机概念 |
   | **状态机设计** | [docs/architecture/concepts/state-machine-design.md](docs/architecture/concepts/state-machine-design.md) | 状态机详细设计 |
   | **可观测性** | [docs/architecture/concepts/observability.md](docs/architecture/concepts/observability.md) | 可观测性系统设计 |
   | **图模型** | [docs/architecture/concepts/graph-model.md](docs/architecture/concepts/graph-model.md) | 图模型概念 |
   ```

**Files to Modify**:
- `CLAUDE.md`

**Validation**:
- Review CLAUDE.md for correct concept paths
- Test concept doc links

---

### Task 2.4: Remove Redundant References
**Priority**: Medium | **Estimated Time**: 10 minutes

**Steps**:
1. Remove old concept doc references from existing CLAUDE.md sections
2. Update cross-references in other docs if they exist
3. Remove duplicate entries in "功能模块" section

**Files to Modify**:
- `CLAUDE.md`

---

### Task 2.5: Phase 2 Validation
**Priority**: Medium | **Estimated Time**: 5 minutes

**Steps**:
1. Verify all concept docs moved successfully
2. Test CLAUDE.md concept links
3. Ensure no duplicate references remain

**Success Criteria**:
- All concept docs consolidated
- CLAUDE.md fully updated
- No duplicate references

---

## Phase 3: Aggressive Cleanup

### Task 3.1: Create Archive Directory
**Priority**: Medium | **Estimated Time**: 2 minutes

**Steps**:
1. Create `docs/archive/prd/` directory

**Command**:
```bash
mkdir -p docs/archive/prd
```

---

### Task 3.2: Archive Historical PRDs
**Priority**: Medium | **Estimated Time**: 5 minutes

**Steps**:
1. Archive historical PRDs to `docs/archive/prd/`:
   ```bash
   git mv docs/PRD_V5_0-initial.md docs/archive/prd/
   git mv docs/PRD_V5_1-ai-integration.md docs/archive/prd/
   git mv docs/PRD_V5_2-flattened-screen.md docs/archive/prd/
   git mv docs/PRD_V5_3-unibrain-refactoring.md docs/archive/prd/
   git mv docs/PRD_V6_0-simulation-testing.md docs/archive/prd/
   ```

**Validation**:
```bash
ls -la docs/archive/prd/
```

---

### Task 3.3: Update CLAUDE.md PRD Section
**Priority**: Medium | **Estimated Time**: 10 minutes

**Steps**:
1. Update CLAUDE.md PRD section to reflect archived content
2. Update references to point to archive location
3. Ensure PRD_UNIFIED.md remains as primary reference

**Files to Modify**:
- `CLAUDE.md`

---

### Task 3.4: Archive Implemented Design Specs
**Priority**: Low | **Estimated Time**: 5 minutes

**Steps**:
1. Create `docs/superpowers/specs/archive/` directory
2. Archive implemented design specs:
   ```bash
   mkdir -p docs/superpowers/specs/archive
   git mv docs/superpowers/specs/2026-05-31-unibrain-design.md docs/superpowers/specs/archive/
   git mv docs/superpowers/specs/2026-06-02-trace-system-improvements.md docs/superpowers/specs/archive/
   git mv docs/superpowers/specs/2026-06-02-v6-executor-state-machine-simulator.md docs/superpowers/specs/archive/
   ```

---

### Task 3.5: Consolidate Testing Documentation
**Priority**: Low | **Estimated Time**: 15 minutes

**Steps**:
1. Review testing documentation for duplicates:
   - `docs/TEST_GUIDE.md`
   - `docs/TESTING_WORKFLOWS.md`
   - `docs/TESTING_ARCHITECTURE.md`
   - `docs/TESTING_DOCS_INDEX.md`
   - `docs/TESTING_FLOWCHARTS.md`
   - `docs/TESTING_QUICK_REFERENCE.md`
   - `docs/SIMULATION_TESTING_GUIDE.md`
2. Identify and remove redundant content
3. Ensure clear hierarchy and no duplication
4. Update CLAUDE.md testing section if needed

**Files to Review**:
- All testing documentation files
- `CLAUDE.md` testing section

---

### Task 3.6: Remove Empty Directories
**Priority**: Low | **Estimated Time**: 2 minutes

**Steps**:
1. Remove empty `docs/modules/` directory after successful migration
2. Verify no other empty directories remain

**Commands**:
```bash
# Check if empty
ls -la docs/modules/

# Remove if empty
rmdir docs/modules/
```

---

### Task 3.7: Final Cleanup Validation
**Priority**: High | **Estimated Time**: 15 minutes

**Steps**:
1. Verify all historical content archived
2. Ensure no duplicate documentation remains
3. Test all CLAUDE.md links
4. Verify directory structure is clean
5. Ensure archive structure is organized

**Validation Commands**:
```bash
# Check overall structure
ls -R docs/

# Verify no broken links (manual check)
# Review CLAUDE.md sections
```

**Success Criteria**:
- Historical PRDs archived
- No duplicate documentation
- All CLAUDE.md links working
- Clean directory structure
- Proper archive organization

---

## Final Validation

### Task 4.1: Comprehensive Link Testing
**Priority**: High | **Estimated Time**: 20 minutes

**Steps**:
1. Test every link in CLAUDE.md
2. Verify all architecture docs are accessible
3. Check for any remaining broken references
4. Validate archive structure

**Validation**:
- Manual review of CLAUDE.md
- Random sampling of module doc links
- Check archive directory contents

---

### Task 4.2: Git Status Review
**Priority**: High | **Estimated Time**: 5 minutes

**Steps**:
1. Review git status to ensure all moves were tracked properly
2. Verify no unintended changes
3. Check that CLAUDE.md modifications are correct

**Command**:
```bash
git status
git diff CLAUDE.md
```

---

### Task 4.3: Create Final Documentation
**Priority**: Low | **Estimated Time**: 10 minutes

**Steps**:
1. Update CLAUDE.md "最后更新" date
2. Add note about documentation reorganization if desired
3. Commit changes with descriptive message

**Commit Message Suggestion**:
```
docs: reorganize architecture and module documentation

- Consolidate all architecture docs under docs/architecture/
- Move 17 module design docs to docs/architecture/modules/
- Create docs/architecture/concepts/ for conceptual architecture
- Archive historical PRDs to docs/archive/prd/
- Update CLAUDE.md with comprehensive references
- Remove duplicate documentation

Phased migration approach (Structure → Consolidation → Cleanup)
```

---

## Rollback Plan

If issues arise during implementation, rollback can be performed by phase:

**Phase 1 Rollback**:
```bash
git mv docs/architecture/modules/* docs/modules/
git mv docs/architecture/ARCHITECTURE*.md docs/
git checkout CLAUDE.md  # Revert reference updates
rmdir docs/architecture/modules docs/architecture
```

**Phase 2 Rollback**:
```bash
git mv docs/architecture/concepts/* docs/
git checkout CLAUDE.md  # Revert reference updates
rmdir docs/architecture/concepts
```

**Phase 3 Rollback**:
```bash
git mv docs/archive/prd/* docs/
git mv docs/superpowers/specs/archive/* docs/superpowers/specs/
```

**Complete Rollback**:
```bash
git reset --hard HEAD~1  # If not yet pushed
```

---

**Total Estimated Time**: ~3 hours
**Risk Level**: Low (file moves with preservation and rollback capability)
**Dependencies**: None (can be executed independently)

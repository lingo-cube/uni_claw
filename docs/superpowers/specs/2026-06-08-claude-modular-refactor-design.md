# CLAUDE.md Modular Refactor Design

**Date**: 2026-06-08
**Status**: Design Approved (Expanded for Documentation Cleanup)
**Author**: Uni-Claw Team

**Scope**: Documentation cleanup + CLAUDE.md modular refactor

---

## Problem Statement

The current CLAUDE.md (390 lines) is inefficient for AI-driven development:

1. **Context overload** - Critical AI guidance buried under navigation tables
2. **Token inefficiency** - AI re-parses 390 lines each session, much of it navigation
3. **Maintenance burden** - Single file mixes stable reference with volatile status
4. **Discovery difficulty** - AI struggles to find relevant "how to work" guidance
5. **Staleness risk** - Large, monolithic file is hard to keep synchronized

**Goal**: Restructure CLAUDE.md to be AI-native for feature development workflows while supporting bug fixing and continuous project evolution.

---

## Expanded Scope: Documentation Cleanup

**Additional problem discovered**: The project has significant documentation chaos that will undermine the CLAUDE.md refactor if not addressed first.

### Documentation Issues Found

1. **PRD version chaos**: 10+ PRD files with inconsistent versioning (V6_1, V6_9.1, V6_9_2, V6_9), missing V6.5
2. **Test documentation overlap**: 6 test-related docs with unclear boundaries
3. **Archive directory missing**: CLAUDE.md references `docs/archive/prd/` which doesn't exist
4. **70+ documentation files**: No clear ownership or lifecycle

### Cleanup Strategy

**Priority**: Clean up while designing (边清理边设计) - integrate cleanup into the refactor workflow

**Decisions made**:
- PRD files: Archive all to `docs/archive/prd/`, keep only `PRD_UNIFIED.md`
- Test docs: Reorganize with clear responsibilities
- Approach: Incremental cleanup during refactor phases

---

## Solution: Modular File Structure

Split the monolithic CLAUDE.md into focused, single-responsibility files:

### New File Structure

```
uni_claw/
├── CLAUDE.md                    # Core AI guidelines (~100 lines)
├── CLAUDE_STATUS.md            # Project status & volatile info
├── CLAUDE_WORKFLOW.md          # Development workflows & commands
├── CLAUDE_CONVENTIONS.md       # Code patterns & conventions
└── docs/
    ├── INDEX.md                 # Complete documentation index
    └── [existing docs...]
```

### File Responsibilities

| File | Purpose | Size | Updated When |
|------|---------|------|--------------|
| `CLAUDE.md` | Essential AI context for every session | ~100 lines | Rarely (architecture changes) |
| `CLAUDE_STATUS.md` | Versions, active changes, verification status | ~50 lines | Frequently (each release/change) |
| `CLAUDE_WORKFLOW.md` | Commands, testing, development flow | ~60 lines | Occasionally (new tools/process) |
| `CLAUDE_CONVENTIONS.md` | Code patterns, naming, design principles | ~80 lines | Occasionally (new patterns) |
| `docs/INDEX.md` | Complete doc navigation | ~200 lines | Regularly (new docs added) |

---

## File Content Specifications

### 1. CLAUDE.md (Core, Always Loaded)

**Purpose**: Essential AI context for every session

**Structure**:
```markdown
# Uni-Claw AI Context

## Project Identity
- **What**: Mobile UI automation traversal framework, AI-driven
- **Tech Stack**: Python 3.10+, ADB, DeepSeek/Anthropic AI
- **Architecture Style**: Interface-driven, dependency injection, event-driven

## Core Design Principles (The "Rules")
1. **Interface-first** - Core components use Protocol/ABC interfaces
2. **Dependency injection** - All dependencies injected, never instantiated internally
3. **State separation** - State management independent of business logic
4. **Observability-first** - Built-in tracing, metrics, logging
5. **Simulation优先 (V6)** - Test without real devices
6. **Testing discovers problems** - Tests are for finding and solving real issues, not just passing

## Essential Module Map
| Module | Purpose | Key Files |
|--------|---------|-----------|
| AI服务 | Strategy decision | src/ai/ |
| Traversal | Core traversal logic | src/traversal/ |
| GraphEngine (V6) | Graph-based execution | src/traversal/graph_engine.py |
| Simulation (V6) | Offline testing | src/simulation/ |
| State | Persistence | src/state/, src/state_machine/ |
| Exception | Exception chain handling | src/exception/ |
| Observability | Trace/metrics/logs | src/trace/, src/analysis/ |

## Before You Work
1. Read relevant module README (see docs/INDEX.md for full list)
2. Follow code conventions in CLAUDE_CONVENTIONS.md
3. Check current status in CLAUDE_STATUS.md
4. Use workflow in CLAUDE_WORKFLOW.md

## File Placement Rules ⭐
**NEVER create files at project root** - Always use appropriate directories:

| File Type | Location | Examples |
|-----------|----------|----------|
| CLAUDE files | Project root | CLAUDE.md, CLAUDE_*.md |
| Documentation | `docs/` | docs/DESIGN.md, docs/INDEX.md |
| Architecture docs | `docs/architecture/` | docs/architecture/ARCHITECTURE.md |
| Testing docs | `docs/testing/` | docs/testing/STANDARDS.md |
| Spec documents | `docs/superpowers/specs/` | specs/YYYY-MM-DD-*.md |
| Scripts | `scripts/` | scripts/verify_docs.py |
| Test fixtures | `tests/fixtures/` | tests/fixtures/test_data.yaml |
| **ALL temporary files** | `temp/` (in .gitignore) | temp/tests/, temp/reports/, temp/verification/ |
| Module code | `src/<module>/` | src/ai/service.py |

**temp/ directory**:
- Contains ALL temporary/generated files
- In .gitignore (not committed)
- Can be deleted anytime
- Subdirectories: `temp/tests/`, `temp/reports/`, `temp/verification/`, `temp/analysis/`

**Before creating any file**:
1. Check if existing directory fits
2. Ask if uncertain
3. Never dump files at project root
4. **ALL temporary files go to `temp/` directory**

| File Type | Location | Examples |
|-----------|----------|----------|
| CLAUDE files | Project root | CLAUDE.md, CLAUDE_*.md |
| Documentation | `docs/` | docs/DESIGN.md, docs/INDEX.md |
| Architecture docs | `docs/architecture/` | docs/architecture/ARCHITECTURE.md |
| Testing docs | `docs/testing/` | docs/testing/STANDARDS.md |
| Spec documents | `docs/superpowers/specs/` | specs/YYYY-MM-DD-*.md |
| Scripts | `scripts/` | scripts/verify_docs.py |
| Test fixtures | `tests/fixtures/` | tests/fixtures/test_data.yaml |
| Module code | `src/<module>/` | src/ai/service.py |

**Before creating any file**:
1. Check if existing directory fits
2. Ask if uncertain
3. Never dump files at project root

## Quick Reference
- Full doc index: docs/INDEX.md
- Current status: CLAUDE_STATUS.md
- Workflow: CLAUDE_WORKFLOW.md
- Conventions: CLAUDE_CONVENTIONS.md
- Testing: docs/testing/README.md
```

**Design principles**:
- Signal-first: Most critical information at the top
- Link, don't duplicate: Reference other files
- Task-oriented: "Before You Work" gives clear starting point
- No navigation tables: Those live in docs/INDEX.md

---

### 2. CLAUDE_STATUS.md (Volatile, Updated Frequently)

**Purpose**: Project status, active changes, verification state

**Structure**:
```markdown
# Uni-Claw Project Status

## Current Version
- **Version**: V6.3
- **Last Updated**: 2026-06-08

## Active OpenSpec Changes
| Change | Status | Owner |
|--------|--------|-------|
| button-type-differentiation | Active | - |
| complete-prd-v5-implementation | Active | - |
| graph-state-trace-model | Active | - |

## Verification Status
- **V6 Implementation**: ✅ Complete (2026-06-05)
- **V6.3 Trace System**: ✅ Complete (2026-06-06)
- **Test Coverage**: 84/84 passing (100%)

## Known Issues
*Leave empty when no known issues. Add items as:*
- **[YYYY-MM-DD]** Issue description - Status
```

**Update frequency**: Every release, OpenSpec change, or significant milestone

---

### 3. CLAUDE_WORKFLOW.md (Development Process)

**Purpose**: Commands, workflows, testing philosophy

**Structure**:
```markdown
# Uni-Claw Development Workflow

## Starting Development
1. Read relevant architecture docs (see docs/INDEX.md)
2. Check CLAUDE_STATUS.md for active changes
3. Follow code conventions in CLAUDE_CONVENTIONS.md

## Common Commands
# Verification
python scripts/verify_refactor.py

# Testing
pytest tests/v6/ -v
pytest tests/models/ --cov=src --cov-report=term-missing

# Dashboard
python dashboards/simple_dashboard.py

## OpenSpec Workflow
/opsx:propose    # Create new change
/opsx:apply      # Implement tasks
/opsx:archive    # Finalize change
/opsx:explore    # Think through problems

## Testing Philosophy
Tests are for discovering and solving problems, not just passing.
- Write tests that verify real behavior
- Investigate root causes when tests fail
- Don't tweak tests just to make them pass
```

---

### 4. CLAUDE_CONVENTIONS.md (Code Patterns)

**Purpose**: Design patterns, naming, strong typing rules

**Structure**:
```markdown
# Uni-Claw Code Conventions

## Strong Typing (MANDATORY ⭐)

### All functions must have type annotations
```python
# ✅ Good: Full type annotations
def process_element(
    element: UIElement,
    context: TraversalContext
) -> Action:
    ...

# ❌ Bad: Missing types
def process_element(element, context):
    ...
```

### Use specific types, never `Any`
```python
# ✅ Good: Specific types
def get_nodes(graph: TraversalGraph) -> list[Node]:
    ...

# ❌ Bad: Vague types
def get_nodes(graph) -> Any:
    ...
```

### Generic types with proper bounds
```python
from typing import TypeVar, Protocol

T = TypeVar('T', bound=Comparable)

def sort_items(items: list[T]) -> list[T]:
    ...
```

### Return types must be explicit
```python
# ✅ Good: Explicit return
def find_element(path: str) -> UIElement | None:
    ...

# ❌ Bad: Implicit return
def find_element(path):  # No return type
    ...
```

## Design Patterns

### Interface-First
```python
from typing import Protocol

class MyService(Protocol):
    def do_work(self) -> Result: ...
```

### Dependency Injection
```python
# ✅ Good: Injected
def __init__(self, ai_service: AIService, adb: ADBClient):
    self._ai = ai_service
    self._adb = adb

# ❌ Bad: Instantiated internally
def __init__(self):
    self._ai = AIService()  # Don't do this
```

## Naming Conventions
- Interfaces: `IFoo`, `FooProtocol`
- Implementations: `FooService`, `FooImpl`
- Exceptions: `FooError`, `FooException`

## File Organization
- One major class per file
- Module README explains purpose
- Tests mirror src structure

## Testing Conventions
- Test fixtures in `tests/fixtures/`
- Mock services in `src/simulation/mock_*.py`
- Test names describe the scenario: `test_[scenario]_[expected_result]`

## Temporary Files
**ALL temporary files go to `temp/` directory**:
- **DO** use `temp/` for anything temporary
- **DO** organize by subdirectory: `temp/tests/`, `temp/reports/`, `temp/verification/`, `temp/analysis/`
- **DON'T** create temporary files anywhere else
- **DO** delete entire `temp/` when done

**Characteristics**:
- `temp/` is in .gitignore
- Everything under `temp/` is ephemeral
- Can be deleted anytime without loss

**If a temp file becomes valuable**:
- Move it to appropriate permanent location
- Give it a proper name
- Remove from `temp/`

## File Placement Conventions ⭐
**CRITICAL**: Never create files at project root. Always use appropriate directories.

| File Type | Location |
|-----------|----------|
| CLAUDE config | Project root (CLAUDE.md only) |
| Documentation | docs/ |
| Specs | docs/superpowers/specs/ |
| Scripts | scripts/ |
| Tests | tests/ |
| Fixtures | tests/fixtures/ |
| Source code | src/ |

**Before `Write` tool**: Verify directory exists and is appropriate.
```

---

### 5. docs/INDEX.md (Complete Navigation)

**Purpose**: Comprehensive documentation index (moved from CLAUDE.md)

**Structure**: Contains all navigation tables from current CLAUDE.md:
- Architecture docs
- Module design docs (17+ modules)
- Testing documentation
- PRD documentation with version history
- API documentation

**Size**: ~200 lines (all navigation content from current CLAUDE.md)

---

## AI Workflow Integration

### Session Start Loading Pattern

```
┌─────────────────────────────────────────────────────────────┐
│                    AI Session Start                          │
├─────────────────────────────────────────────────────────────┤
│  1. Load CLAUDE.md           (Always, ~100 lines)           │
│     → Get project identity, principles, module map         │
├─────────────────────────────────────────────────────────────┤
│  2. Load CLAUDE_STATUS.md     (For context, ~50 lines)      │
│     → Get current version, active changes, known issues      │
├─────────────────────────────────────────────────────────────┤
│  3. Task-specific loading:                                   │
│     • Feature dev → Load CLAUDE_WORKFLOW.md + module README  │
│     • Bug fix → Load CLAUDE_CONVENTIONS.md + exception docs │
│     • Code exploration → Load docs/INDEX.md + target doc    │
└─────────────────────────────────────────────────────────────┘

During work:
- Load files as needed for the task
- Don't worry about re-reading if context suggests it's useful
- Focus on having the right information, not token optimization
```

### Typical Task Patterns

| Task Type | Files Loaded | Estimated Lines |
|-----------|--------------|-----------------|
| Quick question | CLAUDE.md | ~100 |
| Feature development | CLAUDE.md + STATUS + WORKFLOW + module README | ~250 |
| Bug fixing | CLAUDE.md + CONVENTIONS + exception docs | ~230 |
| Architecture exploration | CLAUDE.md + INDEX.md + specific doc | ~350 |

**Improvement**: Core context reduced from 390 to ~100 lines. Task-specific files loaded on-demand.

---

## Maintenance Strategy

### Update Responsibilities

| File | Update Trigger | Who Updates |
|------|---------------|-------------|
| CLAUDE.md | Architecture changes | Senior dev |
| CLAUDE_STATUS.md | Each release/change | CI/auto or dev |
| CLAUDE_WORKFLOW.md | New tools/process | Dev team |
| CLAUDE_CONVENTIONS.md | New patterns discovered | Dev team |
| docs/INDEX.md | New docs added | Doc owner |

### Maintenance Rules

1. **CLAUDE.md changes should be rare** - Only when core architecture or philosophy changes
2. **STATUS.md updated frequently** - With every OpenSpec change or milestone
3. **Never duplicate content** - Link across files, don't repeat
4. **One responsibility per file** - If a file starts doing two things, split it

---

## Success Criteria

The refactoring is successful when:

1. **Token efficiency** - Core context (CLAUDE.md) reduced to ~100 lines from 390; task-specific loading reduces initial parse
2. **Discovery speed** - AI can find relevant docs in <2 file reads
3. **Maintenance** - Updating one section (e.g., status) doesn't require touching others (e.g., principles)
4. **No information loss** - All existing content preserved, just reorganized
5. **AI effectiveness** - AI can start feature work with minimal file loading

---

## Migration Strategy

### Phase 0: Documentation Cleanup & Normalization

**Goal**: Clean documentation foundation before restructuring CLAUDE.md

#### 0.1 PRD Reorganization
1. Create directories:
   - `docs/prd/` - For current V6 PRDs
   - `docs/archive/prd/` - For V5 and older PRDs

2. Move PRDs by version:
   - `docs/PRD_V6_*.md` → `docs/prd/PRD_V6_*.md` (keep current)
   - `docs/PRD_V5_*.md`, `docs/PRD_V4_*.md`, etc. → `docs/archive/prd/` (archive)

3. Keep:
   - `docs/PRD_UNIFIED.md` - Remains as unified entry point

4. Result structure:
   ```
   docs/
   ├── prd/
   │   ├── PRD_V6_1-*.md
   │   ├── PRD_V6_2-*.md
   │   └── ...
   ├── archive/
   │   └── prd/          (V5, V4, etc.)
   └── PRD_UNIFIED.md
   ```

#### 0.2 Testing Documentation Reorganization
**Current problem**: 6 test docs with unclear boundaries
- `docs/TEST_GUIDE.md`
- `docs/TESTING_QUICK_REFERENCE.md`
- `docs/TESTING_FLOWCHARTS.md`
- `docs/TESTING_DOCS_INDEX.md`
- `docs/TESTING_STANDARDS.md`
- `docs/TESTING_WORKFLOWS.md`

**New directory structure**:
```
docs/
└── testing/
    ├── README.md              # = TEST_GUIDE.md (总入口)
    ├── STANDARDS.md           # 质量标准
    ├── WORKFLOWS.md          # 工作流和模式
    └── QUICK_REFERENCE.md    # 快速查询
```

**New structure with clear responsibilities**:

| File | Role | When to Use | Target Audience |
|------|------|-------------|-----------------|
| `docs/testing/README.md` | **总入口** - 完整测试指南 | First time learning | New developers |
| `docs/testing/STANDARDS.md` | **规范** - 测试质量标准 | Before writing tests, code review | All developers |
| `docs/testing/WORKFLOWS.md` | **实践** - 工作流和模式 | During testing, troubleshooting | Developers testing |
| `docs/testing/QUICK_REFERENCE.md` | **速查** - 命令和 fixtures | Quick lookup during work | Quick context |

**Actions**:
1. Create `docs/testing/` directory
2. Move/rename files:
   - `TEST_GUIDE.md` → `docs/testing/README.md` (update as total entry point)
   - `TESTING_STANDARDS.md` → `docs/testing/STANDARDS.md`
   - `TESTING_WORKFLOWS.md` → `docs/testing/WORKFLOWS.md`
   - `TESTING_QUICK_REFERENCE.md` → `docs/testing/QUICK_REFERENCE.md`
3. Archive/delete redundant docs:
   - `TESTING_DOCS_INDEX.md` → Delete (will be replaced by docs/INDEX.md)
   - `TESTING_FLOWCHARTS.md` → Merge into WORKFLOWS.md or delete if outdated
4. Update all cross-references to new paths
5. Add testing section to docs/INDEX.md pointing to docs/testing/

#### 0.3 Temporary/Process Doc Cleanup
Archive or delete process docs that are no longer relevant:
- `docs/DEPENDENCY_FIX.md` → Archive (was a fix, now resolved)
- `docs/EXPECTEDBEHAVIOR_YAML_REFERENCE.md` → Keep if active reference
- `docs/PROBLEM_DETECTOR_REFERENCE.md` → Evaluate relevance
- `docs/PAGEANALYSIS_FIELD_MAPPING.md` → Evaluate relevance

#### 0.4 Validation Docs Consolidation
Consolidate `docs/validation/` files:
- Keep: `final_report.md`, `system_infrastructure_analysis.md`
- Archive/merge: Progress reports, cumulative guides

---

### Phase 1: Create Modular Files
1. Create `docs/INDEX.md` with all navigation content
2. Create `CLAUDE_STATUS.md` with status/volatile content
3. Create `CLAUDE_WORKFLOW.md` with workflow content
4. Create `CLAUDE_CONVENTIONS.md` with conventions content
5. Create `temp/` directory and add to `.gitignore`
   - Create subdirectories: `temp/tests/`, `temp/reports/`, `temp/verification/`, `temp/analysis/`

### Phase 2: Rewrite CLAUDE.md
1. Replace current CLAUDE.md with new ~100 line version
2. Keep only essential identity, principles, module map
3. Add cross-references to new files

### Phase 3: Create Maintenance Scripts
**Goal**: Establish automated checks to prevent documentation degradation

#### 3.1 scripts/verify_docs.py
Main verification script for document structure compliance.

**Checks**:
- CLAUDE modular files exist (5 files)
- Testing structure correct (`docs/testing/` with 4 files)
- PRD structure correct: `docs/prd/` contains V6 PRDs, `docs/archive/prd/` contains older
- No orphaned PRD files in `docs/` root (only PRD_UNIFIED.md should be there)
- No broken internal links
- All docs have `last_updated` metadata
- **No rogue files at project root** (except CLAUDE_*.md, README.md, .gitignore, etc.)
- **All temporary files in `temp/`** (no scattered temp files elsewhere)
- **`temp/` in .gitignore**

**Usage**:
```bash
python scripts/verify_docs.py
# Exit code 1 if violations found
```

#### 3.2 scripts/doc_freshness.py
Scan for potentially outdated documents.

**Checks**:
- Docs not updated in >90 days
- Docs with `last_updated` older than related code changes
- Docs with deprecated/draft status >30 days

**Usage**:
```bash
python scripts/doc_freshness.py --days=90
```

#### 3.3 scripts/doc_audit.py
Comprehensive monthly audit.

**Output**: `docs/reports/doc_audit_YYYY-MM-DD.md`

### Phase 4: Validation (原 Phase 3, Enhanced)
1. **Run module tests** - Use `module-test` skill to verify all tests pass
2. **Generate validation report** - Use `validation-documentation` skill to create standardized report
3. **Run AI session scenarios** - Test typical workflows (feature dev, bug fix, exploration)
4. **Verify content preservation** - Ensure all original content accounted for
5. **Test discoverability** - Confirm AI can find key information in <2 hops

### Phase 5: Establish Maintenance Processes
**Goal**: Ensure long-term documentation health

#### 5.1 Update CLAUDE_CONVENTIONS.md
Add documentation conventions section:
- File naming rules
- Where to put new docs
- Metadata requirements (last_updated, status)
- When to update docs

#### 5.2 Update CLAUDE_WORKFLOW.md
Add AI documentation workflow:
- AI should update docs when changing code
- Run verify_docs.py before committing
- How to handle doc references

#### 5.3 Setup Automation
- Add verify_docs.py to pre-commit hooks (optional)
- Add monthly doc_audit.py to calendar
- Document in project maintenance guide

### Phase 6: Archive & Finalize
1. Archive old CLAUDE.md to `docs/archive/CLAUDE.md.pre-refactor`
2. Commit all changes with descriptive message
3. Generate final validation report
4. Document migration in project notes

---

---

## Documentation Maintenance Mechanisms

To prevent documentation from degrading over time, the following mechanisms are established:

### 1. Document Metadata Standards

Every document MUST include:

```markdown
---
title: Document Title
last_updated: YYYY-MM-DD
version: X.Y
status: active | deprecated | archived | draft
maintainer: [optional]
related_code: path/to/code/ [optional]
---
```

**Status meanings**:
- `active` - Current, regularly maintained
- `deprecated` - Will be removed, refer to alternative
- `archived` - Historical reference, not maintained
- `draft` - Work in progress

### 2. Automated Verification

**Continuous checks** (scripts/verify_docs.py):
- Structure compliance
- No broken links
- Required metadata present

**Scheduled checks** (scripts/doc_freshness.py):
- 90+ day stale detection
- Code-doc sync mismatch detection

### 3. Monthly Audit

Run `scripts/doc_audit.py` monthly to generate comprehensive report covering:
- Structure freshness
- Link health
- Code-doc coverage
- Naming convention compliance

### 4. Developer Workflow

**When adding new docs**:
1. Check if existing doc covers topic
2. Follow naming conventions (kebab-case)
3. Include required metadata
4. Update relevant INDEX files
5. Run `verify_docs.py` before committing

**When AI makes code changes**:
1. Update related documentation if architecture changes
2. Update references if files move
3. Run `verify_docs.py` before committing

### 5. Update Triggers

Documentation should be reviewed/updated when:
- Major feature added
- Module architecture changes
- File structure reorganized
- Deprecation/removal occurs
- Quarterly review (for core docs)

---

## Expanded Scope Summary

**What changed**:
- Added **Phase 0: Documentation Cleanup & Normalization** (~1-2 days)
- Added **Phase 3: Maintenance Scripts** (~1 day)
- Added **Phase 5: Maintenance Processes** (~0.5 day)
- Updated to 6 phases total
- Testing docs reorganization with `docs/testing/` structure
- PRD archive process
- Documentation metadata standards
- Long-term maintenance mechanisms

**New timeline estimate**:
- Phase 0: ~1-2 days (documentation cleanup)
- Phase 1: ~1 day (create modular files)
- Phase 2: ~1 day (rewrite CLAUDE.md)
- Phase 3: ~1 day (maintenance scripts)
- Phase 4: ~0.5 day (validation)
- Phase 5: ~0.5 day (maintenance processes)
- Phase 6: ~0.5 day (archive & finalize)
- **Total: ~5-7 days**

**Risk**: Phase 0 may discover more documentation issues requiring additional work.

**Success criteria**:
1. Documentation structure follows conventions
2. All tests pass
3. AI can find information efficiently
4. Maintenance scripts operational
5. Validation report confirms completion

---

## Open Questions / Future Considerations

1. **Should STATUS.md be auto-generated?** - Could pull from git tags and OpenSpec metadata
2. **Should we add CLAUDE_TESTING.md?** - If testing conventions grow beyond conventions
3. **Should INDEX.md be split by topic?** - If it grows beyond ~300 lines
4. **How to handle stale cross-references?** - Consider adding a verification script

---

## Appendix: Current CLAUDE.md Content Audit

| Content Category | Lines | Destination File |
|------------------|-------|-------------------|
| Project identity & principles | ~50 | CLAUDE.md (keep) |
| Module maps | ~40 | CLAUDE.md (keep) |
| Navigation tables | ~150 | docs/INDEX.md |
| Status & OpenSpec | ~50 | CLAUDE_STATUS.md |
| Workflow & commands | ~40 | CLAUDE_WORKFLOW.md |
| Conventions (implicit) | ~20 | CLAUDE_CONVENTIONS.md (expand) |
| Doc contribution guidelines | ~30 | Remove (out of scope for AI context) |
| Historical notes (文档重组说明) | ~50 | Archive (docs/archive/CLAUDE_HISTORY.md) |

**Total**: 390 lines → ~100 lines in CLAUDE.md + ~290 lines distributed

---

**End of Design Document**

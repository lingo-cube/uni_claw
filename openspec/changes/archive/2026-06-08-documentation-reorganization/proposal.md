# Documentation Reorganization Proposal

> **Change**: documentation-reorganization
> **Created**: 2026-06-04
> **Type**: Documentation Reorganization

---

## What

Reorganize all architecture and module documentation into a centralized `docs/architecture/` structure using a phased migration approach, followed by comprehensive cleanup of obsolete and duplicate documentation.

**Scope**:
- Consolidate 17 module design documents from `docs/modules/` into `docs/architecture/modules/`
- Move architecture overview documents to `docs/architecture/`
- Create dedicated `docs/architecture/concepts/` for conceptual architecture documents
- Archive 5 historical PRD documents to `docs/archive/prd/`
- Update CLAUDE.md to properly reference the new documentation structure
- Remove duplicate and obsolete documentation

## Why

**Current Problems**:
1. **Poor Discoverability**: Module design docs are hidden in `docs/modules/` but not well-referenced in CLAUDE.md
2. **Inconsistent Organization**: Architecture documents mixed with PRDs, testing docs, and other content in main `docs/`
3. **Historical Clutter**: 5 outdated PRD versions (V5.0-V6.0) clutter the main documentation area
4. **Missing Integration**: CLAUDE.md doesn't leverage the comprehensive module documentation that already exists
5. **Maintenance Difficulties**: Inconsistent structure makes documentation updates error-prone

**Benefits**:
1. **Improved Navigation**: All architecture content in one logical location
2. **Better CLAUDE.md Integration**: Comprehensive references to existing high-quality module docs
3. **Historical Clarity**: Clear separation between current documentation and archived historical content
4. **Easier Maintenance**: Consistent structure simplifies future updates
5. **Reduced Confusion**: No duplicate documentation, clear hierarchy between overview, modules, and concepts

## Expected Outcomes

**Immediate Benefits**:
- Developers can quickly find architecture documentation for any module
- CLAUDE.md becomes a comprehensive navigation hub for all design documentation
- Clear separation between current active documentation and historical references

**Long-term Benefits**:
- Easier onboarding for new developers through better documentation organization
- Simplified maintenance and updates to documentation
- Better alignment between code structure and documentation structure
- Reduced cognitive load when navigating project documentation

## Non-Goals

**Out of Scope**:
- Content rewriting or reorganization of the module documentation itself
- Changes to actual code structure
- Creation of new documentation (only reorganization of existing docs)
- Changes to testing documentation beyond de-duplication
- Modifications to README.md or other non-architecture documentation

## Success Criteria

1. **Navigation**: All architecture and module documentation accessible from `docs/architecture/`
2. **References**: CLAUDE.md accurately references all reorganized documentation
3. **Link Integrity**: No broken internal links or dead references
4. **Archive**: Historical PRDs preserved in `docs/archive/prd/` but removed from main docs area
5. **Cleanup**: Duplicate documentation removed, single source of truth for each topic
6. **Hierarchy**: Clear distinction between overview, modules, and concepts documentation

## Risk Assessment

**Low Risk Changes**:
- Moving documents doesn't change content
- Historical content archived, not deleted
- Phased approach allows validation at each step

**Mitigation Strategies**:
- Three-phase migration allows rollback if issues arise
- All historical content preserved in archive/ directory
- Link verification after each phase
- Git history allows easy rollback if needed

---

**Proposed by**: Claude (based on user requirements and design brainstorming)
**Status**: Ready for design and implementation planning

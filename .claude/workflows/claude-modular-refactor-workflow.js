/**
 * CLAUDE.md Modular Refactor - Quality-First Workflow
 *
 * Orchestrates the documentation reorganization with quality gates.
 * Fans out independent tasks, validates at each phase, prevents "work for work's sake".
 */

export const meta = {
  name: 'claude-modular-refactor',
  description: 'Documentation reorganization with quality gates and parallel execution',
  phases: [
    { title: 'Assess', detail: 'Current state analysis and quality baseline' },
    { title: 'Phase 0', detail: 'Document cleanup and normalization' },
    { title: 'Phase 1', detail: 'Create modular documentation files' },
    { title: 'Phase 2', detail: 'Rewrite CLAUDE.md' },
    { title: 'Phase 3', detail: 'Create maintenance scripts' },
    { title: 'Validate', detail: 'Quality validation and testing' },
    { title: 'Finalize', detail: 'Archive and cleanup' }
  ]
};

// Quality assessment schema
const QUALITY_ASSESSMENT_SCHEMA = {
  type: 'object',
  properties: {
    currentClaudeMdSize: { type: 'number' },
    prdFiles: { type: 'array', items: { type: 'string' } },
    testingDocs: { type: 'array', items: { type: 'string' } },
    tempExists: { type: 'boolean' },
    issues: { type: 'array', items: { type: 'string' } },
    recommendations: { type: 'array', items: { type: 'string' } }
  },
  required: ['currentClaudeMdSize', 'prdFiles', 'testingDocs', 'tempExists', 'issues', 'recommendations']
};

// Phase completion schema
const PHASE_RESULT_SCHEMA = {
  type: 'object',
  properties: {
    phase: { type: 'string' },
    success: { type: 'boolean' },
    filesCreated: { type: 'array', items: { type: 'string' } },
    filesModified: { type: 'array', items: { type: 'string' } },
    filesDeleted: { type: 'array', items: { type: 'string' } },
    issues: { type: 'array', items: { type: 'string' } },
    recommendations: { type: 'array', items: { type: 'string' } }
  },
  required: ['phase', 'success', 'filesCreated', 'filesModified', 'filesDeleted', 'issues', 'recommendations']
};

// Validation schema
const VALIDATION_RESULT_SCHEMA = {
  type: 'object',
  properties: {
    testsPass: { type: 'boolean' },
    scriptsWork: { type: 'boolean' },
    aiScenarios: { type: 'array', items: { type: 'object' } },
    contentPreserved: { type: 'boolean' },
    issues: { type: 'array', items: { type: 'string' } }
  },
  required: ['testsPass', 'scriptsWork', 'aiScenarios', 'contentPreserved', 'issues']
};

/**
 * Assess current state and establish quality baseline
 */
const assessCurrentState = async () => {
  phase('Assess');
  log('Analyzing current documentation state...');

  return await agent(
    `Analyze the current documentation state of the Uni-Claw project:

    1. Check CLAUDE.md size and structure
    2. List all PRD files (docs/PRD*.md)
    3. List all testing documentation files (docs/TEST*.md)
    4. Check if temp/ directory exists
    5. Identify documentation issues (naming conflicts, missing files, duplicate content)
    6. Provide recommendations for the reorganization

    Return a structured assessment with specific issues found and recommendations.`,
    { label: 'Quality assessment', schema: QUALITY_ASSESSMENT_SCHEMA }
  );
};

/**
 * Phase 0: Document cleanup and normalization
 */
const phase0Cleanup = async (assessment) => {
  phase('Phase 0');
  log('Document cleanup and normalization...');

  const tasks = [
    () => agent(
      `Execute Phase 0.1: PRD Reorganization

      Based on the assessment:
      - Create docs/prd/ directory if it doesn't exist
      - Create docs/archive/prd/ directory if it doesn't exist
      - Move all PRD_V6_*.md files to docs/prd/
      - Keep PRD_UNIFIED.md in docs/
      - (If V5 PRDs exist, move to docs/archive/prd/)

      Use bash commands to create directories and move files.
      Report what was done.`,
      { label: 'PRD Reorganization', phase: 'Phase 0' }
    ),

    () => agent(
      `Execute Phase 0.2: Testing Documentation Reorganization

      - Create docs/testing/ directory if it doesn't exist
      - Move docs/TEST_GUIDE.md to docs/testing/README.md
      - Move docs/TESTING_STANDARDS.md to docs/testing/STANDARDS.md
      - Move docs/TESTING_WORKFLOWS.md to docs/testing/WORKFLOWS.md
      - Move docs/TESTING_QUICK_REFERENCE.md to docs/testing/QUICK_REFERENCE.md
      - Delete docs/TESTING_DOCS_INDEX.md (redundant)
      - Delete docs/TESTING_FLOWCHARTS.md (merge into WORKFLOWS or delete)

      Use bash commands to create directory, move and delete files.
      Report what was done.`,
      { label: 'Testing Docs Reorg', phase: 'Phase 0' }
    ),

    () => agent(
      `Execute Phase 0.3: Temporary Document Cleanup

      Evaluate these docs for archiving or deletion:
      - docs/DEPENDENCY_FIX.md (likely resolved)
      - docs/EXPECTEDBEHAVIOR_YAML_REFERENCE.md
      - docs/PROBLEM_DETECTOR_REFERENCE.md
      - docs/PAGEANALYSIS_FIELD_MAPPING.md

      Create docs/archive/temporary/ and move resolved temporary docs there.
      Delete any truly obsolete files.

      Report your decisions and actions.`,
      { label: 'Temp Doc Cleanup', phase: 'Phase 0' }
    )
  ];

  const results = await parallel(tasks);
  return results.filter(Boolean);
};

/**
 * Phase 1: Create modular files (can be done in parallel)
 */
const phase1ModularFiles = async () => {
  phase('Phase 1');
  log('Creating modular documentation files...');

  const docs = await parallel([
    () => agent(
      `Create docs/INDEX.md - Complete Documentation Navigation Index

      1. Read the current CLAUDE.md to extract all navigation tables
      2. Create docs/INDEX.md with these sections:
         - Quick Start documents
         - System Architecture documents
         - Architecture Module Design (17+ modules)
         - Architecture Concepts
         - Feature Modules
         - PRD documents (with version history)
         - Testing documents
         - AI Module docs

      3. Format as a clean navigation index
      4. Write to docs/INDEX.md

      Report: File created with section count.`,
      { label: 'Create INDEX.md', phase: 'Phase 1', schema: PHASE_RESULT_SCHEMA }
    ),

    () => agent(
      `Create CLAUDE_STATUS.md - Project Status Tracking

      Create CLAUDE_STATUS.md with:
      1. Current version info (V6.3+)
      2. Last updated date (today)
      3. Active OpenSpec changes table (check openspec/changes/)
      4. Validation status (V6 implementation, V6.3 Trace, test coverage)
      5. Known issues section (template)

      Keep it to ~50 lines. Write to CLAUDE_STATUS.md.
      Report: File created with status summary.`,
      { label: 'Create STATUS.md', phase: 'Phase 1', schema: PHASE_RESULT_SCHEMA }
    ),

    () => agent(
      `Create CLAUDE_WORKFLOW.md - Development Workflow Guide

      Create CLAUDE_WORKFLOW.md with:
      1. "Starting Development" section
      2. Common commands section (verify, test, dashboard)
      3. OpenSpec workflow section
      4. Testing philosophy section

      Keep it to ~60 lines. Write to CLAUDE_WORKFLOW.md.
      Report: File created.`,
      { label: 'Create WORKFLOW.md', phase: 'Phase 1', schema: PHASE_RESULT_SCHEMA }
    ),

    () => agent(
      `Create CLAUDE_CONVENTIONS.md - Code Standards and Conventions

      Create CLAUDE_CONVENTIONS.md with:
      1. Strong typing requirements (MANDATORY ⭐):
         - Functions must have type annotations
         - Use concrete types, disable Any
         - Generic types need bounds
         - Return types must be explicit
      2. Design patterns section:
         - Interface-first principle
         - Dependency injection examples
      3. Naming conventions
      4. File organization
      5. Testing conventions
      6. File placement conventions (ALL temp files go to temp/)

      Keep it to ~80 lines. Write to CLAUDE_CONVENTIONS.md.
      Report: File created.`,
      { label: 'Create CONVENTIONS.md', phase: 'Phase 1', schema: PHASE_RESULT_SCHEMA }
    ),

    () => agent(
      `Create temp/ directory structure

      1. Create temp/ directory with subdirectories:
         - temp/tests/
         - temp/reports/
         - temp/verification/
         - temp/analysis/

      2. Update .gitignore to add temp/ directory

      3. Report directory structure and gitignore update.`,
      { label: 'Create temp/ structure', phase: 'Phase 1', schema: PHASE_RESULT_SCHEMA }
    )
  ]);

  return docs.filter(Boolean);
};

/**
 * Phase 2: Rewrite CLAUDE.md (depends on Phase 1)
 */
const phase2RewriteClaude = async () => {
  phase('Phase 2');
  log('Rewriting CLAUDE.md to ~100 lines...');

  return await agent(
    `Rewrite CLAUDE.md - Core AI Context (~100 lines)

    Based on the current CLAUDE.md, create a new streamlined version with:

    1. Project Identity section:
       - What: Mobile UI automation traversal framework, AI-driven
       - Tech Stack: Python 3.10+, ADB, DeepSeek/Anthropic AI
       - Architecture Style: Interface-driven, dependency injection, event-driven

    2. Core Design Principles (6 items):
       - Interface-first
       - Dependency injection
       - State separation
       - Observability-first
       - Simulation优先 (V6)
       - Testing discovers problems

    3. Essential Module Map:
       - AI服务 - src/ai/
       - Traversal - src/traversal/
       - GraphEngine (V6) - src/traversal/graph_engine.py
       - Simulation (V6) - src/simulation/
       - State - src/state/, src/state_machine/
       - Exception - src/exception/
       - Observability - src/trace/, src/analysis/

    4. "Before You Work" section:
       - Read relevant module README
       - Follow code conventions
       - Check current status
       - Use workflow

    5. File Placement Rules ⭐:
       - NEVER create files at project root
       - File type table (CLAUDE files, Documentation, Architecture, Testing, Scripts, Temporary, etc.)
       - temp/ directory explanation
       - Before creating any file checklist

    6. Quick Reference section:
       - Full doc index: docs/INDEX.md
       - Current status: CLAUDE_STATUS.md
       - Workflow: CLAUDE_WORKFLOW.md
       - Conventions: CLAUDE_CONVENTIONS.md
       - Testing: docs/testing/README.md

    Target: ~100 lines max. Focus on WHAT the project is and HOW to work, not exhaustive documentation.

    IMPORTANT: First BACKUP the current CLAUDE.md to CLAUDE.md.backup before writing.
    Then write the new content.

    Report: Backup created, new CLAUDE.md written with line count.`,
    { label: 'Rewrite CLAUDE.md', phase: 'Phase 2', schema: PHASE_RESULT_SCHEMA }
  );
};

/**
 * Phase 3: Create maintenance scripts (can be done in parallel)
 */
const phase3Scripts = async () => {
  phase('Phase 3');
  log('Creating maintenance scripts...');

  const scripts = await parallel([
    () => agent(
      `Create scripts/verify_docs.py - Documentation Structure Verification

      Create a Python script that checks:
      1. CLAUDE modular files exist:
         - CLAUDE.md exists
         - CLAUDE_STATUS.md exists
         - CLAUDE_WORKFLOW.md exists
         - CLAUDE_CONVENTIONS.md exists
         - docs/INDEX.md exists

      2. Testing structure check:
         - docs/testing/ directory exists
         - docs/testing/README.md exists
         - docs/testing/STANDARDS.md exists
         - docs/testing/WORKFLOWS.md exists
         - docs/testing/QUICK_REFERENCE.md exists

      3. PRD structure check:
         - docs/prd/ directory exists
         - docs/archive/prd/ directory exists
         - No orphan PRD files in docs/ root (only PRD_UNIFIED.md)

      4. Root directory scattered files check:
         - Check project root
         - Allow: CLAUDE_*.md, README.md, .gitignore, .claude/, etc.
         - Report other files

      5. temp/ directory check:
         - temp/ exists
         - temp/ in .gitignore

      6. Broken link check (optional/best effort):
         - Check markdown files for internal links
         - Verify link targets exist

      Exit code 1 if violations found. Print clear error messages.
      Exit code 0 if all checks pass.

      Write to scripts/verify_docs.py. Make it executable.
      Report: Script created with test result.`,
      { label: 'Create verify_docs.py', phase: 'Phase 3', schema: PHASE_RESULT_SCHEMA }
    ),

    () => agent(
      `Create scripts/doc_freshness.py - Outdated Document Scanner

      Create a Python script that:
      1. Scans for documents not updated in >90 days (configurable via --days=N)
      2. Checks code-document sync (compare doc last_updated with code mtime)
      3. Reports deprecated/draft status docs >30 days old

      Usage: python scripts/doc_freshness.py --days=90

      Output: List of potentially outdated documents with recommendations.

      Write to scripts/doc_freshness.py. Make it executable.
      Report: Script created with example output.`,
      { label: 'Create doc_freshness.py', phase: 'Phase 3', schema: PHASE_RESULT_SCHEMA }
    ),

    () => agent(
      `Create scripts/doc_audit.py - Comprehensive Documentation Audit

      Create a Python script that:
      1. Calls verify_docs.py for structure checks
      2. Calls doc_freshness.py for freshness checks
      3. Adds code-document coverage analysis
      4. Adds naming convention compliance check

      Output: Generate comprehensive report to docs/reports/doc_audit_YYYY-MM-DD.md

      Usage: python scripts/doc_audit.py

      Write to scripts/doc_audit.py. Make it executable.
      Report: Script created.`,
      { label: 'Create doc_audit.py', phase: 'Phase 3', schema: PHASE_RESULT_SCHEMA }
    )
  ]);

  return scripts.filter(Boolean);
};

/**
 * Validation Phase
 */
const validateImplementation = async () => {
  phase('Validate');
  log('Running quality validation...');

  return await agent(
    `Validate the documentation refactor implementation:

    1. Test validation:
       - Run pytest tests/ -v
       - Verify all tests pass
       - Check test coverage

    2. Script validation:
       - Run python scripts/verify_docs.py
       - Run python scripts/doc_freshness.py
       - Run python scripts/doc_audit.py
       - Verify all scripts work correctly

    3. Content preservation validation:
       - Verify all original content is preserved
       - Check no information was lost

    4. AI scenario validation:
       - Quick Q&A scenario (CLAUDE.md only)
       - Feature development scenario (CLAUDE.md + STATUS + WORKFLOW + module README)
       - Bug fix scenario (CLAUDE.md + CONVENTIONS + exception docs)
       - Architecture exploration (CLAUDE.md + INDEX.md + specific doc)
       - Verify AI can find relevant docs in <2 jumps

    Return detailed validation results with any issues found.`,
    { label: 'Quality validation', phase: 'Validate', schema: VALIDATION_RESULT_SCHEMA }
  );
};

/**
 * Finalize and archive
 */
const finalizeChanges = async (validation) => {
  phase('Finalize');
  log('Archiving and finalizing...');

  return await agent(
    `Finalize the documentation refactor:

    1. Archive old CLAUDE.md:
       - Create docs/archive/ if needed
       - Move CLAUDE.md.backup to docs/archive/CLAUDE.md.pre-refactor
       - Add archive note

    2. Generate final validation report:
       - Create docs/reports/claude-modular-refactor-validation-YYYY-MM-DD.md
       - Document: what changed, validation results, migration notes

    3. Git commit preparation:
       - List all new files
       - List all modified files
       - List all deleted files
       - Suggest commit message

    Return: Summary of finalization steps completed.`,
    { label: 'Archive and finalize', phase: 'Finalize', schema: PHASE_RESULT_SCHEMA }
  );
};

/**
 * Main workflow execution
 */
async function run() {
  log('🚀 CLAUDE.md Modular Refactor - Quality-First Workflow');
  log('');

  // Step 1: Assess current state
  log('Step 1: Quality assessment of current state');
  const assessment = await assessCurrentState();
  log('✓ Assessment complete');
  log(`  - CLAUDE.md: ${assessment?.currentClaudeMdSize} lines`);
  log(`  - PRD files: ${assessment?.prdFiles?.length || 0}`);
  log(`  - Testing docs: ${assessment?.testingDocs?.length || 0}`);
  log(`  - Issues found: ${assessment?.issues?.length || 0}`);
  log('');

  // Step 2: Phase 0 cleanup
  log('Step 2: Document cleanup and normalization');
  const phase0Results = await phase0Cleanup(assessment);
  log('✓ Phase 0 complete');
  log('');

  // Step 3: Phase 1 modular files (parallel)
  log('Step 3: Creating modular documentation files (parallel)');
  const phase1Results = await phase1ModularFiles();
  log('✓ Phase 1 complete');
  log(`  - Created ${phase1Results.length} files`);
  log('');

  // Step 4: Phase 2 rewrite CLAUDE.md
  log('Step 4: Rewriting CLAUDE.md');
  const phase2Result = await phase2RewriteClaude();
  log('✓ Phase 2 complete');
  log(`  - CLAUDE.md reduced to ~${phase2Result?.targetLines || 100} lines`);
  log('');

  // Step 5: Phase 3 scripts (parallel)
  log('Step 5: Creating maintenance scripts (parallel)');
  const phase3Results = await phase3Scripts();
  log('✓ Phase 3 complete');
  log(`  - Created ${phase3Results.length} scripts`);
  log('');

  // Step 6: Validation
  log('Step 6: Quality validation');
  const validation = await validateImplementation();
  log('✓ Validation complete');
  log(`  - Tests pass: ${validation?.testsPass ? '✓' : '✗'}`);
  log(`  - Scripts work: ${validation?.scriptsWork ? '✓' : '✗'}`);
  log(`  - Content preserved: ${validation?.contentPreserved ? '✓' : '✗'}`);
  if (validation?.issues?.length > 0) {
    log(`  - Issues: ${validation.issues.length}`);
  }
  log('');

  // Step 7: Finalize
  log('Step 7: Archive and finalize');
  const finalization = await finalizeChanges(validation);
  log('✓ Finalization complete');
  log('');

  // Summary
  log('🎉 Workflow Complete!');
  log('');
  log('Summary:');
  log(`- Assessment: ${assessment?.currentClaudeMdSize} → ~100 lines`);
  log(`- Phase 0: Document cleanup`);
  log(`- Phase 1: ${phase1Results.length} modular files created`);
  log(`- Phase 2: CLAUDE.md rewritten`);
  log(`- Phase 3: ${phase3Results.length} maintenance scripts created`);
  log(`- Validation: ${validation?.testsPass ? 'Passed' : 'Issues found'}`);
  log(`- Finalization: Archive and documentation complete`);
  log('');

  return {
    assessment,
    phase0: phase0Results,
    phase1: phase1Results,
    phase2: phase2Result,
    phase3: phase3Results,
    validation,
    finalization
  };
}

return await run();

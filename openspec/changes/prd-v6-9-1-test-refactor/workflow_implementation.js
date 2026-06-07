// Workflow for V6.9.1 Test System Refactor
// Orchestrates parallel execution of test refactoring phases

export const meta = {
  name: 'v6-9-1-test-refactor',
  description: 'Implement V6.9.1 test refactoring - 45 tasks across 9 phases with parallel execution and verification',
  phases: [
    { title: 'Foundation', detail: 'Cleanup and basic helpers (5+5 tasks)' },
    { title: 'Foundation Verification', detail: 'Verify phases 1-3 implementation' },
    { title: 'Fixtures', detail: 'Virtual page JSON fixtures (6 tasks)' },
    { title: 'Test Series', detail: 'D/C/P/E2E/M parallel implementation (20 tasks)' },
    { title: 'Test Series Verification', detail: 'Verify test series implementation' },
    { title: 'Advanced Helpers', detail: 'Chaos, boundary, fault injection (4 tasks)' },
    { title: 'Regression', detail: 'Full test suite validation (5 tasks)' },
    { title: 'Final Verification', detail: 'Comprehensive verification and test execution' }
  ]
}

// Task definitions by phase
const TASKS = {
  phase1_cleanup: [
    { id: '1.1', desc: 'Clean conftest.py - remove 7 AI fixtures', files: ['tests/conftest.py'] },
    { id: '1.2', desc: 'Delete duplicate file test_v6_4_simulation_alignment.py', files: ['tests/v6/test_v6_4_simulation_alignment.py'] },
    { id: '1.3', desc: 'Rename test_simulation.py → test_simulation_base.py', files: ['tests/v6/test_simulation.py', 'tests/v6/test_simulation_base.py'] },
    { id: '1.4', desc: 'Fix test_simulation_e2e.py dead imports', files: ['tests/integration/test_simulation_e2e.py'] },
    { id: '1.5', desc: 'Verify cleanup with pytest --collect-only', files: [] }
  ],
  phase2_helpers1: [
    { id: '2.1', desc: 'Create tests/helpers/__init__.py', files: ['tests/helpers/__init__.py'] },
    { id: '2.2', desc: 'Implement factories.py', files: ['tests/helpers/factories.py'] },
    { id: '2.3', desc: 'Implement state_inspector.py', files: ['tests/helpers/state_inspector.py'] },
    { id: '2.4', desc: 'Implement trace_analyzer.py', files: ['tests/helpers/trace_analyzer.py'] },
    { id: '2.5', desc: 'Verify batch 1 modules import correctly', files: [] }
  ],
  phase3_fixtures: [
    { id: '3.1', desc: 'Create pages_dynamic.json (coordinate format)', files: ['tests/fixtures/pages_dynamic.json'] },
    { id: '3.2', desc: 'Create pages_correction.json', files: ['tests/fixtures/pages_correction.json'] },
    { id: '3.3', desc: 'Create pages_entry.json', files: ['tests/fixtures/pages_entry.json'] },
    { id: '3.4', desc: 'Create pages_boundary.json', files: ['tests/fixtures/pages_boundary.json'] },
    { id: '3.5', desc: 'Create pages_chaos.json', files: ['tests/fixtures/pages_chaos.json'] },
    { id: '3.6', desc: 'Verify fixture format with MockVisionService', files: [] }
  ],
  // Phases 4-7 run in parallel
  phase4_d_series: [
    { id: '4.1', desc: 'Review test_v6_9_dynamic_matching.py - align D1-D10', files: ['tests/v6/test_v6_9_dynamic_matching.py'] },
    { id: '4.2', desc: 'Add boundary tests D11-D13', files: ['tests/v6/test_v6_9_dynamic_matching.py'] },
    { id: '4.3', desc: 'Verify D series with pytest', files: [] }
  ],
  phase5_c_series: [
    { id: '5.1', desc: 'Create tests/v6/unit/test_compiler.py', files: ['tests/v6/unit/test_compiler.py'] },
    { id: '5.2', desc: 'Implement C1-C4 scope mapping tests', files: ['tests/v6/unit/test_compiler.py'] },
    { id: '5.3', desc: 'Implement C5 static path test', files: ['tests/v6/unit/test_compiler.py'] },
    { id: '5.4', desc: 'Implement C6-C9 element_handling tests', files: ['tests/v6/unit/test_compiler.py'] },
    { id: '5.5', desc: 'Implement C10-C12 navigation/completion/validation', files: ['tests/v6/unit/test_compiler.py'] },
    { id: '5.6', desc: 'Verify C series with pytest', files: [] }
  ],
  phase6_p_series: [
    { id: '6.1', desc: 'Review test_simulation_sm.py coverage', files: ['tests/v6/test_simulation_sm.py'] },
    { id: '6.2', desc: 'Implement P1-P2 (precondition + NAVIGABLE)', files: ['tests/v6/test_simulation_sm.py'] },
    { id: '6.3', desc: 'Implement P3-P5 (retry + DEEPER + over-back)', files: ['tests/v6/test_simulation_sm.py'] },
    { id: '6.4', desc: 'Implement P6-P7 (UNKNOWN + vision failure)', files: ['tests/v6/test_simulation_sm.py'] },
    { id: '6.5', desc: 'Implement P8-P10 (concurrent + timeout + unchanged)', files: ['tests/v6/test_simulation_sm.py'] },
    { id: '6.6', desc: 'Verify P series with pytest', files: [] }
  ],
  phase7_e2e_m: [
    { id: '7.1', desc: 'Rewrite test_simulation_e2e.py', files: ['tests/integration/test_simulation_e2e.py'] },
    { id: '7.2', desc: 'Implement E2E1-E2E3 (full menu, target, static path)', files: ['tests/integration/test_simulation_e2e.py'] },
    { id: '7.3', desc: 'Implement E2E4-E2E7 (nested, dynamic, depth, error)', files: ['tests/integration/test_simulation_e2e.py'] },
    { id: '7.4', desc: 'Extend test_simulation_base.py with M1-M5', files: ['tests/v6/test_simulation_base.py'] },
    { id: '7.5', desc: 'Verify E2E + M series with pytest', files: [] }
  ],
  phase8_helpers2: [
    { id: '8.1', desc: 'Implement chaos_engine.py', files: ['tests/helpers/chaos_engine.py'] },
    { id: '8.2', desc: 'Implement boundary_tester.py', files: ['tests/helpers/boundary_tester.py'] },
    { id: '8.3', desc: 'Implement fault_injector.py', files: ['tests/helpers/fault_injector.py'] },
    { id: '8.4', desc: 'Verify batch 2 modules with unit tests', files: [] }
  ],
  phase9_regression: [
    { id: '9.1', desc: 'Full test execution: pytest tests/v6/ tests/integration/ -v', files: [] },
    { id: '9.2', desc: 'Coverage check: pytest --cov=src > 80%', files: [] },
    { id: '9.3', desc: 'Performance baseline comparison', files: [] },
    { id: '9.4', desc: 'Update tests/REFERENCE.md', files: ['tests/REFERENCE.md'] },
    { id: '9.5', desc: 'Acceptance confirmation', files: [] }
  ]
}

// Helper to generate implementation prompt for a task
function taskPrompt(task, phaseContext) {
  return `Implement task ${task.id}: ${task.desc}

Context:
${phaseContext}

Requirements:
1. Read any existing files to understand current state
2. Make ONLY the minimal changes required for this task
3. Follow the spec requirements exactly
4. After implementing, verify the changes work

Files involved: ${task.files.length > 0 ? task.files.join(', ') : 'verification task, no files'}

Report back with:
- What you did
- Any issues encountered
- Verification results`
}

// Verification prompt for checking agent work
function verificationPrompt(phaseTasks, phaseContext, results) {
  const taskList = phaseTasks.map((t, i) => `- ${t.id}: ${t.desc}`).join('\n')
  return `Verify the implementation of ${phaseTasks.length} tasks in this phase.

Tasks completed:
${taskList}

Context:
${phaseContext}

Your job:
1. Read each file that was modified/created
2. Verify the implementation matches the spec requirements
3. Check for:
   - Correct implementation of requirements
   - No unnecessary changes
   - Code quality and consistency
   - Edge cases handled properly
4. Run relevant tests if applicable
5. Report:
   - ✅ Tasks that pass verification
   - ❌ Tasks that need fixes with specific issues
   - ⚠️ Tasks that need review

Be thorough but fair - focus on catching real issues that would prevent the tests from working correctly.`
}

// Phase context helpers
const PHASE_CONTEXTS = {
  phase1_cleanup: `Phase 1: Cleanup & Foundation
Remove dead code that causes import errors and prevents tests from running.
- Delete 7 unused AI fixtures from tests/conftest.py
- Delete duplicate test_v6_4_simulation_alignment.py
- Rename test_simulation.py to test_simulation_base.py
- Fix dead imports in test_simulation_e2e.py
- Verify with pytest --collect-only`,

  phase2_helpers1: `Phase 2: Test Helpers Batch 1
Create shared test helper modules in tests/helpers/
- factories.py: create_minimal_plan(), create_test_node(), create_mock_vision()
- state_inspector.py: verify_stack_consistency(), verify_cache_coherency(), verify_no_orphan_spans(), verify_metrics_completeness(), verify_state_machine_invariants()
- trace_analyzer.py: build_tree(), extract_operations(), count_span_types()
See specs/test-helpers/spec.md for detailed requirements`,

  phase3_fixtures: `Phase 3: Virtual Page Fixtures
Create JSON fixture files with virtual page data.
IMPORTANT: Use coordinate.x/y format, NOT bounds.
MockVisionService reads coordinate: {"x": 0.5, "y": 0.3}
- pages_dynamic.json: Dynamic matching scenarios
- pages_correction.json: Smart correction (multi-level menu)
- pages_entry.json: Entry strategy (desktop + app entry)
- pages_boundary.json: Boundary test scenarios
- pages_chaos.json: Fault injection scenarios`,

  phase4_d_series: `Phase 4: D Series - Dynamic Matching Tests
Review and extend tests/v6/test_v6_9_dynamic_matching.py
- D1-D10: Basic dynamic matching scenarios (verify current implementation aligns)
- D11: Random element order matching
- D12: Empty/massive elements boundary
- D13: Vision failure tolerance
See specs/dynamic-matching-tests/spec.md`,

  phase5_c_series: `Phase 5: C Series - Compiler Tests
Create tests/v6/unit/test_compiler.py with 12 scenarios:
- C1-C4: scope mapping (full→NONE, partial→MAX_STEPS, target_only→TARGET_FOUND, error on missing target)
- C5: target_path static path with path concatenation
- C6-C9: element_handling mapping (full_interaction→4 rules, menu_only→1 rule, safe_mode→4 rules with meta flag, read_only→leaf_info)
- C10: navigation mapping (back→BACK fallback, no nav→AUTO_ESCAPE)
- C11: completion override (timeout overrides scope)
- C12: validation (missing target_app raises CompilerError)
See specs/compiler-tests/spec.md`,

  phase6_p_series: `Phase 6: P Series - Smart Correction Tests
Extend tests/v6/test_simulation_sm.py with 10 scenarios:
- P1: Precondition satisfied bypasses correction
- P2: NAVIGABLE 1-round correction
- P3: NAVIGABLE 3-round exhausts
- P4: DEEPER correction succeeds
- P5: DEEPER over-back returns UNKNOWN
- P6: UNKNOWN recovery exhausts
- P7: Vision failure tolerance
- P8: Concurrent precondition handling
- P9: Precondition timeout after 3 retries
- P10: Correction action succeeds but page unchanged
See specs/smart-correction-tests/spec.md`,

  phase7_e2e_m: `Phase 7: E2E & M Series
- Rewrite tests/integration/test_simulation_e2e.py with E2E1-E2E7 scenarios
- Extend tests/v6/test_simulation_base.py with M1-M5 Mock validation tests
E2E scenarios:
- E2E1: Full menu traversal
- E2E2: Target search (TARGET_FOUND)
- E2E3: Static path traversal
- E2E4: Nested popup handling
- E2E5: Dynamic matching with correction
- E2E6: Depth limit and back strategy
- E2E7: Error recovery with retry
M scenarios: MockVisionService and MockActionExecutor validation
See specs/e2e-tests/spec.md and specs/mock-validation-tests/spec.md`,

  phase8_helpers2: `Phase 8: Test Helpers Batch 2
Advanced testing utilities:
- chaos_engine.py: randomize_page_order(), inject_delay(), corrupt_page_data(), duplicate_elements()
- boundary_tester.py: test_empty_elements(), test_excessive_depth(), test_massive_elements(), test_unicode_edge_cases(), test_extreme_coordinates()
- fault_injector.py: inject_vision_failure(), inject_action_failure(), inject_state_corruption(), inject_mismatched_page()
See specs/test-helpers/spec.md for detailed requirements`,

  phase9_regression: `Phase 9: Full Regression & Acceptance
- Run full test suite: pytest tests/v6/ tests/integration/ -v
- Check coverage: pytest tests/v6/ tests/integration/ --cov=src (ensure >80% for core modules)
- Performance baseline comparison
- Update tests/REFERENCE.md documentation
- Final acceptance: 44+20 scenarios, 100% pass rate, coverage met`
}

// Main workflow
async function run() {
  log('🚀 V6.9.1 Test Refactor Workflow')
  log('================================')
  log('Total tasks: 45 across 9 phases')
  log('')

  // Phase 1: Cleanup (must run first)
  phase('Foundation')
  log('Phase 1: Cleanup & Foundation (sequential - 5 tasks)')
  const cleanupResults = await pipeline(TASKS.phase1_cleanup, task => {
    const prompt = taskPrompt(task, PHASE_CONTEXTS.phase1_cleanup)
    return agent(prompt, { label: task.id, phase: 'Foundation' })
  })
  log(`✓ Phase 1 complete: ${cleanupResults.filter(Boolean).length}/${TASKS.phase1_cleanup.length} tasks`)

  // Phase 2: Helpers Batch 1 (depends on cleanup)
  phase('Fixtures')
  log('Phase 2: Test Helpers Batch 1 (sequential - 5 tasks)')
  const helpers1Results = await pipeline(TASKS.phase2_helpers1, task => {
    const prompt = taskPrompt(task, PHASE_CONTEXTS.phase2_helpers1)
    return agent(prompt, { label: task.id, phase: 'Fixtures' })
  })
  log(`✓ Phase 2 complete: ${helpers1Results.filter(Boolean).length}/${TASKS.phase2_helpers1.length} tasks`)

  // Phase 3: Fixtures (depends on helpers)
  log('Phase 3: Virtual Page Fixtures (sequential - 6 tasks)')
  const fixturesResults = await pipeline(TASKS.phase3_fixtures, task => {
    const prompt = taskPrompt(task, PHASE_CONTEXTS.phase3_fixtures)
    return agent(prompt, { label: task.id, phase: 'Fixtures' })
  })
  log(`✓ Phase 3 complete: ${fixturesResults.filter(Boolean).length}/${TASKS.phase3_fixtures.length} tasks`)

  // Foundation verification phase
  phase('Foundation Verification')
  log('🔍 Verifying Phases 1-3 (Foundation + Fixtures)...')
  const foundationTasks = [...TASKS.phase1_cleanup, ...TASKS.phase2_helpers1, ...TASKS.phase3_fixtures]
  const foundationContext = `${PHASE_CONTEXTS.phase1_cleanup}\n\n${PHASE_CONTEXTS.phase2_helpers1}\n\n${PHASE_CONTEXTS.phase3_fixtures}`
  const foundationResults = [...cleanupResults, ...helpers1Results, ...fixturesResults]
  const foundationVerify = await agent(
    verificationPrompt(foundationTasks, foundationContext, foundationResults),
    { label: 'Verify: Foundation', phase: 'Foundation Verification' }
  )
  log(foundationVerify || 'Foundation verification complete')

  // Phases 4-7: Run in PARALLEL (test series can be worked on simultaneously)
  phase('Test Series')
  log('Phase 4-7: Test Series (PARALLEL - 4 phases)')

  const parallelResults = await parallel([
    () => {
      log('  → Phase 4: D Series (3 tasks)')
      return pipeline(TASKS.phase4_d_series, task => {
        const prompt = taskPrompt(task, PHASE_CONTEXTS.phase4_d_series)
        return agent(prompt, { label: `D:${task.id}`, phase: 'Test Series' })
      })
    },
    () => {
      log('  → Phase 5: C Series (6 tasks)')
      return pipeline(TASKS.phase5_c_series, task => {
        const prompt = taskPrompt(task, PHASE_CONTEXTS.phase5_c_series)
        return agent(prompt, { label: `C:${task.id}`, phase: 'Test Series' })
      })
    },
    () => {
      log('  → Phase 6: P Series (6 tasks)')
      return pipeline(TASKS.phase6_p_series, task => {
        const prompt = taskPrompt(task, PHASE_CONTEXTS.phase6_p_series)
        return agent(prompt, { label: `P:${task.id}`, phase: 'Test Series' })
      })
    },
    () => {
      log('  → Phase 7: E2E & M Series (5 tasks)')
      return pipeline(TASKS.phase7_e2e_m, task => {
        const prompt = taskPrompt(task, PHASE_CONTEXTS.phase7_e2e_m)
        return agent(prompt, { label: `E2E:${task.id}`, phase: 'Test Series' })
      })
    }
  ])

  const [dResults, cResults, pResults, e2eResults] = parallelResults
  log(`✓ D Series: ${dResults.filter(Boolean).length}/${TASKS.phase4_d_series.length} tasks`)
  log(`✓ C Series: ${cResults.filter(Boolean).length}/${TASKS.phase5_c_series.length} tasks`)
  log(`✓ P Series: ${pResults.filter(Boolean).length}/${TASKS.phase6_p_series.length} tasks`)
  log(`✓ E2E & M: ${e2eResults.filter(Boolean).length}/${TASKS.phase7_e2e_m.length} tasks`)

  // Test series verification phase
  phase('Test Series Verification')
  log('🔍 Verifying Phases 4-7 (Test Series)...')
  const testSeriesTasks = [...TASKS.phase4_d_series, ...TASKS.phase5_c_series, ...TASKS.phase6_p_series, ...TASKS.phase7_e2e_m]
  const testSeriesContext = `${PHASE_CONTEXTS.phase4_d_series}\n\n${PHASE_CONTEXTS.phase5_c_series}\n\n${PHASE_CONTEXTS.phase6_p_series}\n\n${PHASE_CONTEXTS.phase7_e2e_m}`
  const testSeriesResults = [...dResults, ...cResults, ...pResults, ...e2eResults]
  const testSeriesVerify = await agent(
    verificationPrompt(testSeriesTasks, testSeriesContext, testSeriesResults),
    { label: 'Verify: Test Series', phase: 'Test Series Verification' }
  )
  log(testSeriesVerify || 'Test series verification complete')

  // Phase 8: Helpers Batch 2 (depends on test series stability)
  phase('Advanced Helpers')
  log('Phase 8: Test Helpers Batch 2 (sequential - 4 tasks)')
  const helpers2Results = await pipeline(TASKS.phase8_helpers2, task => {
    const prompt = taskPrompt(task, PHASE_CONTEXTS.phase8_helpers2)
    return agent(prompt, { label: task.id, phase: 'Advanced Helpers' })
  })
  log(`✓ Phase 8 complete: ${helpers2Results.filter(Boolean).length}/${TASKS.phase8_helpers2.length} tasks`)

  // Phase 9: Regression (final phase)
  phase('Regression')
  log('Phase 9: Full Regression & Acceptance (sequential - 5 tasks)')
  const regressionResults = await pipeline(TASKS.phase9_regression, task => {
    const prompt = taskPrompt(task, PHASE_CONTEXTS.phase9_regression)
    return agent(prompt, { label: task.id, phase: 'Regression' })
  })
  log(`✓ Phase 9 complete: ${regressionResults.filter(Boolean).length}/${TASKS.phase9_regression.length} tasks`)

  // Final verification phase
  phase('Final Verification')
  log('🔍 Final verification of all phases...')
  const allTasks = [
    ...TASKS.phase1_cleanup, ...TASKS.phase2_helpers1, ...TASKS.phase3_fixtures,
    ...TASKS.phase4_d_series, ...TASKS.phase5_c_series, ...TASKS.phase6_p_series, ...TASKS.phase7_e2e_m,
    ...TASKS.phase8_helpers2, ...TASKS.phase9_regression
  ]
  const allResults = [
    ...cleanupResults, ...helpers1Results, ...fixturesResults,
    ...dResults, ...cResults, ...pResults, ...e2eResults,
    ...helpers2Results, ...regressionResults
  ]
  const finalVerify = await agent(
    `Final comprehensive verification of V6.9.1 test refactor implementation.

Total tasks: ${allTasks.length}
Tasks completed: ${allResults.filter(Boolean).length}

Your job:
1. Run the full test suite to verify everything works
2. Check test coverage meets requirements (>80% for core modules)
3. Verify all test files are properly structured
4. Confirm all fixtures are valid JSON
5. Report final status with:
   - Overall pass/fail
   - Any remaining issues
   - Recommendations for next steps

Run: pytest tests/v6/ tests/integration/ -v --collect-only
Then: pytest tests/v6/ tests/integration/ --cov=src --cov-report=term-missing`,
    { label: 'Verify: Final', phase: 'Final Verification' }
  )
  log(finalVerify || 'Final verification complete')

  // Final summary
  log('')
  log('🎉 Implementation Complete!')
  log('================================')

  const totalComplete = allResults.filter(Boolean).length

  log(`Total tasks completed: ${totalComplete}/45`)
  log('')
  log('Verification Summary:')
  log(`  ✓ Foundation phases (1-3): ${[...cleanupResults, ...helpers1Results, ...fixturesResults].filter(Boolean).length}/16`)
  log(`  ✓ Test series phases (4-7): ${testSeriesResults.filter(Boolean).length}/20`)
  log(`  ✓ Advanced helpers (8): ${helpers2Results.filter(Boolean).length}/4`)
  log(`  ✓ Regression (9): ${regressionResults.filter(Boolean).length}/5`)

  if (totalComplete === 45) {
    log('')
    log('✅ All tasks complete! Change is ready for archival.')
    log('   Archive with: /opsx:archive prd-v6-9-1-test-refactor')
    return { success: true, totalComplete, total: 45 }
  } else {
    log('')
    log(`⚠️  ${45 - totalComplete} tasks incomplete. Review agent outputs for issues.`)
    return { success: false, totalComplete, total: 45, incomplete: 45 - totalComplete }
  }
}

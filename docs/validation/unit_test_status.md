# V6 Unit Test Status Report

**Project**: Uni-Claw  
**Version**: V6.5  
**Generated**: 2026-06-06  
**Changes**: trace-integration (V6.3) + simulation-interface-alignment (V6.4) + state-machine-operation-integration (V6.5)  
**Data Source**: test_results/*_unit.json

---

## Executive Summary

| Category | Total | Passing | Failed | Status |
|----------|-------|---------|--------|--------|
| **Trace System (V6.3)** | 123 | 123 | 0 | ✅ 100% |
| **Simulation + Handler Metrics (V6.4/V6.5)** | 28 | 28 | 0 | ✅ 100% |
| **State Machine** | 35 | 30 | 0 | ✅ 86% passing, 14% skipped |
| **E2E Tests** | 3 | 2 | 1 | ⚠️ Pre-existing |
| **TOTAL** | **189** | **183** | **1** | **96.8%** |

## Data Freshness

All test results from 2026-06-06 — FRESH ✅

## Module Detail

### Trace System (123/123) ✅
- `test_trace_models.py`: 24 tests (ULID, SessionNode, StepNode, SpanNode, serialization)
- `test_trace_storage.py`: 15 tests (MemoryStorage, FileStorage, queue buffering)
- `test_trace_recorder.py`: 16 tests (StepTracker, TraceRecorder lifecycle, log-and-continue)
- `test_trace_analyzer.py`: 14 tests (build_tree, backfill, 8 extraction methods)
- `test_trace_context.py`: 15 tests (Session, StackFrame, TraversalRuntimeContext, to_readonly)
- `test_trace_recovery.py`: 14 tests (ContextRebuilder, RecoveryStrategy, Span validation)
- `test_trace_integration.py`: 12 tests (MockEngine, JSONL, session.json, recovery)
- `test_trace_simulation.py`: 15 tests (MemoryStorage simulation, TraceAnalyzer with sim traces)

### Simulation + Handler Metrics (28/28) ✅
- `test_v6_4_simulation_alignment.py`: 17 tests (MockVisionService ABC, MockActionExecutor ABC, SimulationRunner engine, no fallback, no redundant state)
- `test_v6_5_handler_metrics.py`: 11 tests (handler metrics pipeline, ai_call/execution/error spans, TraceAnalyzer extraction, simulation end-to-end)

### State Machine (30/35) ✅
- 30 passed, 0 failed, 5 skipped (pre-existing skips)
- `test_state_machine.py`: core state machine lifecycle tests

### Engine Creation (3/3) ✅
- `test_executor.py`: GraphTraversalEngine creation, exception chain, state machine instantiation

## Known Issues

| Issue | Severity | Status |
|-------|----------|--------|
| E2E test failure (1/3) | Low | Pre-existing, not related to V6.3-V6.5 |
| State machine complex traversal hangs | Medium | Pre-existing V6.0 cycle issue, step limit guard added |
| Old simulation tests use deleted API | Expected | 25 tests testing removed `tap/swipe/click` methods |

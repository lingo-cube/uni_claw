# Trace System (V6.3)

Distributed tracing system for Uni-Claw, aligned with industry standards (OpenTelemetry/Jaeger).

## Architecture

Three-tier node hierarchy:

| Node | Purpose | Example |
|------|---------|---------|
| `SessionNode` | Root of trace, holds session metadata | One per traversal run |
| `StepNode` | Represents a traversal step (NODE_SELECT / FRAME_COMPLETE) | One per graph node |
| `SpanNode` | Fine-grained operation within a step | AI call, action execution, error |

## Module Structure

```
src/trace/
├── models.py      # TraceNode, SessionNode, StepNode, SpanNode, generate_id()
├── storage.py     # TraceStorage ABC, FileStorage, MemoryStorage
├── recorder.py    # TraceRecorder, StepTracker
├── analyzer.py    # TraceAnalyzer, build_tree()
├── context.py     # Session, StackFrame, TraversalRuntimeContext
├── recovery.py    # ContextRebuilder, RecoveryStrategy
├── metrics.py     # AICallMetrics, ExecutionMetrics, ErrorMetrics
└── README.md      # This file
```

## Quick Start

```python
from src.trace import (
    SessionNode, StepNode, SpanNode,
    MemoryStorage, TraceRecorder, TraceAnalyzer,
)

# 1. Create storage and recorder
storage = MemoryStorage()
recorder = TraceRecorder(storage=storage)

# 2. Initialize a session
sess = SessionNode(device_model="Pixel 7", os_version="Android 14")
recorder.init(sess)

# 3. Record steps and spans
step = StepNode(node_id="home", step_type="NODE_SELECT")
recorder.record_step_start(step)

recorder.record_span(SpanNode(
    span_type="ai_call",
    capability="vision",
    provider_id="deepseek",
    success=True,
    latency_ms=350.0,
    input_tokens=1200,
    output_tokens=80,
))

recorder.record_span(SpanNode(
    span_type="execution",
    action="click",
    status="success",
    target="btn_settings",
    duration_ms=150.0,
))

recorder.record_step_end(step.span_id, {"ok": True})

# 4. Finalize
recorder.finalize("completed")

# 5. Analyze
nodes = storage.read(sess.trace_id)
analyzer = TraceAnalyzer(nodes)
print(analyzer.extract_page_tree())
print(analyzer.extract_action_sequence())
```

## Storage Backends

### MemoryStorage
For simulation and testing. Nodes stored in memory — no I/O.

```python
storage = MemoryStorage()
storage.write(node)
nodes = storage.read(trace_id)
```

### FileStorage
For production. Buffered, async JSONL writes via background thread.
Never blocks the traversal thread.

```python
storage = FileStorage(base_dir="traces")
storage.write(node)          # Non-blocking (queue)
storage.write_session(data, trace_id)  # Writes session.json
storage.flush(timeout=5.0)   # Wait for queue to drain
nodes = storage.read(trace_id)  # Read trace.jsonl
```

Directory layout:
```
traces/{trace_id}/
├── session.json       # Session metadata
├── trace.jsonl        # One JSON node per line
└── screenshots/
    └── index.json     # ref_id → filename mapping
```

## Span Types

| span_type | Fields | When |
|-----------|--------|------|
| `state_transition` | from_state, to_state, state_machine | State machine transitions |
| `execution` | action, status, target, page_before, page_after, duration_ms | Action execution |
| `ai_call` | capability, provider_id, success, latency_ms, tokens | AI service calls |
| `error` | error_type, error_message, severity, stack_trace | Error handling |
| `step_end` | step_span_id, result | Step completion (backfills StepNode) |
| `session_end` | status, end_time | Session completion (backfills SessionNode) |

## Trace Analysis

```python
analyzer = TraceAnalyzer(nodes)

analyzer.extract_page_tree()         # Nested page hierarchy
analyzer.extract_state_sequence()    # State transitions
analyzer.extract_span_chain(span_id) # Call chain from root
analyzer.extract_ai_calls()          # AI call records
analyzer.extract_action_sequence()   # Action records
analyzer.extract_error_statistics()  # Error aggregation
analyzer.extract_time_analysis()     # Timing statistics
analyzer.extract_coverage_analysis() # Page/node coverage
```

## Context Recovery

```python
from src.trace import ContextRebuilder, RecoveryStrategy

rebuilder = ContextRebuilder()
ctx = rebuilder.rebuild(nodes, trace_id, RecoveryStrategy.FULL)

# ctx.current_path, ctx.visited_pages, ctx.node_stack are restored
```

## Error Handling: "Log and Continue"

All recorder methods follow "log and continue": write failures are logged but never interrupt traversal.

## ULID Identifiers

All IDs are ULIDs — 26-character Crockford Base32 strings, time-sortable, URL-safe.

```python
from src.trace import generate_id
uid = generate_id()  # e.g., "01KTEB36KGRB4QJ12X9VMAJ382"
```

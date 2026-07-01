---
name: state-machine-integration
description: Integrate trace data with state machines - format traces as state transitions, error events, and performance metrics for state machine consumption
license: MIT
compatibility: Works with any state machine implementation, JSON-based state data
metadata:
  author: uni-claw-ai-team
  version: "1.0"
  tags: [state-machine, trace, integration, json, transitions]
---

# State Machine Integration Skill

Integrate trace data with state machines by formatting traces as state transitions and events.

## When to Use

Use this skill when you need to:
- Convert trace data to state machine events
- Track state transitions in traversals
- Monitor state machine performance
- Generate state transition logs
- Analyze state machine behavior

## What It Does

1. **Event Formatting**: Converts trace assets to state machine events:
   - State transition events (from_state -> to_state)
   - Performance events (latency, tokens)
   - Error events (failures, fallbacks)
   - Context propagation events

2. **State Tracking**: Maintains state machine state:
   - Current state tracking
   - State transition history
   - Error state handling
   - Context preservation

3. **Metrics Collection**: Gathers state machine metrics:
   - Transition counts and rates
   - Error rates by state
   - Average performance per transition
   - Total resource consumption

## How It Works

1. **Load Trace Assets**: Reads JSON trace files
2. **Extract State Info**: Identifies states from trace data
3. **Build Transitions**: Creates state transition events
4. **Track Errors**: Separate error events and states
5. **Calculate Metrics**: Compute performance statistics
6. **Generate Reports**: Create state machine friendly output

## State Machine Event Format

```json
{
  "event_type": "state_transition",
  "event_id": "analyze_visual_0_1780403493461",
  "from_state": "initial",
  "to_state": "menu_list", 
  "capability": "analyze_visual",
  "timestamp": "2026-06-02T20:31:33.461225",
  "performance": {
    "latency_ms": 2500.0,
    "tokens": 1450
  },
  "context": {
    "traversal_session": "session_001",
    "current_depth": 2,
    "target_goal": "configure_wifi"
  },
  "success": true
}
```

## Usage

```bash
# Generate state machine integration report
python tests/ai/trace_tree_visualizer.py
```

## Output Reports

### Transition Log
```
[TRANSITION LOG]
  1. [SUCCESS] initial -> menu_list
      Trigger: analyze_visual
      Performance: 2500ms, 1450 tokens
  2. [SUCCESS] menu_list -> settings_group  
      Trigger: analyze_visual
      Performance: 2800ms, 1600 tokens
```

### State Transitions
```
[STATE TRANSITIONS]
  initial -> menu_list (via analyze_visual)
  menu_list -> settings_group (via analyze_visual)
```

### Error Events
```
[ERROR EVENTS]
  State: unknown, Error: Low confidence due to poor image quality
  Capability: analyze_visual
```

### Performance Summary
```
[PERFORMANCE SUMMARY]
  Total Transitions: 4
  Total Latency: 9600.0ms
  Average Latency: 1920.0ms
  Total Tokens: 4685
  Error Rate: 20.00%
```

## State Machine Benefits

1. **Transition Clarity**: Clear from/to state mappings
2. **Error Isolation**: Separate error event tracking
3. **Performance Visibility**: Metrics per transition
4. **Context Preservation**: Business context maintained
5. **Debugging Support**: Detailed event logs for debugging

## Integration Points

The skill provides several integration hooks:

1. **Event Streams**: JSON event arrays for streaming
2. **Metrics APIs**: Performance data for monitoring
3. **Error Callbacks**: Error event notifications
4. **State Snapshots**: State machine state serialization

## File Organization

Generated files:
- `state_transitions.json` - Raw transition events
- `state_metrics.json` - Performance metrics
- `error_log.json` - Error event log
- `integration_report.txt` - Human-readable report

## Use Cases

- **Real-time Monitoring**: Stream events to monitoring systems
- **Post-Mortem Analysis**: Analyze past state machine runs
- **Performance Tuning**: Identify slow transitions
- **Error Analysis**: Debug state machine failures
- **Documentation**: Generate state machine diagrams

## See Also

- `trace-collection` skill - Generate trace data
- `trace-visualization` skill - Visualize traces  
- `workflow-trace-collection` skill - Workflow integration
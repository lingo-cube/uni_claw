---
name: trace-collection
description: Collect Mock test asset traces with zero API cost - generates JSON trace files for testing, debugging, and state machine integration
license: MIT
compatibility: Works with any Python project, requires asyncio
metadata:
  author: uni-claw-ai-team
  version: "1.0"
  tags: [testing, trace, mock, zero-cost, state-machine]
---

# Trace Collection Skill

Collect comprehensive Mock trace data with zero API cost for testing and state machine integration.

## When to Use

Use this skill when you need to:
- Generate test assets with realistic trace data
- Create performance baselines for AI operations  
- Build state machine transition logs
- Analyze system behavior without API costs
- Generate trace visualization data

## What It Does

1. **Mock API Simulation**: Simulates AI API calls with realistic delays and token consumption
2. **Trace Data Collection**: Collects comprehensive trace information including:
   - Span IDs and parent-child relationships
   - Performance metrics (latency, tokens)
   - Custom business context
   - Error scenarios and low-confidence cases
3. **Multi-Scenario Coverage**: Generates traces for:
   - Vision analysis (Claude Sonnet)
   - Instruction parsing (DeepSeek)
   - Page verification
   - Decision making
   - Safety screening
   - Error handling

## How It Works

1. **Asset Collection**: Runs predefined scenarios with Mock data
2. **Trace Simulation**: Simulates realistic API call delays (800ms-3000ms)
3. **Data Generation**: Creates structured JSON assets with:
   - Input/output data
   - Performance metrics
   - Trace context (span IDs, operations, tags)
   - Custom business context
4. **Asset Storage**: Saves to `tests/ai/assets/traces/` directory

## Generated Assets

- `vision_analysis_standard.json` - Visual analysis scenarios
- `instruction_parse_standard.json` - Instruction parsing scenarios  
- `page_verification.json` - Page type verification scenarios
- `decision_making.json` - Decision capability scenarios
- `safety_screening.json` - Safety check scenarios
- `error_handling.json` - Error and low-confidence scenarios
- `overview.json` - Complete statistics and summary

## Usage

```bash
# Collect standard trace assets
python tests/ai/test_asset_collection_demo.py

# Analyze collected assets
python tests/ai/test_asset_collection_demo.py analyze
```

## Output Format

Each asset contains:
```json
{
  "asset_id": "vision_analysis_standard_analyze_visual_0",
  "scenario": "vision_analysis_standard",
  "capability": "analyze_visual", 
  "provider_id": "claude",
  "mode": "vision",
  "input_data": {...},
  "output_data": {...},
  "latency_ms": 2500.0,
  "input_tokens": 1100,
  "output_tokens": 350,
  "total_tokens": 1450,
  "trace_context": {
    "span_id": "analyze_visual_0_1780403493461",
    "operation": "unibrain.analyze_visual",
    "tags": {...}
  },
  "custom_context": {...},
  "tags": ["vision", "settings", "menu_list"],
  "description": "Analyze visual structure of settings main page"
}
```

## State Machine Integration

The trace data is formatted for state machine consumption:
- **State Transitions**: From/to states with triggers
- **Performance Events**: Timing and token metrics per transition  
- **Error Events**: Failure states with error context
- **Custom Context**: Business context injection per state

## Benefits

- **Zero API Cost**: No real API calls, completely free
- **Fast Execution**: Mock data generation is instant
- **Comprehensive Coverage**: Multiple scenarios and edge cases
- **State Machine Ready**: Formatted for state machine integration
- **Reusable Assets**: JSON files for testing and documentation

## See Also

- `trace-visualization` skill - Visualize collected traces
- `state-machine-integration` skill - Integrate with state machines
- `workflow-trace-collection` skill - Workflow integration
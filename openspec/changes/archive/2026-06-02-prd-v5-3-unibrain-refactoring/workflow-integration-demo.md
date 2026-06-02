# Workflow Integration Demo: Trace Collection + OpenSpec

This demonstrates the complete integration of the 4 trace collection skills with the OpenSpec workflow for PRD_V5_3-unibrain-refactoring.

---

## Skills Ecosystem Overview

### 4 Core Skills Created

1. **trace-collection** - Core trace collection functionality
2. **trace-visualization** - Tree visualization and diagram generation  
3. **state-machine-integration** - State machine event formatting
4. **workflow-trace-collection** - OpenSpec workflow integration

---

## Integration Phase 1: Planning

### During Proposal Creation

**Step 1**: Define trace requirements in the proposal

```yaml
# In proposal.md
## Trace Requirements

This change will collect traces for:
- analyze_visual capability (Claude Sonnet)
- parse_instruction capability (DeepSeek)
- Provider routing decisions
- Prompt management operations
- State transitions in refactored UniBrain

Expected trace volume: ~50 traces per run
Performance targets: <2000ms avg latency, <1000 tokens avg
```

**Step 2**: Add trace collection to task dependencies

```yaml
# In tasks.md
### T1.1 Implement Provider Abstraction

**Dependencies**: 
- [x] Mock data infrastructure (T0.x)
- [ ] Trace collection setup

**Trace Verification**:
- [ ] Collect traces during implementation
- [ ] Verify trace completeness
- [ ] Generate trace visualization
- [ ] Validate state machine integration
```

---

## Integration Phase 2: Implementation

### Automated Trace Collection During Development

**Workflow**: When implementing T1.1 (Provider Abstraction)

```bash
# 1. Developer implements feature
# Implementing AIProvider interface...

# 2. Automatic trace collection (integrated into workflow)
python tests/ai/test_asset_collection_demo.py

# Output:
[COLLECTED] 5 trace assets for Provider Abstraction implementation
[ASSETS] Saved to tests/ai/assets/traces/
```

**Real-time Trace Simulation**:
```json
// Generated trace: provider_routing.json
{
  "asset_id": "provider_routing_select_provider_0",
  "scenario": "provider_routing",
  "capability": "route_to_provider",
  "provider_id": "unibrain",
  "mode": "text",
  "latency_ms": 50.0,
  "trace_context": {
    "span_id": "route_0_1780403493461",
    "operation": "unibrain.route_capability"
  },
  "custom_context": {
    "requested_capability": "analyze_visual",
    "selected_provider": "claude",
    "routing_reason": "vision_capability"
  }
}
```

---

## Integration Phase 3: Verification

### Automated Trace Visualization

**After each task completion**:

```bash
# Generate trace visualization
python tests/ai/trace_tree_visualizer.py
```

**Output**: Tree structure showing implementation traces

```
[TREE STRUCTURE]
├── [route_to_provider] via unibrain (text)
│   Performance: 50ms, 0 tokens
│   Span ID: route_0_1780403493461
└── [analyze_visual] via claude (vision)
    Performance: 2500ms, 1450 tokens
    Span ID: analyze_visual_0_1780403493461
```

**State Machine Integration Report**:
```
[STATE MACHINE INTEGRATION REPORT]

[TRANSITION LOG]
  1. [SUCCESS] initial -> provider_selected
      Trigger: route_to_provider
      Performance: 50ms, 0 tokens
  2. [SUCCESS] provider_selected -> vision_analysis
      Trigger: analyze_visual
      Performance: 2500ms, 1450 tokens

[PERFORMANCE SUMMARY]
  Total Transitions: 2
  Total Latency: 2550.0ms
  Average Latency: 1275.0ms
  Total Tokens: 1450
  Error Rate: 0.00%
```

---

## Integration Phase 4: Workflow Artifacts

### Enhanced OpenSpec Artifacts with Trace Evidence

**Enhanced tasks.md** with trace verification:

```markdown
### T1.1 Implement Provider Abstraction ✅

**Implementation Status**: Completed
**Trace Evidence**: 
- `provider_routing.json` - 3 traces collected
- `provider_selection.json` - 2 traces collected
- State machine integration: Verified

**Performance Validation**:
- Average routing latency: 50ms ✓
- Provider selection accuracy: 100% ✓
- Token efficiency: Optimal ✓

**State Machine Verification**:
- Transitions: 5 successful, 0 errors
- Context propagation: Complete ✓
- Error handling: Not triggered (no errors) ✓
```

---

## Integration Phase 5: Continuous Monitoring

### CI/CD Integration

**GitHub Actions Workflow** (automated):

```yaml
# .github/workflows/trace-integration.yml
name: Trace Integration

on: [push, pull_request]

jobs:
  trace-verification:
    runs-on: ubuntu-latest
    steps:
      - name: Setup Python
        uses: actions/setup-python@v2
        
      - name: Install dependencies
        run: pip install -e .
        
      - name: Run trace collection
        run: python tests/ai/test_asset_collection_demo.py
        
      - name: Generate trace visualization
        run: python tests/ai/trace_tree_visualizer.py
        
      - name: Verify trace completeness
        run: |
          python -c "
          import json
          with open('tests/ai/assets/traces/overview.json') as f:
              overview = json.load(f)
              assert overview['total_assets'] >= 5, 'Insufficient traces'
              print(f'[OK] {overview[\"total_assets\"]} traces collected')
          "
        
      - name: Upload trace assets
        uses: actions/upload-artifact@v2
        with:
          name: trace-assets
          path: tests/ai/assets/traces/
```

---

## Integration Phase 6: Code Review Evidence

### Pull Request with Trace Evidence

**Pull Request Description**:

```markdown
## PRD V5.3 Task T1.1: Provider Abstraction Implementation

### Implementation Summary
✅ Implemented AIProvider interface
✅ Created DeepSeekProvider, ClaudeProvider, MiMoProvider
✅ Added declarative routing configuration

### Trace Evidence

#### Collected Traces
- `provider_routing.json` - 3 routing decision traces
- `provider_implementation.json` - 5 implementation traces
- Total: 8 traces collected during implementation

#### Performance Metrics
- Average routing latency: 50ms
- Average provider latency: 1850ms
- Token efficiency: 950 avg tokens per call

#### State Machine Integration
- Transitions: 8 successful, 0 errors
- Context propagation: 100% complete
- Error handling: Verified (1 intentional error test)

### Verification Status
- [x] Trace coverage adequate (≥5 traces)
- [x] Performance within targets
- [x] State machine transitions valid
- [x] Error handling traces included
- [x] All tests passing

### Generated Artifacts
- [Trace Tree Visualization](tests/ai/assets/trace_diagram.mmd)
- [State Machine Report](tests/ai/assets/state_machine_report.json)
- [Performance Metrics](tests/ai/assets/performance_summary.json)
```

---

## Integration Phase 7: Archival

### Complete Change Package with Trace Evidence

**When archiving with `/opsx:archive`**:

```bash
# Archive with trace evidence included
/opsx:archive --include-traces
```

**Generated Archive Structure**:

```
openspec/changes/archive/2026-06-02-prd-v5-3-unibrain-refactoring/
├── .openspec.yaml
├── proposal.md
├── design.md
├── tasks.md
├── trace_evidence/
│   ├── provider_implementation/
│   │   ├── routing_traces.json
│   │   ├── implementation_traces.json
│   │   └── performance_metrics.json
│   ├── visualizations/
│   │   ├── trace_diagram.mmd
│   │   ├── tree_structure.txt
│   │   └── state_machine_report.json
│   └── overview.json
└── completion_report.md
```

---

## Benefits of Workflow Integration

### 1. **Automated Evidence Collection**
- No manual trace gathering needed
- Continuous collection during implementation
- Automatic verification at each phase

### 2. **Enhanced Code Review**
- Complete trace evidence for PRs
- Performance metrics included
- State machine integration verified

### 3. **Improved Quality Assurance**
- Automated trace completeness checks
- Performance regression detection
- State machine behavior validation

### 4. **Better Documentation**
- Complete trace history
- Visual diagrams for documentation
- State machine transition evidence

### 5. **Zero-Cost Testing**
- Mock data integration
- No API costs during development
- Continuous validation without expenses

---

## Skills Working Together

### Skill Orchestration Example

```bash
# Phase 1: During implementation
/trace-collection    # Collect implementation traces

# Phase 2: After task completion  
/trace-visualization    # Generate trees and diagrams

# Phase 3: Verification
/state-machine-integration    # Format for state machine

# Phase 4: Throughout workflow
/workflow-trace-collection    # Automated CI/CD integration
```

### Data Flow

```
Implementation → Trace Collection → Visualization → State Machine → Workflow Evidence
     ↓                ↓                  ↓                  ↓                    ↓
  Code Changes    JSON Traces       ASCII Trees       Event Logs         PR/Artifacts
```

---

## Conclusion

This demonstrates the complete skills ecosystem:

1. **trace-collection** - Foundation data collection
2. **trace-visualization** - Human-readable output
3. **state-machine-integration** - State machine formatting
4. **workflow-trace-collection** - OpenSpec workflow orchestration

All 4 skills work seamlessly together to provide comprehensive trace integration throughout the entire development lifecycle, from initial implementation through final archival.

---

**Demo Created**: 2026-06-02
**OpenSpec Change**: prd-v5-3-unibrain-refactoring
**Skills Ecosystem**: 4 comprehensive skills integrated
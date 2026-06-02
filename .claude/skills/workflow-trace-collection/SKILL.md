---
name: workflow-trace-collection
description: Integrate trace collection into OpenSpec workflow - automatically collect traces during implementation, generate trace-based verification, and support continuous trace monitoring
license: MIT
compatibility: Requires OpenSpec workflow system, integrates with /opsx commands
metadata:
  author: uni-claw-ai-team
  version: "1.0"
  tags: [workflow, openspec, trace, ci-cd, automation]
---

# Workflow Trace Collection Skill

Integrate comprehensive trace collection into OpenSpec workflow for continuous monitoring and verification.

## When to Use

Use this skill when you need to:
- Integrate trace collection into implementation workflows
- Generate trace-based verification during development
- Support continuous monitoring in CI/CD pipelines
- Automate trace collection for testing
- Provide trace evidence for code reviews

## What It Does

1. **Workflow Integration**: Embeds trace collection into:
   - Implementation tasks
   - Testing phases
   - Verification steps
   - Code review processes

2. **Automated Collection**: Automatically collects traces during:
   - Task execution
   - Test runs
   - Performance benchmarks
   - Error simulations

3. **Verification Support**: Generates trace-based:
   - Implementation verification
   - Performance validation
   - Behavior confirmation
   - Error reproduction

## OpenSpec Integration

### With /opsx Commands

```bash
# Propose change with trace collection
/opsx:propose add-trace-monitoring

# Apply with automatic trace collection  
/opsx:apply --enable-trace-collection

# Archive with trace evidence
/opsx:archive --include-traces
```

### In Change Artifacts

**proposal.md**:
```markdown
## Trace Requirements

This change will collect traces for:
- analyze_visual capability (Claude Sonnet)
- parse_instruction capability (DeepSeek)
- State transitions in traversal flow

Expected trace volume: ~50 traces per run
Performance targets: <2000ms avg latency, <1000 tokens avg
```

**tasks.md**:
```markdown
### T3.1 Implement with Trace Collection

**Verification**:
- [ ] Collect traces during implementation
- [ ] Verify trace completeness
- [ ] Generate trace visualization
- [ ] Validate state machine integration
```

## Workflow Phases

### Phase 1: Planning
- Define trace requirements
- Specify capabilities to trace
- Set performance baselines
- Plan trace collection points

### Phase 2: Implementation  
- Collect traces during coding
- Monitor trace coverage
- Validate trace format
- Generate trace reports

### Phase 3: Verification
- Run trace collection tests
- Verify trace completeness
- Check state machine integration
- Generate verification reports

### Phase 4: Archival
- Include traces in change archive
- Generate final trace summary
- Create trace evidence package
- Store in project documentation

## Automated Workflows

### CI/CD Integration
```yaml
# .github/workflows/trace-collection.yml
name: Trace Collection

on: [push, pull_request]

jobs:
  collect-traces:
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
        
      - name: Upload trace assets
        uses: actions/upload-artifact@v2
        with:
          name: trace-assets
          path: tests/ai/assets/traces/
```

### Pre-commit Hooks
```bash
#!/usr/bin/env python3
# .git/hooks/pre-commit

def check_trace_coverage():
    """Ensure adequate trace coverage"""
    import subprocess
    result = subprocess.run([
        'python', 'tests/ai/test_asset_collection_demo.py'
    ], capture_output=True)
    
    if result.returncode != 0:
        print("[ERROR] Trace collection failed")
        return False
    
    # Verify minimum trace count
    import json
    with open('tests/ai/assets/traces/overview.json') as f:
        overview = json.load(f)
        if overview['total_assets'] < 3:
            print("[ERROR] Insufficient trace coverage")
            return False
    
    print("[SUCCESS] Trace coverage verified")
    return True

if __name__ == "__main__":
    if not check_trace_coverage():
        exit(1)
```

## Trace-Based Verification

### Implementation Verification
```bash
# After implementing a feature, collect traces
python -c "
import asyncio
from tests.ai.test_asset_collection_demo import collect_standard_scenarios

async def verify_implementation():
    collector = await collect_standard_scenarios()
    
    # Verify trace completeness
    assert collector.collected_assets, 'No traces collected'
    assert len(collector.collected_assets) >= 3, 'Insufficient traces'
    
    # Verify state machine integration
    from tests.ai.trace_tree_visualizer import StateMachineTraceFormatter
    formatter = StateMachineTraceFormatter()
    sm_data = formatter.format_assets_for_state_machine(collector.collected_assets)
    
    assert sm_data['trace_log'], 'No state machine events'
    assert sm_data['performance_metrics']['total_transitions'] > 0, 'No state transitions'
    
    print('[VERIFIED] Implementation traces complete and valid')

asyncio.run(verify_implementation())
"
```

### Continuous Monitoring
```python
# Monitor trace health during development
def monitor_trace_health():
    """Check trace collection system health"""
    import json
    from pathlib import Path
    
    # Check recent traces
    trace_dir = Path("tests/ai/assets/traces")
    overview_file = trace_dir / "overview.json"
    
    if not overview_file.exists():
        print("[WARNING] No trace overview found")
        return False
    
    with open(overview_file) as f:
        overview = json.load(f)
    
    # Health checks
    asset_count = overview['total_assets']
    if asset_count < 5:
        print(f"[WARNING] Low trace count: {asset_count}")
    
    # Check error rates
    summary = overview.get('assets_summary', {})
    error_rate = summary.get('averages', {}).get('error_rate', 0)
    if error_rate > 0.3:
        print(f"[WARNING] High error rate: {error_rate:.1%}")
    
    print(f"[OK] Trace system healthy: {asset_count} assets, {error_rate:.1%} errors")
    return True
```

## Benefits

1. **Automated Collection**: No manual trace gathering needed
2. **Continuous Verification**: Always have fresh trace data
3. **Workflow Integration**: Seamless OpenSpec integration
4. **Evidence Generation**: Automatic evidence for code reviews
5. **Performance Monitoring**: Continuous performance tracking

## Generated Artifacts

During workflow execution, generates:
- `traces/latest/` - Most recent trace collection
- `traces/baseline/` - Performance baseline traces
- `traces/verification/` - Verification trace sets
- `trace_reports/` - Human-readable reports
- `mermaid_diagrams/` - Visual trace diagrams

## Usage Examples

### In Implementation Tasks
```python
# Automatically collect traces when implementing features
async def implement_with_traces():
    """Implementation with automatic trace collection"""
    
    # 1. Implement feature
    result = implement_new_feature()
    
    # 2. Collect traces
    from tests.ai.test_asset_collection_demo import MockTraceCollector
    collector = MockTraceCollector()
    
    # 3. Generate traces for the feature
    test_calls = generate_trace_scenarios(result)
    await collector.collect_scenario('feature_test', test_calls)
    
    # 4. Verify trace integration
    assert verify_trace_integration(collector.collected_assets)
    
    # 5. Save traces for workflow
    collector.save_assets()
```

### In Code Review
```markdown
## Code Review Evidence

### Traces Collected
- `feature_x_traces.json` - 15 traces collected during implementation
- State transitions: 8 successful, 0 errors  
- Performance: Average 1850ms latency, 850 tokens
- State machine integration: Verified

### Verification Status
- [x] Trace coverage adequate (>10 traces)
- [x] Performance within targets
- [x] State machine transitions valid
- [x] Error handling traces included
```

## See Also

- `trace-collection` skill - Core trace collection
- `trace-visualization` skill - Trace visualization
- `state-machine-integration` skill - State machine format
- OpenSpec documentation for workflow integration details
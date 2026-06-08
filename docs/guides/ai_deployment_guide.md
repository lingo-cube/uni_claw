# AI Provider Deployment & Rollout Guide

## Overview

This guide covers deployment strategies, gradual rollout, monitoring setup, and rollback procedures for the UniBrain AI Provider.

## Prerequisites

1. **API Keys**
   - DeepSeek API key (required)
   - Anthropic Claude API key (if using Claude vision)

2. **Dependencies**
   ```bash
   pip install -r requirements.txt
   ```

3. **Configuration**
   ```bash
   cp config/ai_production.env.template .env.production
   # Edit .env.production with your values
   ```

## Deployment Strategy

### Phase 1: Staging Environment

1. **Setup Staging Config**
   ```bash
   export DEEPSEEK_API_KEY="staging-key"
   export VISION_SERVICE_TYPE="mock"  # Use mock initially
   export ENABLE_AI_ADVISOR="true"
   ```

2. **Test Core Functionality**
   - Verify configuration loading
   - Test individual capabilities
   - Run integration tests

3. **Test with Real Vision**
   ```bash
   export VISION_SERVICE_TYPE="claude"
   export VISION_API_KEY="staging-claude-key"
   ```

### Phase 2: Gradual Rollout

**Recommended Rollout Order:**

1. **Week 1-2: Vision Analysis Only**
   ```python
   # Enable only vision capability
   config = TraversalConfig(
       enable_ai_advisor=True,
       ai_min_confidence=0.8,  # Higher threshold initially
   )
   ```
   - Monitor vision accuracy
   - Track failure patterns
   - Validate page type detection

2. **Week 3-4: Add Page Verification**
   - Enable `verify` capability
   - Verify container type accuracy
   - Check mismatch detection

3. **Week 5-6: Add Safety Screening**
   - Enable `safety` capability
   - Validate safety classifications
   - Check false positive rates

4. **Week 7-8: Full Decision Making**
   - Enable `decision` capability
   - Start with read-only mode
   - Gradually allow actual interactions

### Phase 3: Production Deployment

**Production Configuration Checklist:**

- [ ] All API keys configured
- [ ] Retry settings tuned (3-5 attempts)
- [ ] Monitoring dashboards setup
- [ ] Alerts configured
- [ ] Rollback procedure documented
- [ ] Team trained on new system

## Monitoring Setup

### Metrics to Monitor

**1. Performance Metrics**
```python
# Latency monitoring
latency_stats = provider.get_latency_stats("ContextDecisionCapability")
print(f"P50: {latency_stats['p50']}ms")
print(f"P95: {latency_stats['p95']}ms")
print(f"P99: {latency_stats['p99']}ms")
```

**Alert Thresholds:**
- P95 latency > 5000ms: Warning
- P95 latency > 10000ms: Critical

**2. Success Rate Metrics**
```python
metrics = provider.get_metrics_summary()
for capability, counts in metrics["call_counts"].items():
    total = counts["success"] + counts["failure"]
    if total > 0:
        success_rate = counts["success"] / total * 100
        print(f"{capability}: {success_rate:.1f}% success")
```

**Alert Thresholds:**
- Success rate < 90%: Warning
- Success rate < 70%: Critical

**3. Confidence Distribution**
```python
confidence = provider.metrics.get_confidence_distribution("ContextDecisionCapability")
low_confidence = confidence["buckets"]["0.5-0.7"] / confidence["count"] * 100
```

**Alert Thresholds:**
- Low confidence > 30%: Review prompts
- Low confidence > 50%: Critical issue

**4. Token Usage**
```python
usage = provider.metrics.get_token_usage()
print(f"Total tokens: {usage['total']}")
print(f"Average per call: {usage['mean']}")
```

**Alert Thresholds:**
- Total > 100k tokens: Review usage
- Total > 500k tokens: Cost alert

### Dashboard Configuration

**Grafana Dashboard Example:**

```json
{
  "panels": [
    {
      "title": "AI Capability Latency",
      "targets": [
        {
          "expr": "ai_capability_latency_seconds{quantile='0.95'}"
        }
      ]
    },
    {
      "title": "Success Rate",
      "targets": [
        {
          "expr": "rate(ai_capability_calls_total{result='success'}[5m]) / rate(ai_capability_calls_total[5m])"
        }
      ]
    },
    {
      "title": "Token Usage",
      "targets": [
        {
          "expr": "increase(ai_token_usage_total[1h])"
        }
      ]
    }
  ]
}
```

### Alert Configuration

**Prometheus Alerts Example:**

```yaml
groups:
  - name: ai_provider_alerts
    rules:
      - alert: HighLatency
        expr: ai_capability_latency_seconds{quantile="0.95"} > 10
        for: 5m
        annotations:
          summary: "AI capability latency too high"
          
      - alert: LowSuccessRate
        expr: rate(ai_capability_calls_total{result="success"}[5m]) / rate(ai_capability_calls_total[5m]) < 0.7
        for: 10m
        annotations:
          summary: "AI capability success rate below 70%"
          
      - alert: HighTokenUsage
        expr: increase(ai_token_usage_total[1h]) > 100000
        annotations:
          summary: "High token usage detected"
```

## Rollback Procedure

### Automatic Rollback Triggers

Rollback should be triggered automatically if:
1. Success rate drops below 70% for 10 minutes
2. P95 latency exceeds 10 seconds for 5 minutes
3. Error rate exceeds 30% for 5 minutes
4. Critical system errors occur

### Manual Rollback Steps

**Immediate Rollback (Feature Flag):**
```bash
# Disable AI advisor via environment
export ENABLE_AI_ADVISOR="false"

# Or via code
config.enable_ai_advisor = False
```

**Service Rollback:**
```bash
# 1. Revert to previous version
git revert <commit-hash>

# 2. Redeploy
kubectl rollout undo deployment/uni-claw

# 3. Verify rollback
kubectl get pods -l app=uni-claw
```

**Data Rollback:**
```bash
# Archive current metrics/failures for analysis
mv .ai_failures.jsonl .ai_failures_$(date +%Y%m%d_%H%M%S).jsonl

# Clear metrics cache
rm -rf .ai_cache/
```

### Rollback Verification

1. **Check System Health**
   ```bash
   # Verify system is responsive
   curl -f http://localhost:8080/health || exit 1
   ```

2. **Check Capability Status**
   ```python
   from src.ai import NoOpAIAdvisor
   # Fall back to NoOp advisor
   advisor = NoOpAIAdvisor()
   ```

3. **Verify Data Integrity**
   - Check no data corruption during AI period
   - Validate state consistency

### Post-Rollback Analysis

1. **Collect Diagnostic Data**
   ```python
   failures = provider.get_failures(limit=1000)
   metrics = provider.get_metrics_summary()
   
   # Save for analysis
   with open("rollback_analysis.json", "w") as f:
       json.dump({"failures": failures, "metrics": metrics}, f)
   ```

2. **Identify Root Cause**
   - Review failure patterns
   - Check API service status
   - Analyze metrics trends

3. **Document Findings**
   - Create incident report
   - Update runbook
   - Implement fixes

## Testing Checklist

### Pre-Deployment Testing

- [ ] All unit tests pass
- [ ] Integration tests pass
- [ ] Vision service tests pass with real API
- [ ] Load tests complete (1000+ operations)
- [ ] Security scan passes
- [ ] Code review complete

### Staging Validation

- [ ] Configuration loads correctly
- [ ] All capabilities respond
- [ ] Metrics are collected
- [ ] Failures are archived
- [ ] Vision analysis accurate (>90%)
- [ ] Page verification accurate (>85%)
- [ ] Safety screening accurate (>95%)

### Production Smoke Tests

- [ ] System starts without errors
- [ ] AI advisor responds
- [ ] Vision analysis works
- [ ] Decisions are made
- [ ] Metrics are visible
- [ ] No critical errors in logs

## Troubleshooting

### Common Deployment Issues

**1. API Connection Failures**
```bash
# Test API connectivity
curl -X POST https://api.deepseek.com/v1/chat/completions \
  -H "Authorization: Bearer $DEEPSEEK_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"model":"deepseek-v4-flash","messages":[{"role":"user","content":"test"}]}'
```

**2. Vision Service Failures**
```bash
# Test Claude API
curl https://api.anthropic.com/v1/messages \
  -H "x-api-key: $VISION_API_KEY" \
  -H "anthropic-version: 2023-06-01" \
  -H "content-type: application/json" \
  -d '{"model":"claude-3-5-sonnet-20241022","max_tokens":1024,"messages":[{"role":"user","content":"test"}]}'
```

**3. Configuration Errors**
```python
# Validate configuration
from src.ai.config_loader import load_ai_config, validate_config

try:
    config = load_ai_config()
    validate_config(config)
    print("Configuration valid")
except ValueError as e:
    print(f"Configuration error: {e}")
```

## Performance Tuning

### Latency Optimization

1. **Reduce Reasoning Detail**
   ```python
   config.reasoning_detail = "concise"  # Faster, less accurate
   ```

2. **Enable Caching**
   ```python
   config = TraversalConfig(
       ai_cache_ttl=600,  # 10 minute cache
   )
   ```

3. **Increase Concurrency**
   ```python
   config.max_concurrent_requests = 8
   ```

### Cost Optimization

1. **Use Faster Models**
   ```python
   DEEPSEEK_MODEL = "deepseek-v4-flash"  # Most cost-effective
   ```

2. **Limit Context**
   ```python
   # Reduce context window size
   context.visited_pages.max_size = 50
   ```

3. **Monitor Usage**
   ```python
   # Set up alerts for 100k token threshold
   if metrics.get_token_usage()["total"] > 100000:
       logger.warning("Token usage alert threshold exceeded")
   ```

## Support and Escalation

### Level 1: Immediate Issues (First 24 hours)
- Check configuration
- Verify API keys
- Check service status
- Review logs

### Level 2: Persistent Issues (24-72 hours)
- Analyze failure patterns
- Review metrics trends
- Adjust prompts if needed
- Tune configuration

### Level 3: Fundamental Issues (72+ hours)
- Architecture review
- Model evaluation
- Feature reassessment
- Escalate to team lead

## References

- [UniBrain Documentation](../src/ai/README.md)
- [Design Decisions](../../openspec/changes/unibrain-ai-provider/design.md)
- [Implementation Tasks](../../openspec/changes/unibrain-ai-provider/tasks.md)

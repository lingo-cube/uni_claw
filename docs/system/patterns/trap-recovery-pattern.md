# Trap and Recovery Pattern

## 1. Trap

Trap 是一等 Runtime 模型。

Trap 不等于 Exception。

Exception 通常表示技术执行失败。

Trap 表示：

> 当前执行过程所依赖的状态假设可能已经失效，继续沿原控制流执行已经不可靠。

典型 Trap：

- ActionFailed
- TargetLost
- StateMismatch
- UnexpectedPage
- ContainerMismatch
- WorldLost
- PlanInvalid

Trap 至少应能够携带：

```text
Source
Scope
Kind
Expected
Observed
LastAction
Evidence
Timestamp
Recoverability
```

---

## 2. Trap Scope

推荐 Scope：

```text
Step
Container
Agent
```

### Step Scope

局部动作问题，例如：

- click timeout；
- coordinate stale；
- temporary target missing。

允许 Traversal 尝试：

```text
Retry
ReResolve
ReObserve
```

### Container Scope

问题仍然处于当前 Semantic Page 范围，例如：

- Popup；
- 页面局部结构变化；
- Scroll 状态异常；
- Candidate 消失；
- 需要重新 Ground。

由 Container 处理。

### Agent Scope

当前局部页面模型已经无法可信控制现实，例如：

- Desktop；
- App exited；
- Other application；
- Unknown semantic page；
- Plan invalid；
- 无法确认如何返回当前 Container。

由 Agent 处理。

原则：

```text
Lower Scope may recover locally.
If local recovery cannot be proven:
Escalate Upward.
```

低层不得偷偷执行高层恢复。

---

## 3. Recovery

Recovery 不是：

```text
PressBack()
```

Recovery 是完整协议：

```text
Detect
→ Diagnose
→ Plan Recovery
→ Execute
→ Observe
→ Verify
→ Reconcile
→ Resume
```

Recovery 成功必须经过 Observation + Verification。

禁止：

```text
Recovery Action returned success
→ assume recovered
```

如果无法验证，则 Recovery 仍未完成。

---

## 4. Recovery Mechanism

不同 Scope 不需要三套完全不同的 Recovery Framework。

可以考虑统一机制：

```text
RecoveryRequest
→ RecoveryPlanner
→ RecoveryPlan
→ RecoveryRuntime
→ RecoveryResult
```

RecoveryPlan 可以不同。

### Container Scope 示例

```text
Dismiss Popup
Reobserve
Reground
Retry
```

### Agent Scope 示例

```text
Go Home
Cold Launch
Restore Recovery Anchor
Navigate
Rebind Container
```

Mechanism 可以共享。

Authority 不共享。

---

## 5. Error、Trap、Failure

必须区分：

### Error

技术问题，例如：

- JSON parse failed
- Vision provider unavailable

### Trap

执行假设失效，例如：

```text
Expected Network Page
Observed Launcher
```

### Failure

某个执行范围最终无法完成，例如：

- Container recovery exhausted
- Run unable to restore environment

不要统一：

```text
catch(Exception)
→ ErrorHandling
```

处理所有问题。

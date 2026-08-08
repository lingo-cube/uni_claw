# UniClaw Agent Runtime Charter

> 定位: 系统使命、核心闭环、Architecture Spine 与总体原则。
> 硬约束见 `runtime-architecture-contract.md`；完整行为指导见 `../greenfield-runtime-charter.md`。

## 1. 文档目的

本 Charter 定义 UniClaw Agent Runtime 从零构建时的长期架构意图。

假设不存在历史 Runtime，不需要兼容旧的 `TraversalEngine`、`StepOrchestrator`、`Frame`、`InterceptionHandler` 等控制结构。设计必须从真实运行需求出发，而不是从历史类名或已有调用链出发。

默认技术栈：

- C#
- .NET 10
- async/await
- 强类型模型
- 尽可能不可变的数据结构
- 明确接口边界
- dependency injection
- xUnit
- 可测试、可观察、fail-fast

可以调整实现细节，但不得静默改变本 Charter 中的架构原则。

---

## 2. 系统目标

UniClaw 是一个运行在真实 GUI / Device Environment 上的智能执行 Runtime。

系统接收用户 Intent，例如：

- 打开系统设置中的 WiFi；
- 找到某个联系人并发送消息；
- 进入某个 App，完成一系列页面操作。

总体执行过程：

```text
Intent
→ Plan
→ Establish Environment
→ Observe
→ Understand
→ Decide
→ Execute
→ Verify
→ Update State
→ Continue
```

直到：

```text
Completed
```

或者：

```text
Failed / Terminated
```

真实设备环境并不是可靠的内部程序状态。设备可能发生：

- 页面加载延迟；
- 动画；
- Popup；
- Scroll；
- 页面结构变化；
- 元素位置变化；
- App 被关闭；
- 跳转到错误页面；
- 回到 Launcher；
- 外部事件改变当前页面；
- 操作已经执行但 Runtime 不知道是否成功。

因此 UniClaw 不是普通 Workflow Engine。

它必须持续回答：

1. 我想完成什么？
2. 我当前认为自己在哪里？
3. 现实世界实际上是什么？
4. 我的执行状态是否仍然可信？
5. 下一步应该执行什么？
6. 如果状态失配，应该在哪里恢复？
7. 恢复完成以后，如何验证并继续？

---

## 3. 核心控制闭环

Runtime 的核心不是：

```text
Plan → Execute
```

而是：

```text
Observe
   ↓
Reconcile
   ↓
Decide
   ↓
Execute
   ↓
Observe
   ↓
Verify
   ↓
Update
   ↓
Continue
```

如果：

```text
Expected World
≠
Observed World
```

则进入：

```text
Detect Trap
→ Determine Scope
→ Recover
→ Observe
→ Verify Recovery
→ Reconcile
→ Resume
```

任何架构设计都必须服务于这个闭环。

---

## 4. 第一原则：External World 不可信

设备是一个外部、弱状态，甚至可以视为无状态的执行环境。

程序内部记录：

```text
CurrentPage = WiFi
```

不能证明真实设备仍然在 WiFi 页面。

程序内部记录：

```text
ActionExecuted = true
```

不能证明操作真实生效。

因此：

```text
Internal Runtime State
≠
External World State
```

必须通过 Observation 对现实重新确认。

禁止设计：

> "FSM 处于某状态，所以现实一定处于对应状态。"

Runtime 必须允许：

```text
Observe
→ Discover Mismatch
→ Correct Belief
→ Correct Runtime
→ Continue / Recover
```

---

## 5. 核心架构

第一阶段只定义四个核心运行职责：

```text
Agent
→ Container
→ Traversal
→ Environment
```

支持能力包括：

- Startup
- World Model
- Planning
- Memory
- Recovery
- AI
- Observability

这些支持能力不是新的"核心层"。

不要未经必要性证明继续创造：

- TaskContainer
- ExecutionContainer
- PageAgent
- AgentFSM
- TraversalAgent
- WorldAgent

优先保持概念数量少而清晰。

---

## 6. Architecture Spine

第一阶段必须建立并稳定：

```text
                Agent
                  │
                  ▼
             World Belief
                  │
         ┌────────┴────────┐
         │                 │
      Decide          Active Container
                           │
                           ▼
                       Traversal
                           │
                           ▼
                       Environment
                           │
                           ▼
                      Observation
                           │
                           └──────────────→ Reconcile
```

异常路径：

```text
Traversal / Container / Environment
              │
              ▼
             Trap
              │
       Determine Scope
              │
              ▼
           Recovery
              │
              ▼
           Observe
              │
              ▼
            Verify
              │
              ▼
           Reconcile
              │
              ▼
            Resume
```

这条 Spine 比目录、类数量和 Pattern 更重要。

---

## 7. Greenfield 原则

Greenfield 的优势在于当前不存在历史兼容负担，因此：

- 不为不存在的兼容性设计 Adapter；
- 不提前建立 Legacy layer；
- 不模拟未来可能出现的迁移问题；
- 不因为某种框架习惯引入复杂架构；
- 每一项复杂度都必须由当前 Requirement 支付成本。

Greenfield 的目标不是第一天拥有完整 Agent Framework，而是第一天拥有正确的 Architecture Spine。

---

## 8. 总体原则

始终遵守：

- External world is authoritative.
- Observation is evidence, not semantic truth.
- World Belief is revisable.
- Plan is hypothesis, not reality.
- Memory is prior knowledge, not truth.
- Fingerprint is evidence, not identity.
- Agent owns global semantic authority.
- Container owns page-local runtime state.
- Traversal owns deterministic step execution.
- Environment owns interaction with the external world.
- FSM owns protocol transitions, not intelligence.
- Lower scope can escalate; it cannot steal higher-scope authority.
- One mutable state has one owner.
- One decision has one authority.
- Recovery is not an action; recovery is a verified process.
- Completion requires evidence against the Goal.
- AI augments deterministic execution; it does not replace Runtime architecture.
- Do not optimize architecture for hypothetical future complexity.
- Build the smallest correct system, then grow from real scenarios.

# AGT-002 Observer Demo Evidence

这份证据是可重复的 observer/read-model 场景输出。它使用现有
`AgentDecision` replay/fake realization，不把测试 realization 当成正式 Product
transport；真实 DSH/provider E2E 由本机 `127.0.0.1:3080` DSH service 承载，模型
选择为 `opencode-go/space-bunny-free` 或 `opencode-go/deepseek-flash`。

## Demo A — Act

```text
TASK             打开 Wi-Fi
MODEL            OpenCode / space-bunny-free (demo)
PRODUCT SESSION  session-demo-act
DSH SESSION      dsh-demo-act
CONSULTATIONS   1
DECISION        Act
TRACE           NeedDecision → Agent → Grounding → Assurance → Effect → Verify
EFFECTS         1 verified
RESULT          Goal satisfied
```

## Demo B — Policy

```text
TASK             温度从 24 调到 20
MODEL            OpenCode / space-bunny-free (demo)
PRODUCT SESSION  session-demo-policy
DSH SESSION      dsh-demo-policy
CONSULTATIONS   1
DECISION        Policy P-001
APPLICATIONS    4
TRACE           24 → 23 → 22 → 21 → 20
EFFECTS         4 verified
UNAUTHORIZED    0
RESULT          Goal satisfied
```

## Demo C — NoAction

```text
TASK             Wi-Fi 已经打开
CONSULTATIONS   1
DECISION        NoAction
EFFECTS         0
RESULT          Goal already satisfied
```

## Demo D — Defer

```text
TASK             目标控件的状态无法确认
CONSULTATIONS   1
DECISION        Defer
EFFECTS         0
RESULT          Waiting for observation
```

## Failure — abort / late response

```text
TURN            aborted
LATE RESPONSE   discarded
EFFECTS        0
PRODUCT RUN    remains Kernel-owned and continues independently
```

Observer 的 timeline 只关联 Product Trace、AgentDecision records、DSH trajectory
和 diagnostic；关闭、刷新或断开 projection reader 不会调用 Product command，也不
会改变 Run、World、Effect 或 Outcome。

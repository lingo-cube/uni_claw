# Environment Runtime Layer

## 1. 定义

Environment 是 Runtime 与外部世界之间的能力边界。

Environment 至少包含两类能力：

- Observation capabilities
- Action capabilities

例如：

- Vision
- OCR
- YOLO
- Screenshot
- Device Controller
- Tap
- Swipe
- Back
- Launch App
- External API

---

## 2. Environment 的职责

Environment 回答：

> "我现在能看到什么？"

以及：

> "请让我对世界执行这个动作。"

Environment 不回答：

> "为了完成任务下一步应该做什么？"

它不拥有任务决策权。

---

## 3. Ports Before Adapters

对于真正的外部能力优先定义 Port，例如：

- `IVisionProvider`
- `IDeviceController`
- `IActionExecutor`
- `IAISemanticResolver`
- `IMemoryStore`
- `IClock`

具体实现属于 Adapter。

不要为了形式主义给所有内部类都制造 `IXxxService`。

接口优先用于：

- 外部能力；
- 可替换策略；
- nondeterministic environment；
- AI Provider；
- Device；
- Vision；
- Storage；
- Clock。

---

## 4. Simulation First

第一阶段不要连接真实手机。

先实现 Simulation / Fake Environment，使 Runtime Architecture 可以确定性测试。

Simulation 应能够配置：

```text
Screen A
Click X → Screen B
Click Y → Popup
Unexpected event → Launcher
```

原因：

如果 Runtime 只能通过真实设备调试，就无法区分：

```text
Architecture Bug
```

和：

```text
Vision / Device Bug
```

等 Runtime Kernel 稳定后再接入：

- Android Adapter
- Vision Adapter
- 真实 AI Provider

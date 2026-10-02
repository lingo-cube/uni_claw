# 业界 UI Agent 遍历规划与动作安全设计审核

日期：2026-10-02

范围：审阅 UI/Settings 遍历中抽象计划、结构化与视觉感知、慢语义推理、规则/记忆/外部资料、动作前检查和动作后验证。优先采用 Android、Playwright、OpenAI Agents SDK 的一手文档。本文只做行业对照，不改变 UniClaw 源码或目标计划。

## 结论

UniClaw 目前收敛出的方向符合业界共同做法，但应把“计划”和“动作”明确分层：上层 Agent 生成受约束的抽象计划；运行时根据当前观察把计划实例化成一个候选动作；执行器重新定位、做动作前检查并执行；执行后用新的观察验证结果。Agent 不应直接输出坐标、selector 或点击命令。

“进入、记录、操作、禁止、未知”适合作为元素处理结果；“重新观察、重新规划、停止”适合作为流程控制结果。这个拆分比一个平面枚举更清楚，也与现有工具链的等待、重定位、断言和 guardrail 机制一致。

## 一手资料与观察

### 1. Android：结构化可观察性与动作本身是两件事

Android `UiAutomation` 明确把 UI 自动化定义为通过 Accessibility API 检查远程视图树，并执行动作；同时提供 `getRootInActiveWindow()`、截图、等待空闲和全局动作等能力。官方还指出底层 API 追求灵活性，通常应由更高层库封装。

来源：[UiAutomation API](https://developer.android.com/reference/android/app/UiAutomation)，摘要定位：`UiAutomation` 类说明及 `getRootInActiveWindow`, `takeScreenshot`, `waitForIdle`。

AndroidX UiAutomator 的 `onElement`/`onElements` 会在有限超时内周期性重新搜索无障碍树；文档还建议在多个元素分时出现时先等待 UI 稳定。这支持“观察 → 等待/重新观察 → 再定位”的循环，而不是保存一次观察中的旧节点长期使用。

来源：[AccessibilityWindowInfoExt](https://developer.android.com/reference/androidx/test/uiautomator/AccessibilityWindowInfoExt)，摘要定位：`onElement`, `onElements`, `timeoutMs`, `pollIntervalMs`, `waitForStable`。

Android 的 AccessibilityAction 区分标准动作、控件自定义动作和覆盖标准动作；动作是否存在是节点能力事实，不等于当前任务一定允许执行。

来源：[AccessibilityNodeInfo.AccessibilityAction](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo.AccessibilityAction)，摘要定位：三类 action 及 `performAccessibilityAction` 说明。

AndroidX 还提供在每次 UI action 前运行的 `UiAccessibilityValidator`，验证失败可以阻止动作。这是“动作前安全门”由执行侧拥有的直接例子。

来源：[UiAccessibilityValidator](https://developer.android.com/reference/androidx/test/uiautomator/UiAccessibilityValidator)，摘要定位：`validate` 在 click/scroll 等 action 前调用，异常会导致测试失败。

### 2. Playwright：计划给出意图，执行器负责当前元素可操作性和后验

Playwright 把 Locator 定义为“在当前页面上每次需要时重新寻找元素”的对象，推荐按 role、text、label 等语义定位。Locator 不是一次快照；元素动态变化时，直接枚举而不等待稳定会产生不可预测结果。

来源：[Locators](https://playwright.dev/docs/locators)，摘要定位：Locator 是 auto-waiting/retryability 的中心；推荐 `getByRole` 等；`locator.all()` 对动态列表不等待且可能 flaky。

点击前，Playwright 会检查 locator 是否唯一、可见、稳定、能接收事件且启用；检查不满足会等待到超时并失败。点击后，Web-first assertions 会反复重新获取节点并验证期望状态。

来源：[Auto-waiting](https://playwright.dev/docs/actionability)，摘要定位：`locator.click()` 的 Visible/Stable/Receives Events/Enabled 检查；[Assertions](https://playwright.dev/docs/api/class-playwrightassertions)，摘要定位：断言会重取节点并重试直到满足或超时。

这对应 UniClaw 的边界：Agent 可以说“进入设置项”或“处理一个可取消弹窗”，但 Kernel 必须在当前 observation 上重新 grounding，验证元素仍唯一、稳定、可接收事件，再执行并检查预期结果。

### 3. OpenAI Agents SDK：计划、工具、记忆、交接和 guardrail 是可组合但不同的层

OpenAI Agents SDK 将 Agent、工具、handoff、guardrail、session 和 tracing 作为不同原语。其 orchestration 文档明确区分两种模式：LLM 决定下一步，或由代码编排流程；复杂系统可以混合两者。

来源：[OpenAI Agents SDK](https://openai.github.io/openai-agents-python/)，摘要定位：核心原语列表；[Agent orchestration](https://openai.github.io/openai-agents-python/multi_agent/)，摘要定位：LLM orchestration 与 code orchestration 两种模式及 agents-as-tools/handoffs。

SDK 的 tools 允许 Agent 调用预先提供的工具，guardrail 可以在工具执行前后校验并阻断；工具输入/输出 guardrail 的职责是检查工具调用，而不是让模型获得绕过执行器的能力。

来源：[Guardrails](https://openai.github.io/openai-agents-python/guardrails/)，摘要定位：tool input/output guardrails、tripwire 和阻断语义；[Agents](https://openai.github.io/openai-agents-python/agents/)，摘要定位：tools、handoffs、input/output guardrails、structured output。

SDK 的 Session memory 保存会话历史；其 sandbox memory 文档另行说明记忆可用于让后续运行吸收经验，但读取记忆和生成记忆可分别控制。这支持“记忆帮助形成计划”，不支持“记忆本身就是当前页面事实或动作授权”。

来源：[Sessions](https://openai.github.io/openai-agents-python/sessions/)，摘要定位：每轮自动读取/保存会话历史；[Agent memory](https://openai.github.io/openai-agents-python/sandbox/memory/)，摘要定位：memory 与 session 分开，且可配置只读 memory。

Responses API 的工具配置可以把可用工具限制为预定义集合，并配置 tool choice；MCP 工具还可以按只读属性或名称配置审批。这说明外部资料/工具应通过显式能力和审批边界进入计划，不应隐式扩大动作权限。

来源：[Responses API tool choice](https://platform.openai.com/docs/api-reference/responses-streaming/response/refusal?lang=python)，摘要定位：`tools`、`tool_choice` 和 allowed tools；[Realtime tools/approvals](https://platform.openai.com/docs/api-reference/realtime?lang=javascript)，摘要定位：MCP tool approval filter。

## 对 UniClaw 当前方案的审核

### 保留

1. `uni-agent` 为整个 Kernel 提供抽象遍历计划是合理的。计划应表达元素类别的处理意图、顺序、适用条件、禁止项和停止条件，不表达坐标或一次性 selector。
2. Slow 是语义观察来源；Fast/XML/截图提供输入上下文。无 XML 不是异常分支，慢智能可以在文本/布局上下文足够时高可靠识别页面和元素语义；视觉 Slow 是否可用由 profile 配置决定。
3. Kernel 保持执行权和动作安全门。Agent/记忆/外部资料可以提出计划或策略，不能直接授权过期目标、坐标点击或跳过动作前检查。
4. 一次一个动作，动作后重新观察并验证，是合理的最小闭环。页面、弹窗、目标或 contract 变化后重新咨询，避免复用过期策略。
5. 元素处理五类与流程控制三类的拆分清晰：
   - 元素：进入、记录、操作、禁止、未知；
   - 流程：重新观察、重新规划、停止。

### 需要在目标文档中写清楚的边界

1. 抽象计划不是静态脚本。它应允许在 `Unknown`、冲突、动作验证失败或策略信息不足时请求 Slow、重新观察或重新规划；但重新规划不能放宽人工预设的不可逆保护规则。
2. “操作”不等于“点击允许”。它还需要动作类别、当前目标绑定、策略来源/版本、动作前检查、执行结果和后验观察。
3. 规则、记忆和外部资料的作用是帮助 Agent 形成计划或解释语义。它们不能单独证明当前按钮存在，也不能单独授权当前 Effect；当前授权仍须回到新鲜观察、策略约束和 Kernel/Assurance。
4. XML 对结构事实最强，Slow 对语义理解最强，视觉/OCR/YOLO 可作补充。冲突不能由“哪个模型置信度更高”粗暴解决，应按判断类型分工并保留 `Unknown/Conflict`。
5. RouteKey 和 ViewportDigest 仍应分开。页面身份由标题、布局、上下文等多源信号组合；滚动完成由重新观察后的稳定视口变化判断。不能因为没有 XML 就退化为只看标题。
6. `uni-agent` 可以提供整体现象的遍历计划，也可以在障碍或失败后提供新的局部建议；但目标完成判断、Kernel 执行、安全门、证据准入仍要保持独立职责。

## 与 UniClaw 当前设计的主要冲突点

- 当前 Settings live feed 的 route 仍主要由标题生成，和业界“当前时刻重新定位/多信号 identity”不一致；应改为组合身份，并把不确定性保留下来。
- 当前 Fast/Settings 链路还没有把 Slow 结果真正接入页面、弹窗、元素分类和 Agent 计划上下文；只定义异步基础设施不等于业务链路已完成。
- 当前 Settings 遍历更接近“下一个 descriptor”，尚未正式承载“进入/记录/操作/禁止/未知”的抽象计划结果；这会让 Host 自己承担本应由 Agent 规划的策略。
- 若 `SettingsCoverageDirector` 把 Agent 的偏离一律当错误，会阻断合法的“清障 → 验证 → 回到遍历”路径；应允许受约束的重新观察/重新规划结果，并保持每步动作验证。
- HostRunner 当前 evidence wiring 不完整时，无法稳定证明策略来源、慢结果、动作前后状态和最终覆盖；应把 evidence sink 接线列为可验证性前提，而不是塞入感知或 Agent 模块。

## 推荐收敛的最小模型

```text
目标与人工保护规则
  → uni-agent 结合规则、记忆、必要外部资料生成抽象计划
  → Fast/XML/截图观察当前页面
  → 条件触发 Slow，补充页面/元素语义
  → Kernel 按计划把一个元素实例化为候选动作或记录结果
  → 动作前重新定位、检查唯一/稳定/可操作/策略允许
  → 执行一个动作
  → 重新观察并验证结果
  → 成功则继续；Unknown/冲突则重新观察；计划走不通则重新规划；达到边界则停止
```

这个模型与 Android 的结构化观察和 action validator、Playwright 的 locator/actionability/assertion、OpenAI Agents 的 tools/guardrails/sessions/orchestration 方向一致；它支持高智能规划，同时把实时事实、动作执行和安全授权留在各自边界内。

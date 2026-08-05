---
name: trace-to-simulation
description: 从真实 run 产物按 FSM 时序构建仿真测试用例，复现问题并给出修改意见。核心是 FSM 验证，vision 可配置接入（trace replay / mock / 自定义）。与 host-test-runner 和 trace-analyzer agent 串联形成完整闭环。
metadata:
  author: uni-claw-ai-team
  version: "2.0"
  tags: [trace, simulation, fsm, testing, debugging, fsm-verification]
---

# Trace-to-Simulation Skill

从真实 run 的产物按 FSM 时序还原执行路径，构建可复现的仿真测试用例。
**核心是 FSM 验证，vision 可配置接入。**

```
真实 run 产物 → 时序分析 → FSM 回放 → 诊断 → 修复验证
```

## Skill / Agent 串联工作流

```
┌─────────────────────────────────────────────────────────────┐
│  host-test-runner (skill)                                    │
│  Phase 1-6: 模拟器 → 执行 → 产物 → 浅层报告                  │
│                          │                                    │
│                          ├─ 成功 → done                       │
│                          └─ 失败 → 产物目录                    │
│                                    │                          │
│              ┌─────────────────────┤                          │
│              ↓                     ↓                          │
│  trace-analyzer (agent)    trace-to-simulation (skill)       │
│  深度归因: L1→L4 分层      产物回放: analysis.jsonl          │
│  root cause + evidence     → TraceReplayHarness              │
│  + 资产缺失回报             → 复现 FSM 执行路径               │
│              │                     │                          │
│              └─────────┬───────────┘                          │
│                        ↓                                      │
│                  修复代码                                      │
│                        │                                      │
│                        ↓                                      │
│              trace-to-simulation (skill)                      │
│              RunWithPlan(fixedPlan) → 验证修复                │
│              < 1s/次迭代，不需要模拟器                         │
│                        │                                      │
│                        ├─ 失败 → 继续修 → 重跑 replay         │
│                        └─ 通过 → host-test-runner E2E 确认    │
└─────────────────────────────────────────────────────────────┘
```

| 阶段 | 工具 | 耗时 | 职责 |
|------|------|------|------|
| 执行 | host-test-runner skill | 3-5 min | 产生 run 产物 |
| 归因 | trace-analyzer agent | ~30s | 深度诊断, 资产缺失回报 |
| 复现 | trace-to-simulation skill | < 1s | 产物回放, FSM 验证 |
| 修复 | trace-to-simulation skill | < 1s × N | RunWithPlan 迭代验证 |
| 确认 | host-test-runner skill | 3-5 min | E2E 模拟器终验 |

## When to Use

- 集成测试失败 / 卡死后，需要将真实执行路径转化为可复现的仿真测试
- 需要在 CI 中固化某个已知 bug 的复现用例
- 需要理解某个 run 的 FSM 状态转换时序

## 前置知识

执行前掌握：

| 层 | 内容 | 来源 |
|----|------|------|
| FSM | TraversalState 8 值 + GlobalState 8 值 + 合法转换矩阵 | `docs/system/layers/state-machine.md` |
| 产物 | trace.jsonl span 树 / analysis.jsonl 时序快照 / run.log 动作日志 / plan.json 计划 / result.json 结局 | `docs/system/layers/observability.md` |
| 仿真 | StateFixture / StateFixtureBuilder / PageState / PageTransition / StatefulMockVisionService / StatefulMockActionExecutor | `src/UniClaw.Core/Simulation/` |
| 引擎 | TraversalEngine / TraversalPlan / DynamicRule / PlanCompiler | `src/UniClaw.Core/Graph/` `src/UniClaw.Core/StateMachine/` |

---

## Phase 1 — 时序提取

### 1.1 读取产物

```bash
# run 目录结构
{runDir}/
  result.json          # 结局: status, completionReason, steps, actions
  plan.json            # 计划: DynamicRules, templateRegistry, depth
  criteria.json        # 验证: expectedPageIdentities
  trace/{runId}/
    trace.jsonl        # span 树 + execution + transition
    run.log            # FSM 转换 + 动作 + 错误
  assets/{runId}/
    analysis.jsonl     # 每次页面分析的快照（按时间排序）
```

### 1.2 构建时序表

从 `run.log` 提取 FSM 转换序列：
```
提取: grep "TraversalFSM:" run.log → FSM From→To step=N
提取: grep "SafeActionExecutor:" run.log → action=X result=Y
提取: grep "InvalidatingPageAnalysisCache:" run.log → page= items=N
提取: grep "Engine terminated" run.log → reason=X
```

从 `analysis.jsonl` 提取页面快照序列（每行 = 一次分析）：
```
row 0: 16 items, first="Settings"
row 1: 16 items, first="Settings"  
row 2: 16 items, first="Settings"
row 10: 21 items, first="Network & internet"  ← 页面变化
...
```

从 `trace.jsonl` 提取 span 树 + execution 事件：
```
提取: record_type=="span" → spanType, parentSpanId, durationMs
提取: record_type=="execution" → 动作执行结果
提取: record_type=="transition" → FSM 状态转换
提取: record_type=="error" → 错误详情
```

### 1.3 按步构建执行路径

将上述三条时序合并为统一的 **step-by-step 执行路径表**：

| Step | FSM Transition | Page (first item) | Items | Action | Result |
|------|---------------|-------------------|-------|--------|--------|
| 1 | NodeSelect→PreconditionCheck | Settings | 16 | - | - |
| 2 | PreconditionCheck→Execute | Settings | 16 | click "Network & internet" | ok |
| 3 | Execute→ResultVerify | Network & internet | 21 | - | - |
| ... | ... | ... | ... | ... | ... |

---

## Phase 2 — 场景分类

按执行路径的异常模式分类为独立场景：

| 场景 | 判定条件 | 优先级 |
|------|---------|--------|
| `search-box-stuck` | 搜索/输入框类型元素被点击 → 引擎卡在搜索界面 | 🔴 |
| `dfs-revisit-loop` | depth=N 回退到 depth=N-1 → 重新进入同一子节点 | 🔴 |
| `home-not-restored` | completionReason 含 "not_restored" | 🔴 |
| `scroll-no-progress` | 连续滚动但页面未变化（坐标签名全等） | 🟡 |
| `swipe-misnavigation` | 滚动触发导航到非预期页面 | 🟡 |

---

## Phase 3 — 构建仿真 Fixture

### 3.1 页面抽取

从 `analysis.jsonl` 中提取**页面身份变化点**（`first item name` 变化即为页面切换）：

```
page_id = first_item_name（去空格、小写、替换特殊字符）
例如: "Network & internet" → "network_internet"
```

每个页面抽 items（name + type + coordinate）：
```python
for row in analysis_rows:
    first = row['items'][0]['name']
    page_id = sanitize(first)
    if page_id not in pages:
        pages[page_id] = {
            'name': first,
            'items': [
                {'name': item['name'], 'type': item['type'], 
                 'x': item['x'], 'y': item['y']}
                for item in row['items']
            ]
        }
```

### 3.2 跳转推断

从 FSM + action 序列推断页面跳转规则：

1. 找到所有 `action=click` 事件
2. 定位 click 前后的页面变化点（analysis.jsonl row）
3. 推断: `(from_page, clicked_item_name) → to_page`

```python
transitions = []
for step in execution_path:
    if step.action == 'click':
        from_page = page_before_click(step)
        to_page = page_after_click(step)
        clicked_item = find_item_in_page(from_page, step.click_target)
        transitions.append({
            'from': from_page,
            'trigger': clicked_item['name'],
            'to': to_page
        })
```

### 3.3 构建 StateFixture

```csharp
var fixture = new StateFixtureBuilder()
    .Page("settings", p => p
        .Name("Settings")
        .MenuItem("network_internet", "Network & internet", 0.5, 0.22)
        .MenuItem("connected_devices", "Connected devices", 0.5, 0.29)
        // ... 从 analysis.jsonl row 0 提取所有 items
    )
    .Page("network_internet", p => p
        .Name("Network & internet")
        .MenuItem("internet", "Internet", 0.5, 0.15)
        .MenuItem("sims", "SIMs", 0.5, 0.22)
        // ... 从 analysis.jsonl row 10 提取
    )
    .Transition(t => t.Id("go_network").Click("network_internet").From("settings").To("network_internet"))
    .Transition(t => t.Id("go_internet").Click("internet").From("network_internet").To("internet"))
    // ...
    .Build();
```

---

## Phase 4 — 构建仿真测试用例

### 4.1 测试命名规范

```
{run_id_short}_{scenario_slug}
例如: 20260805T052309367Z_settings_home_not_restored
```

### 4.2 测试结构

```csharp
[Fact(DisplayName = "复现: settings_home_not_restored — DFS 回退后重新进入已访问子节点")]
public async Task Run_20260805T052309367Z_SettingsHomeNotRestored()
{
    // 1. 构建 fixture（从真实产物提取）
    var fixture = BuildFixtureFromTrace();

    // 2. 构建 plan（与真实 run 一致）
    var plan = new TraversalPlan(
        EntryApp: "com.android.settings",
        EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
        PlanName: "com.android.settings_full",
        PlanId: "test-enumerate",
        RootNode: CreateEnumerateRootNode(),  // 4 DynamicRules (safe_mode)
        StaticNodes: new Dictionary<string, TraversalNode>(),
        TemplateRegistry: new Dictionary<string, TraversalTemplate>(),
        CompletionPolicy: new CompletionPolicy(
            CompletionPolicyType.VerifiedEndOfList, [], 
            IdentityMatchMode.Contains, ActionOnFound.ObserveOnly));

    // 3. 创建引擎
    var engine = CreateEngine(fixture, plan);

    // 4. 运行
    var result = await engine.RunAsync(CancellationToken.None);

    // 5. 断言期望的失败模式（复现）
    Assert.Equal(CompletionReason.MaxSteps, result.CompletionReason);
    // 或者: 期望成功但实际失败 → 标记为已知 bug
}

private static StateFixture BuildFixtureFromTrace()
{
    // 从 analysis.jsonl 提取的页面定义
    return new StateFixtureBuilder()
        .Page("settings", p => p
            .Name("Settings")
            .MenuItem("network_internet", "Network & internet", 0.5, 0.22)
            .MenuItem("connected_devices", "Connected devices", 0.5, 0.29)
            // ... more items
        )
        .Page("network_internet", p => p
            .Name("Network & internet")
            .MenuItem("internet", "Internet", 0.5, 0.15)
            .MenuItem("sims", "SIMs", 0.5, 0.22)
        )
        .Page("internet", p => p
            .Name("Internet")
            .MenuItem("wifi", "Wi‑Fi", 0.5, 0.15)
        )
        .Transition(t => t.Id("t1").Click("network_internet").From("settings").To("network_internet"))
        .Transition(t => t.Id("t2").Click("internet").From("network_internet").To("internet"))
        .Build();
}
```

---

## Phase 5 — 复现验证 + 修改建议

### 5.1 复现检查

- 测试是否复现了真实 run 的相同失败模式？
- FSM 状态转换路径是否匹配？
- completionReason 是否一致？

### 5.2 修改建议

复现后，根据 fixture 中暴露的问题给出建议：

| 问题类型 | 建议方向 |
|---------|---------|
| DFS 回退重复访问 | visited-children 跟踪在 back 后未清理 → 修复 NavigationContext |
| 搜索框误点击 | type=menu_item 应改 input → 修复 label-mapping / fusion pre-labeling |
| 回不到首页 | restore 机制未生效 → 检查 restore 配置 + back-navigation 链 |
| 滚动无效循环 | ROI 检测未区分"首屏未生效"与"到底" → 修复 EndReached 判定 |

### 5.3 输出格式

```
═══════════════════════════════════════════════
  Trace-to-Simulation Report
═══════════════════════════════════════════════

📋 Source Run
   runId:    20260805T052309367Z-1bc7a25ea6384e3
   scenario: enumerate-settings-safely
   outcome:  max_steps (120), settings_home_not_restored

🔬 Extracted Scenarios
   1. dfs-revisit-loop: Settings→Network→Internet→back→Internet (loop)
   2. (none other significant)

🧪 Simulation Tests Generated
   tests/UniClaw.Core.Tests/Simulation/TraceReplay/
   ├─ 20260805T052309367Z_dfs_revisit_loop.cs
   └─ 20260805T052309367Z_search_box_stuck.cs

📊 Reproduction
   dfs-revisit-loop: ✅ REPRODUCED (max_steps after 120 steps)
   
💡 Fix Suggestions
   1. NavigationContext.VisitedChildren 在 back 后未清理当前层级
      → 修复: BackNavigationHandler 中 back 后调用 MarkChildVisited
   2. restore=false 导致引擎不尝试回 Settings
      → 建议: enumerate 场景默认 restore=true
═══════════════════════════════════════════════
```

---

## 硬约束

1. **不修改源码** — 只构建测试用例和 fixture
2. **测试落目录** — `tests/UniClaw.Core.Tests/Simulation/TraceReplay/`
3. **命名规范** — `{runIdShort}_{scenarioSlug}` (runIdShort = 前 17 位: yyyyMMddTHHmmssZ)
4. **产物只读** — 不写回 run 目录
5. **fixture 从 analysis.jsonl 提取** — 不做手工编造页面数据

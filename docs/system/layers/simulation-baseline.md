# Layers — Simulation Baseline

> **Tier 3 · Layers**: Simulation 基线端到端测试规格书。改基线场景/规则/数值时更新。
> 约束: → constitution C-11 (基线 E2E 回归门槛)
> 配套代码层规格: → layers/simulation.md
> Python 对齐参考: 本文档从 Python `expected_behavior.yaml` + `simulation-ci.yaml` 提取

---

## 0. 项目结构与索引

基线测试体系横跨**文档层**和**代码层**，完整索引如下：

### 文档层 (docs/system/)

| 文件 | 层级 | 内容 | 更新触发 |
|------|------|------|---------|
| `constitution/constraints.md` C-11 | Tier 1 | 基线 E2E 回归门槛原则: "必须通过，回归 = CI-blocking" | 新增基线场景或发现回归 |
| `layers/simulation-baseline.md` | Tier 3 | **本文件**: 2 核心场景定义 + 7 类规则体系 + 基线数值 + Python↔C# 对照 | 改基线场景/规则/数值 |
| `layers/simulation.md` | Tier 3 | Simulation 代码层规格 (类型清单、数据流、依赖) | 改 Simulation 代码 |

### 代码层 (tests/)

| 文件 | 目录 | 内容 | 性质 |
|------|------|------|------|
| `SimulationBaselineTests.cs` | `tests/.../Baseline/` | 场景1+2 C# 测试代码 + 内联 7页 fixture (Assert 验证) | **功能回归 guard** — CI-blocking |
| `ScrollableBaselineTests.cs` | `tests/.../Baseline/` | 场景1-6 滚动基线测试代码 + 内联 3 fixture (ExpectedBehavior 验证) | **功能回归 guard** — CI-blocking |
| `ArchitectureGuardTests.cs` | `tests/.../Architecture/` | 架构约束 guard (C-1~C-8) | **架构约束 guard** — CI-blocking |
| `SimulationE2ETests.cs` | `tests/.../Simulation/` | 2-page/4-page 开发验证 E2E | 普通 E2E (非基线) |

### 三类测试的区分

| 目录 | 性质 | CI-blocking | 失败语义 | 对应文档 |
|------|------|-------------|---------|---------|
| `Architecture/` | 架构约束 guard | ✅ 阻断 | 规则违反，修代码 | constitution/* |
| `Baseline/` | 功能回归 guard | ✅ 阻断 | 主功能退化，修代码 | C-11 + simulation-baseline.md |
| `Simulation/` | 普通 E2E / 单元 | ✅ 阻断 | 功能不工作，排查 | layers/simulation.md |

### 紧密配对关系

```
constitution/constraints.md (C-11 原则)
  ↕ 交叉验证
layers/simulation-baseline.md (场景定义 + 基线数值)
  ↕ 测试断言映射
tests/.../Baseline/SimulationBaselineTests.cs (代码验证 + 内联 fixture, D-B1)
```

### Python↔C# 资产对照

| Python 资产 | C# 等价 | 位置 |
|------------|---------|------|
| `expected_behavior.yaml` (规则+数值) | `simulation-baseline.md` (文档) + `SimulationBaselineTests.cs` (代码断言) | docs/ + tests/ |
| `simulation-ci.yaml` (CI配置) | GitHub Actions workflow (dotnet test filter) | `.github/workflows/` |
| `run_simulation_ci.py` (调度脚本) | `dotnet test --filter "FullyQualifiedName~Baseline"` | 内置，无独立脚本 |
| `test_settings_simulation.py` (全量遍历) | `SimulationBaselineTests.cs` 全量遍历场景 | `tests/.../Baseline/` |
| `test_target_search.py` (目标搜索) | `SimulationBaselineTests.cs` 目标搜索场景 | `tests/.../Baseline/` |
| `settings_page.json` (fixture 数据) | `SettingsAppFixture7Pages()` 内联方法 (D-B1: 内联优先) | `tests/.../Baseline/SimulationBaselineTests.cs` |
| `expected_behavior.py` (行为定义类) | `ExpectedBehavior` sealed record class + `VerificationReport` + JSON fixture files | `src/.../Simulation/ExpectedBehavior/` + `tests/.../Baseline/Fixtures/expected/` |

---

## 1. 核心场景

两个场景共享同一 fixture (7 页 Settings App)。区别仅在 TraversalPlan 配置和验证断言。

### 1.0 共享 Fixture: Settings App (7 页 + 2 子页)

#### 页面定义

| 页面 ID | 页面名 | 元素数 | 元素列表 |
|---------|--------|--------|---------|
| `home` | Settings | 6 | menu_wifi(B), menu_bluetooth(B), menu_display(B), menu_storage(B), menu_battery(B), menu_apps(B) |
| `wifi` | Wi-Fi | 3 | wifi_switch(S,ON), network_1(B,HomeNetwork), network_2(B,OfficeWiFi), network_3(B,GuestNetwork) |
| `bluetooth` | Bluetooth | 3 | bluetooth_switch(S,ON), device_1(B,Headphones Pro), device_2(B,Speaker Mini) |
| `display` | Display | 3 | brightness(SL,Brightness level), wallpaper(B), dark_mode(S,Dark mode) |
| `storage` | Storage | 2 | internal_storage(B,Internal Storage), external_storage(B,SD Card) |
| `storage_internal` | Internal Storage | 3 | apps_usage(R), media_usage(R), system_usage(R) — 全只读 |
| `storage_external` | SD Card | 2 | photos_usage(R), videos_usage(R) — 全只读 |

元素类型缩写: B=button, S=switch, SL=slider, R=readonly

#### 元素坐标 (C# StateFixtureBuilder 用)

| 页面 | 元素 ID | 类型 | text | x | y |
|------|---------|------|------|---|---|
| home | menu_wifi | button | Wi-Fi | 0.50 | 0.13 |
| home | menu_bluetooth | button | Bluetooth | 0.50 | 0.22 |
| home | menu_display | button | Display | 0.50 | 0.31 |
| home | menu_storage | button | Storage | 0.50 | 0.40 |
| home | menu_battery | button | Battery | 0.50 | 0.50 |
| home | menu_apps | button | Apps | 0.50 | 0.59 |
| wifi | wifi_switch | switch | ON | 0.90 | 0.07 |
| wifi | network_1 | button | HomeNetwork | 0.50 | 0.15 |
| wifi | network_2 | button | OfficeWiFi | 0.50 | 0.24 |
| wifi | network_3 | button | GuestNetwork | 0.50 | 0.33 |
| bluetooth | bluetooth_switch | switch | ON | 0.90 | 0.07 |
| bluetooth | device_1 | button | Headphones Pro | 0.50 | 0.15 |
| bluetooth | device_2 | button | Speaker Mini | 0.50 | 0.24 |
| display | brightness | slider | Brightness level | 0.50 | 0.13 |
| display | wallpaper | button | Wallpaper | 0.50 | 0.22 |
| display | dark_mode | switch | Dark mode | 0.50 | 0.31 |
| storage | internal_storage | button | Internal Storage | 0.50 | 0.14 |
| storage | external_storage | button | SD Card | 0.50 | 0.25 |
| storage_internal | apps_usage | readonly | Apps: 25GB | 0.50 | 0.12 |
| storage_internal | media_usage | readonly | Media: 15GB | 0.50 | 0.17 |
| storage_internal | system_usage | readonly | System: 5GB | 0.50 | 0.22 |
| storage_external | photos_usage | readonly | Photos: 1.5GB | 0.50 | 0.12 |
| storage_external | videos_usage | readonly | Videos: 500MB | 0.50 | 0.17 |

坐标计算规则: `x = (bounds_left + bounds_right) / 2 / 500`, `y = (bounds_top + bounds_bottom) / 2 / 1080`

#### Transition 表

| ID | trigger | from_page | to_page | action |
|----|---------|-----------|---------|--------|
| home_to_wifi | menu_wifi | home | wifi | click |
| home_to_bluetooth | menu_bluetooth | home | bluetooth | click |
| home_to_display | menu_display | home | display | click |
| home_to_storage | menu_storage | home | storage | click |
| wifi_to_home | btn_back | wifi | home | back |
| bluetooth_to_home | btn_back | bluetooth | home | back |
| display_to_home | btn_back | display | home | back |
| storage_to_home | btn_back | storage | home | back |
| storage_to_internal | internal_storage | storage | storage_internal | click |
| storage_to_external | external_storage | storage | storage_external | click |
| internal_to_storage | btn_back | storage_internal | storage | back |
| external_to_storage | btn_back | storage_external | storage | back |

注意: Python 原版有 back_button 元素在子页面 (wifi/bluetooth/display/storage) 和 Storage 子页面 (internal/external)。C# StateFixtureBuilder 用 `.BackButton(id, x, y)` 自动生成。

#### C# StateFixtureBuilder 代码

```csharp
private static StateFixture SettingsAppFixture() => new StateFixtureBuilder()
    .Page("home", p => p
        .Name("Settings")
        .Button("menu_wifi", "Wi-Fi", 0.50, 0.13)
        .Button("menu_bluetooth", "Bluetooth", 0.50, 0.22)
        .Button("menu_display", "Display", 0.50, 0.31)
        .Button("menu_storage", "Storage", 0.50, 0.40)
        .Button("menu_battery", "Battery", 0.50, 0.50)
        .Button("menu_apps", "Apps", 0.50, 0.59))
    .Page("wifi", p => p
        .Name("Wi-Fi")
        .Switch("wifi_switch", "ON", 0.90, 0.07)
        .Button("network_1", "HomeNetwork", 0.50, 0.15)
        .Button("network_2", "OfficeWiFi", 0.50, 0.24)
        .Button("network_3", "GuestNetwork", 0.50, 0.33)
        .BackButton("btn_back_w", 0.05, 0.05))
    .Page("bluetooth", p => p
        .Name("Bluetooth")
        .Switch("bluetooth_switch", "ON", 0.90, 0.07)
        .Button("device_1", "Headphones Pro", 0.50, 0.15)
        .Button("device_2", "Speaker Mini", 0.50, 0.24)
        .BackButton("btn_back_bt", 0.05, 0.05))
    .Page("display", p => p
        .Name("Display")
        .Switch("brightness", "Brightness level", 0.50, 0.13)  // slider mapped as switch in mock
        .Button("wallpaper", "Wallpaper", 0.50, 0.22)
        .Switch("dark_mode", "Dark mode", 0.50, 0.31)
        .BackButton("btn_back_d", 0.05, 0.05))
    .Page("storage", p => p
        .Name("Storage")
        .Button("internal_storage", "Internal Storage", 0.50, 0.14)
        .Button("external_storage", "SD Card", 0.50, 0.25)
        .BackButton("btn_back_s", 0.05, 0.05))
    .Page("storage_internal", p => p
        .Name("Internal Storage")
        .Readonly("apps_usage", "Apps: 25GB", 0.50, 0.12)
        .Readonly("media_usage", "Media: 15GB", 0.50, 0.17)
        .Readonly("system_usage", "System: 5GB", 0.50, 0.22)
        .BackButton("btn_back_si", 0.05, 0.05))
    .Page("storage_external", p => p
        .Name("SD Card")
        .Readonly("photos_usage", "Photos: 1.5GB", 0.50, 0.12)
        .Readonly("videos_usage", "Videos: 500MB", 0.50, 0.17)
        .BackButton("btn_back_se", 0.05, 0.05))
    .Transition(t => t.Id("home_to_wifi").Click("menu_wifi").From("home").To("wifi"))
    .Transition(t => t.Id("home_to_bt").Click("menu_bluetooth").From("home").To("bluetooth"))
    .Transition(t => t.Id("home_to_d").Click("menu_display").From("home").To("display"))
    .Transition(t => t.Id("home_to_s").Click("menu_storage").From("home").To("storage"))
    .Transition(t => t.Id("wifi_back").Click("btn_back_w").From("wifi").To("home"))
    .Transition(t => t.Id("bt_back").Click("btn_back_bt").From("bluetooth").To("home"))
    .Transition(t => t.Id("d_back").Click("btn_back_d").From("display").To("home"))
    .Transition(t => t.Id("s_back").Click("btn_back_s").From("storage").To("home"))
    .Transition(t => t.Id("s_to_si").Click("internal_storage").From("storage").To("storage_internal"))
    .Transition(t => t.Id("s_to_se").Click("external_storage").From("storage").To("storage_external"))
    .Transition(t => t.Id("si_back").Click("btn_back_si").From("storage_internal").To("storage"))
    .Transition(t => t.Id("se_back").Click("btn_back_se").From("storage_external").To("storage"))
    .Build();
```

#### 与 Python fixture 差异

| 差异点 | Python | C# | 原因 |
|--------|--------|-----|------|
| 数据格式 | YAML + JSON 混合 | JSON + Fluent Builder | C# 无 YAML 依赖 |
| 页面 ID | `/settings/home` (路径式) | `home` (短 ID) | C# StateFixtureBuilder 用简短 ID |
| back_button | `trigger: "back_button"` (通用) | 独立 ID (`btn_back_w`, `btn_back_bt`) | C# 需唯一 ID 匹配元素 |
| Battery/Apps 页面 | 有 page 定义但无元素 | 同 Python (空页面) | 无交互元素但 DFS 仍需访问 |
| slider | Python 有 `brightness` 为 slider | C# mock 暂映射为 switch | StatefulMockVisionService 暂不区分 slider/switch |
| Home/Battery/Apps 子页面 | Python fixture 不含这些子页 | 同 Python | 二级菜单无 fixture 页 → DynamicMatch fallback |

---

### 1.1 场景 1: Settings 全量遍历 (safe_full_traversal)

| 属性 | 值 |
|------|-----|
| 基线版本 | Python V6.11.0 |
| Python 测试入口 | `test_settings_simulation.py::test_settings_simulation_run` |
| Completion | `expected_state: completed`, `expected_reason: natural` |
| CompletionPolicy | NONE (自然完成) |

#### TraversalPlan 配置

```csharp
var root = new TraversalNode("root", "Settings App", NodeType.Container,
    new Operation(OperationType.NoAction),
    new ChildrenStrategy(ChildrenStrategyType.DynamicMatch,
        DynamicRules: new Dictionary<string, DynamicRule>
        {
            ["menu_rule"] = new DynamicRule(
                RuleId: "menu_rule",
                MatchCondition: new MatchCondition(Type: "menu_item"),
                ChildTemplate: "menu_container",
                Action: "generate_child"),
            ["switch_rule"] = new DynamicRule(
                RuleId: "switch_rule",
                MatchCondition: new MatchCondition(Type: "switch"),
                ChildTemplate: "switch_leaf"),
        }),
    ExitCondition: new ExitCondition(
        Type: ExitConditionType.AllChildrenVisited,
        Fallback: FallbackAction.AutoEscape));

var plan = new TraversalPlan(
    EntryApp: "com.example.settings",
    EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
    PlanName: "Safe Full Traversal",
    PlanId: "settings-full-traversal-v1",
    RootNode: root,
    StaticNodes: new Dictionary<string, TraversalNode>());
```

#### 基线数值 (Python V6.11.0)

| 指标 | Python 值 | C# 实际值 |
|------|-----------|----------|
| 总步数 | **118** | **145** |
| 访问节点数 | **19** | **19** |
| ActionHistory 数 | — | **38** |
| 执行时间 | < 5s | < 0.01s |
| Trace nodes | ~600 | 145 |

#### visited_pages 基线明细 (C# NodeId 消歧后)

```
root:         root
level1:       dyn_menu_container_Wi-Fi_root,
              dyn_menu_container_Bluetooth_root,
              dyn_menu_container_Display_root,
              dyn_menu_container_Storage_root,
              dyn_menu_container_Battery_root,
              dyn_menu_container_Apps_root (6 个)
switch_leaves: dyn_switch_leaf_ON_dyn_menu_container_Wi-Fi_root,
               dyn_switch_leaf_ON_dyn_menu_container_Bluetooth_root, ← 碰撞修复后独立节点
               dyn_switch_leaf_Brightness level_dyn_menu_container_Display_root,
               dyn_switch_leaf_Dark mode_dyn_menu_container_Display_root (4 个)
level2:       dyn_menu_container_HomeNetwork_dyn_menu_container_Wi-Fi_root,
              dyn_menu_container_OfficeWiFi_dyn_menu_container_Wi-Fi_root,
              dyn_menu_container_GuestNetwork_dyn_menu_container_Wi-Fi_root,
              dyn_menu_container_Headphones Pro_dyn_menu_container_Bluetooth_root,
              dyn_menu_container_Speaker Mini_dyn_menu_container_Bluetooth_root,
              dyn_menu_container_Wallpaper_dyn_menu_container_Display_root,
              dyn_menu_container_Internal Storage_dyn_menu_container_Storage_root,
              dyn_menu_container_SD Card_dyn_menu_container_Storage_root (8 个)
```

1 + 6 + 4 + 8 = 19 节点 (含碰撞修复后的 Bluetooth 开关)

#### DFS 预期顺序

```
root → Wi-Fi → wifi_switch(ON) → HomeNetwork → OfficeWiFi → GuestNetwork
     → (back) → Bluetooth → bluetooth_switch(ON) → Headphones Pro → Speaker Mini
     → (back) → Display → brightness(slider) → Dark mode(switch) → Wallpaper
     → (back) → Storage → Internal Storage(只读) → SD Card(只读)
     → (back) → Battery(空页面) → Apps(空页面)
     → AllVisited ✓
```

---

### 1.2 场景 2: Settings 目标搜索 (TARGET_FOUND)

| 属性 | 值 |
|------|-----|
| 基线版本 | Python V6.11.1 |
| Python 测试入口 | `test_target_search.py::test_target_search_stops_at_dark_mode` |
| CompletionPolicy | TARGET_FOUND: Dark mode (EXACT, MARK_AND_STOP) |
| MatchMode | EXACT — 精确匹配文本 "Dark mode" |

#### TraversalPlan 配置

与场景 1 共享同一 root node 和 fixture，仅 CompletionPolicy 不同：

```csharp
var plan = new TraversalPlan(
    EntryApp: "com.example.settings",
    EntryPolicy: new EntryPolicy(EntryStrategy.BindCurrentScreen),
    PlanName: "Target Search - Dark Mode",
    PlanId: "settings-target-search-v1",
    RootNode: root,  // 同场景 1
    StaticNodes: new Dictionary<string, TraversalNode>(),
    CompletionPolicy: new CompletionPolicy(
        Type: CompletionPolicyType.TargetFound,
        TargetName: "Dark mode",
        MatchMode: MatchMode.Exact,
        ActionOnFound: TargetFoundAction.MarkAndStop));
```

#### 基线数值 (C# NodeId 消歧后)

| 指标 | Python 值 | C# 实际值 |
|------|-----------|----------|
| 总步数 | **49** | **92** |
| 访问节点数 | **9** | **14** |
| ActionHistory 数 | — | **26** |
| 执行时间 | < 2s | < 0.05s |

#### DFS 遍历路径与提前终止

```
设置主页 (root)                → visited
  Wi-Fi 子树完整               → visited (3 子页 + 开关)
  Bluetooth 子树完整           → visited (2 子页 + 开关)
  Display 子树 → 命中 Dark mode → MARK_AND_STOP
  Storage                       → ❌ 未访问 (提前终止)
  Battery                       → ❌ 未访问
  Apps                          → ❌ 未访问
```

#### visited_pages 顺序

```
1. 设置主页 (root)
2. Wi-Fi (menu_container-Wi-Fi-0-root)
3. Bluetooth (menu_container-Bluetooth-1-root)
4. Display (menu_container-Display-2-root)
5. Dark mode (switch_leaf-Dark mode-2-menu_container-Display-2-root) ← 目标命中
```

#### not_visited (证明提前终止有效)

```
Storage, Battery, Apps — 均排在 Display 之后，命中目标后不再访问
```

#### 行为特性

- 深度优先: Wi-Fi 子树完成 → Bluetooth 子树完成 → Display 子树中命中
- 提前终止: Storage/Battery/Apps 未被访问，证明 MARK_AND_STOP 生效
- 二级菜单无 fixture 页的项: HomeNetwork 等失败回退，不阻塞遍历

---

### 1.3 两个场景核心差异

| 对比项 | 全量遍历 | 目标搜索 |
|--------|---------|---------|
| 目的 | 验证 DFS 完整性 + NodeId 消歧 | 验证 TARGET_FOUND 提前终止策略 |
| CompletionPolicy | NONE (自然完成) | TARGET_FOUND + MARK_AND_STOP |
| Fixture | 共享 Settings App 7+2 页 | **同一 fixture** |
| Root Node | 共享 DynamicMatch root | **同一 root** |
| C# 步数 | 145 | 92 (少 37%) |
| C# 节点数 | 19 | 14 (少 26%) |
| C# ActionHistory | 38 | 26 |
| 验证重点 | 全节点覆盖 + Bluetooth 开关碰撞修复 | DFS 顺序 + 提前终止 + 未访问项证明 |

---

### 1.4 滚动场景基线 (Scroll-Enabled Baseline)

> **新增 (2026-07-12)**: ScrollableBaselineTests.cs — 6 个滚动场景，覆盖全部滚动行为。
> 使用 DynamicMatch 策略 + ScrollableMockVisionService + ScrollDataStore。

#### 1.4.0 滚动基线测试概览

**测试类**: `tests/UniClaw.Core.Tests/Baseline/ScrollableBaselineTests.cs`
**策略**: DynamicMatch (匹配按钮/开关/返回按钮) + ScrollableMockVisionService (累积模式 + 元素去重)
**Fixture 模式**: 最小 fixture (页面壳) + ScrollDataStore (分段元素数据)
**验证方式**: ExpectedBehavior.FromJson + WithFixtureDerivation + Verify

#### 1.4.1 场景 1: WiFi 列表全屏遍历 (AllScreens)

| 属性 | 值 |
|------|-----|
| Fixture | WiFiListFixture7Screens (单页 "wifi_list") |
| ScrollData | WiFiScrollData (6 分段, 24 唯一元素, 3 重叠) |
| Completion | `success: true`, `reason: all_visited` |
| 预期滚动次数 | ≥ 5 (6 分段 → 5 次滚动) |

**验证点**: 所有 24 个网络元素访问 (Network3/6/18 重叠去重) + 多次向下滚动 + finalProgress = 1.0

#### 1.4.2 场景 2: WiFi 列表向上滚动 (ScrollBackToTop)

| 属性 | 值 |
|------|-----|
| Fixture | 共享 WiFiListFixture7Screens |
| ScrollData | 共享 WiFiScrollData |
| Completion | `success: true`, `reason: all_visited` |
| scrollUpCount | ≥ 1 |

**验证点**: BackToSettings 元素被访问 + 向上滚动触发

#### 1.4.3 场景 3: WiFi 列表元素去重 (ElementDeduplication)

| 属性 | 值 |
|------|-----|
| Fixture | 共享 WiFiListFixture7Screens |
| ScrollData | 共享 WiFiScrollData |
| Completion | `success: true`, `reason: all_visited` |

**验证点**: Network3 (seg 0.0/0.2), Network6 (seg 0.2/0.4), Network18 (seg 0.8/1.0) 各只访问一次

#### 1.4.4 场景 4: WiFi 列表边界条件 (BoundaryConditions)

| 属性 | 值 |
|------|-----|
| Fixture | 共享 WiFiListFixture7Screens |
| ScrollData | 共享 WiFiScrollData |
| Completion | `success: true`, `reason: all_visited` |

**验证点**: 初始 progress = 0.0 + 最终 IsEndOfList = true

#### 1.4.5 场景 5: 稀疏列表跳跃恢复 (SparseJumpRecovery)

| 属性 | 值 |
|------|-----|
| Fixture | SparseFixture (单页 "sparse_list") |
| ScrollData | SparseJumpData (4 分段: 0.0, 0.4, 0.7, 1.0) |
| 元素数 | 8 (每段 2 个) |
| 跳跃检测 | gap 0.0→0.4 = 40% > 30% 默认步长 → jump 触发 |
| Completion | `success: true`, `reason: all_visited` |

**验证点**: jumpDetected ≥ 1 + jumpRecovered ≥ 1 + 全部 8 元素访问

#### 1.4.6 场景 6: 高重叠列表自适应步长 (AdaptiveStep)

| 属性 | 值 |
|------|-----|
| Fixture | OverlappingFixture (单页 "overlap_list") |
| ScrollData | OverlappingAdaptiveData (5 分段, 17 唯一元素, 70%+ 重叠) |
| 元素数 | 17 |
| Completion | `success: true`, `reason: all_visited` |

**验证点**: adaptiveStepIncreases ≥ 1 + 全部 17 元素访问

#### 1.4.7 滚动 vs 非滚动基线对照表

| 对比维度 | 非滚动基线 (SimulationBaselineTests) | 滚动基线 (ScrollableBaselineTests) |
|---------|--------------------------------------|-----------------------------------|
| **场景数** | 2 | 6 |
| **Fixture 策略** | 多页 (7+2 页) + Transition | 单页 + ScrollDataStore 分段 |
| **Child Strategy** | DynamicMatch | DynamicMatch (相同) |
| **Vision Provider** | StatefulMockVisionService | ScrollableMockVisionService |
| **Action Executor** | StatefulMockActionExecutor | ScrollableMockActionExecutor |
| **页面导航** | ✅ tap + back 多页跳转 | ❌ 单页停留 (滚动替代导航) |
| **元素发现** | 页面切换 → 新元素 | 累积模式 → 元素随进度出现 |
| **去重机制** | fixture 内元素唯一 ID | ScrollDataStore 内去重 + 跨分段去重 |
| **完成条件** | AllChildrenVisited + AutoEscape | AllChildrenVisited + AutoEscape (IsEndOfList 守卫) |
| **验证方式** | ExpectedBehavior-driven | ExpectedBehavior-driven (相同) |
| **JSON 预期文件** | `tests/.../Baseline/Fixtures/expected/*.json` | `tests/.../Baseline/Fixtures/expected/scroll/*.json` |
| **CI-blocking** | ✅ 是 | ✅ 是 |

#### 1.4.8 滚动场景 JSON 预期文件清单

| 文件 | 场景 | 路径 |
|------|------|------|
| `wifi-list-scroll-all-screens.json` | 场景1: 全屏遍历 | `tests/.../Baseline/Fixtures/expected/scroll/` |
| `wifi-list-scroll-back-to-top.json` | 场景2: 向上滚动 | `tests/.../Baseline/Fixtures/expected/scroll/` |
| `wifi-list-element-deduplication.json` | 场景3: 元素去重 | `tests/.../Baseline/Fixtures/expected/scroll/` |
| `wifi-list-boundary-conditions.json` | 场景4: 边界条件 | `tests/.../Baseline/Fixtures/expected/scroll/` |
| `sparse-list-jump-recovery.json` | 场景5: 跳跃恢复 | `tests/.../Baseline/Fixtures/expected/scroll/` |
| `overlapping-list-adaptive-step.json` | 场景6: 自适应步长 | `tests/.../Baseline/Fixtures/expected/scroll/` |

#### 1.4.9 ScrollableMockVisionService 关键增强

**FindElementAt 双搜索**: 先搜索 fixture 元素，再后备搜索 ScrollDataStore 可见元素（累积模式 + 去重）。确保 DynamicMatch 解析的坐标能在滚动数据中找到对应元素。

**GetVisibleElementsFromScrollData**: 从 ScrollDataStore 提取当前进度下的所有可见元素，按 `Threshold <= CurrentProgress` 累积，以元素 ID 去重。

Python `expected_behavior.yaml` 定义了 7 类验证维度。C# 通过 `ExpectedBehavior` sealed record class 实现了 5 类可验证维度 + 1 informational 参考锚点 (D-E4), 2 类标记 TODO。

### ExpectedBehavior record 定义 (D-E1, C-11 schema 锁定)

```
ExpectedBehavior (顶层, sealed record class)
  ├── Scenario        — string
  ├── Description     — string
  ├── Completion      — CompletionExpectation (Success, Reason, FinalState?)
  ├── PageCoverage    — PageCoverageExpectation (Required, Forbidden)
  ├── ElementCoverage — ElementCoverageExpectation (Required, RequiredRatio=0.95)
  ├── CollisionProof  — ImmutableArray<CollisionProof> (Text, ExpectedDistinct, ParentPages?)
  ├── DfsProperties   — DfsPropertiesExpectation (RootFirst, ParentBeforeChild, BackAfterForward)
  └── NumericAnchor   — NumericAnchor (TotalSteps, VisitedPagesCount, ActionHistoryCount, ElapsedSecondsMax)
```

### Python 7类 → C# ExpectedBehavior 子 record 映射

| Python 维度 | C# ExpectedBehavior 子 record | 状态 | 对照数据源 |
|------------|-------------------------------|------|-----------|
| 1. completion | `CompletionExpectation` | ✅ 已实现 | TraversalResult.Success + CompletionReason + FinalState |
| 2. page_rules | `PageCoverageExpectation` (Required + Forbidden) | ✅ 已实现 | TraversalResult.VisitedPages |
| 3. node_coverage | `ElementCoverageExpectation` (Required + RequiredRatio) | ✅ 已实现 | TraversalResult.ActionHistory |
| 4. collision_proof | `CollisionProof` (Text + ExpectedDistinct + ParentPages?) | ✅ 已实现 | TraversalResult.VisitedPages (按 Text 分组) |
| 5. dfs_properties | `DfsPropertiesExpectation` (RootFirst + ParentBeforeChild + BackAfterForward) | ✅ 已实现 | TraversalResult.VisitedPages + ActionHistory |
| 6. numeric_anchor | `NumericAnchor` (TotalSteps + VisitedPagesCount + ActionHistoryCount + ElapsedSecondsMax) | ✅ 已实现 (informational) | TraversalResult 数值 (±5% tolerance) |
| 3. operation_rules | — | ⏳ TODO (D-E4) | 依赖 Trace 补齐: restore_ops, skip_dangerous |
| 7. trace_integrity | — | ⏳ TODO (D-E4) | 依赖 Trace 补齐: span_types, page_transitions |

### VerificationReport 结构 (D-E2)

```
VerificationReport (sealed record class)
  ├── AllPassed  — bool (排除 numeric_anchor, 只看 5 类 blocking 规则)
  ├── Summary    — string (逐条 PASS/FAIL/INFO)
  └── Details    — ImmutableArray<RuleResult> (RuleId, Passed, Message, Actual?)
```

### auto_derive sentinel 推导 (D-E3)

JSON 预期定义中 `"auto_derive"` sentinel 从 StateFixture 推导填充:

| 字段 | auto_derive 推导逻辑 |
|------|----------------------|
| `pageCoverage.required` | fixture 页面名 (PageName, 排除 initialPage 的 PageName) |
| `elementCoverage.required` | fixture 中所有非-readonly/back_button 元素 Id |
| `collisionProof` | fixture 中同 Text 不同 PageId 的元素组合 (e.g. "ON" 在 wifi+bluetooth → CollisionProof(Text="ON", ExpectedDistinct=2)) |

### JSON 预期定义文件清单

| 文件 | 场景 | 路径 |
|------|------|------|
| `settings-full-traversal.json` | 场景1: 全量遍历 | `tests/.../Baseline/Fixtures/expected/` |
| `settings-target-search.json` | 场景2: 目标搜索 | `tests/.../Baseline/Fixtures/expected/` |

### 维度 1: completion → CompletionExpectation

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| success 匹配 | `Expected.Success == TraversalResult.Success` | 全量遍历 + 目标搜索 |
| reason 匹配 | `Expected.Reason == TraversalResult.CompletionReason` | 全量遍历 (all_visited) + 目标搜索 (target_found) |
| final_state 匹配 | `Expected.FinalState == TraversalResult.FinalState?.ToString()` | 可选 |

### 维度 2: page_rules → PageCoverageExpectation

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| required pages visited | Required 页面名在 VisitedPages 中 (Contains 语义) | 全量遍历 (auto_derive) + 目标搜索 (手写) |
| forbidden pages not visited | Forbidden 页面名不在 VisitedPages 中 | 目标搜索 (Storage, Internal Storage, SD Card) |

### 维度 3: node_coverage → ElementCoverageExpectation

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| element coverage ratio | Required 元素在 ActionHistory 覆盖率 ≥ RequiredRatio | 全量遍历 (≥95%) + 目标搜索 (≥60%) |

### 维度 4: collision_proof → CollisionProof

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| NodeId 碰撞解决 | 同 Text 在 VisitedPages 中 distinct count ≥ ExpectedDistinct | 全量遍历 (ON=2, 碰撞修复证明) |

### 维度 5: dfs_properties → DfsPropertiesExpectation

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| root_first | VisitedPages[0] 包含 "root" | 全量遍历 + 目标搜索 |
| parent_before_child | root/home 在子页面之前 | 全量遍历 + 目标搜索 |
| back_after_forward | ActionHistory 中有 tap + back 交替模式 | 全量遍历 + 目标搜索 |

### 维度 6: numeric_anchor → NumericAnchor (informational)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| total_steps ±5% | TotalSteps 在 anchor ±5% 范围内 | 全量遍历 + 目标搜索 (INFO, 不 CI-blocking) |
| visited_pages ±5% | VisitedPages.Length 在 anchor ±5% 范围内 | INFO |
| action_history ±5% | ActionHistory.Length 在 anchor ±5% 范围内 | INFO |
| elapsed_seconds ≤ | ElapsedSeconds ≤ ElapsedSecondsMax | INFO |

### 维度 TODO: operation_rules (待 Trace 补齐)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| depth_first_order | 操作顺序符合 DFS | 全量遍历 |
| restore_operations_count | switch/slider 后执行恢复, `count ≥ 2` | 全量遍历 |
| skip_dangerous_buttons | 恢复出厂设置/清除数据被跳过 | 全量遍历 |
| no_duplicate_actions | 同节点连续重复 ≤ 2 | 全量遍历 |

### 维度 TODO: trace_integrity (待 Trace 补齐)

| 规则 | 验证条件 | 适用场景 |
|------|---------|---------|
| span_types_present | session_end, step_end, state_transition, execution, ai_call, page_transition 全存在 | 全量遍历 |
| page_transitions_recorded | page_transition ≥ 10 | 全量遍历 |

---

## 2.5. 滚动模拟支持 (Scroll Simulation)

> **Phase 1 新增 (2026-07-12)**: 滚动模拟基础设施完成，支持可滚动列表场景测试。

### 核心概念

#### 累积模式 (Accumulation Mode)

所有 `Threshold <= CurrentProgress` 的分段元素均可见。随着滚动进度增加，更多分段变为可见，元素累积显示。

**语义**: "向下滚动时，更多内容出现" — 符合用户直觉的滚动行为。

#### 元素去重 (Element Deduplication)

当相同元素 ID 出现在多个分段时，只返回最低阈值分段的实例。

**原因**: 
- 防止同一元素被多次访问
- 保持元素身份一致性 (同 ID = 同元素)
- 支持可靠的"已访问子节点"跟踪

**实现**: 按阈值分组，取每组的最小阈值实例。

#### 跳跃检测与恢复 (Jump Detection & Recovery)

**跳跃定义**: 滚动前后元素集合无重叠且两者都非空 (`OverlapStatus.NoOverlap_BothHaveElements`)

**恢复策略**:
1. 检测: 比较滚动前后元素 ID 集合
2. 回滚: 恢复滚动前进度
3. 重试: 使用减小的步长 (`step * JumpRecoveryFactor`)
4. 重复: 直到检测到重叠或超过最大重试次数

**安全状态**: 
- `NoOverlap_BeforeEmpty`: 初始状态，安全
- `NoOverlap_AfterEmpty`: 可能到达末尾，非跳跃

#### 自适应步长 (Adaptive Step Calculation)

当重复元素比例超过阈值 (默认 70%) 且新元素数达到最小样本量 (默认 3) 时，增加步长 (默认 ×1.5)。

**目的**: 减少冗余滚动，提高效率。

**限制**: 步长始终限制在 `[MinScrollStep, MaxScrollStep]` 范围内。

### 数据模型

| 类型 | 用途 |
|------|------|
| `ScrollSegment` | 阈值 + 元素集合关联 |
| `ScrollState` | 进度 + 滚动次数 + 历史记录 |
| `ScrollAction` | 单次滚动操作记录 |
| `ScrollDataStore` | 页面 ID → 分段集合映射 |
| `OverlapStatus` | 元素重叠状态分类 (5 种状态) |
| `ScrollVerifyResult` | 滚动验证结果 |
| `JumpRecoveryResult` | 跳跃恢复结果 |
| `ScrollHandlerConfig` | 滚动参数配置 |

### ScrollHandler 7 步流程

```
1. Detect (ScrollabilityDetector)     → 检测滚动能力 (NotScrollable/CanScrollDown/AtBottom/CanScrollUp)
2. Classify (ScrollClassifier)        → 计算进度、最大阈值、推荐步长
3. Decide (ScrollDecider)             → 映射到动作类型 (None/ScrollDown/ScrollUp)
4. Execute (ScrollActionExecutor)     → 通过 Hook Dispatch Table 执行滚动
5. Verify (JumpDetector)              → 检测跳跃 (元素集合比较)
6. Recover (JumpRecoveryHandler)      → 回滚并重试 (如需要)
7. Statistics (ScrollStatisticsCollector) → 收集统计指标
```

### 配置参数 (ScrollHandlerConfig)

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `DefaultScrollStep` | 0.3 | 默认滚动步长 (30%) |
| `MinScrollStep` | 0.01 | 最小滚动步长 (1%) |
| `MaxScrollStep` | 0.5 | 最大滚动步长 (50%) |
| `MaxJumpRetryCount` | 3 | 跳跃恢复最大重试次数 |
| `JumpRecoveryFactor` | 0.5 | 跳跃恢复步长缩减因子 |
| `ProgressEpsilon` | 0.001 | 进度边界比较容差 |
| `EnableAdaptiveStep` | true | 是否启用自适应步长 |
| `AdaptiveStepIncreaseThreshold` | 0.7 | 自适应增加阈值 (70% 重复比例) |
| `AdaptiveStepIncreaseFactor` | 1.5 | 自适应增加因子 |
| `MinSampleSize` | 3 | 自适应增加最小样本量 |

### Mock 服务

| 服务 | 用途 |
|------|------|
| `ScrollableMockVisionService` | 支持滚动的 Mock Vision Provider (累积模式 + 去重) |
| `ScrollableMockActionExecutor` | 支持滚动的 Mock Action Executor (ScrollDown/ScrollUp) |

### 测试场景覆盖 (19 个场景)

| 类别 | 场景数 | 场景列表 |
|------|--------|---------|
| 基本场景 | 4 | 单屏、双屏、多屏、空列表 |
| 边界场景 | 4 | 顶部边界、底部边界、接近边界 (epsilon)、精确末尾 |
| 元素场景 | 3 | 去重、重复、动态变化 |
| 步长场景 | 4 | 小步长、默认步长、大步长、自适应步长 |
| 跳跃场景 | 4 | 正常滚动、跳跃检测、跳跃恢复、跳跃恢复失败 |

### 使用示例

```csharp
// 创建滚动数据
var scrollData = ScrollDataStore.CreateBuilder()
    .Add("wifi_list",
        new ScrollSegment(0.0, CreateMenuItems("Network", 1, 3)),
        new ScrollSegment(0.5, CreateMenuItems("Network", 4, 6)),
        new ScrollSegment(1.0, CreateMenuItems("Network", 7, 9)))
    .Build();

// 创建滚动支持的服务
var vision = new ScrollableMockVisionService(fixture, scrollData);
var executor = new ScrollableMockActionExecutor(vision);

// 执行滚动
executor.ScrollDown(0.5);

// 验证结果
var progress = vision.GetScrollProgress("wifi_list"); // 0.5
var isAtBottom = vision.IsEndOfList; // false
```

### 向后兼容性

- `ScrollableMockVisionService` 是独立类，不替换 `StatefulMockVisionService`
- 现有非滚动测试无需修改
- 滚动功能通过 `HasScrollData(pageId)` 判断启用

---

## 3. C# 当前状态与缺口

### 已有 — SimulationE2ETests.cs (7 个开发验证场景)

| # | 场景 | 验证内容 | 是否基线级 |
|---|------|---------|-----------|
| 1 | 空节点树立即完成 | AllVisited, steps≤5 | ❌ 开发验证 |
| 2 | 2 页遍历 | AllVisited, ActionHistory 非空 | ❌ 开发验证 |
| 3 | MaxSteps 超限 | MaxSteps, TotalSteps=1 | ❌ 开发验证 |
| 4 | VisitedPages 按序 | root 首个被访问 | ❌ 开发验证 |
| 5 | 4 页 Settings App 全路径 | AllVisited, tap+back 混合 | ❌ 简化版 (4 页 vs 7 页) |
| 6 | Settings App WiFi 路径 | 4 步 (2 tap + 2 back) | ❌ 简化版 |
| 7 | 空区域 tap | ResultVerify, success=false | ❌ 开发验证 |

### 已有 — SimulationBaselineTests.cs (2 个基线场景, ✅ ExpectedBehavior-driven)

| # | 场景 | 验证内容 | 是否基线级 |
|---|------|---------|-----------|
| 1 | 7 页全量遍历 | ExpectedBehavior.FromJson + WithFixtureDerivation + Verify → Assert.True(report.AllPassed) | ✅ 基线 (Phase D: ExpectedBehavior 契约驱动验证) |
| 2 | 7 页目标搜索 Dark mode | ExpectedBehavior.FromJson + WithFixtureDerivation + Verify → Assert.True(report.AllPassed) | ✅ 基线 (Phase D: ExpectedBehavior 契约驱动验证) |

7 页 fixture 通过 `StateFixtureBuilder` 内联构建 (设计决策 D-B1: 内联优先, 不建独立 JSON 文件)。
Phase C: 升级范围断言为精确数值 (待 C# 运行时基线确认)。

### 缺口 (待建)

| 缺口 | 说明 | 依赖 |
|------|------|------|
| **operation_rules 验证** | 5 类已实现 (completion, page_coverage, element_coverage, collision_proof, dfs_properties) + numeric_anchor; 2 类 TODO: operation_rules, trace_integrity | Trace 补齐 (SpanType, PageTransition) |

### 建设进度

```
✅ Phase 2.3b 完成 (HandlePreconditionCheck + HandleResultVerify)
✅ 7 页 fixture 内联构建 (StateFixtureBuilder, D-B1: 内联优先)
✅ SimulationBaselineTests.cs (2 核心场景, Phase B 范围断言)
✅ C-11 加入 constitution/constraints.md
✅ 2 基线测试全绿 (523 total suite, 2 baseline)
✅ Phase D: ExpectedBehavior 契约驱动验证 (9 record types + FromJson + WithFixtureDerivation + Verify)
✅ 2 基线 JSON 预期定义文件 (settings-full-traversal.json + settings-target-search.json)
✅ 523 total suite tests all green (Phase D: ExpectedBehavior-driven)
✅ Phase E: 基线测试报告系统 (BaselineReportCollector + BaselineReportWriter, JSON + Markdown 输出)

待做:
  1. operation_rules 验证维度 (待 Trace 补齐: restore_ops, skip_dangerous)
  2. trace_integrity 验证维度 (待 Trace 补齐: span_types, page_transitions)
  3. numeric_anchor 数值随引擎演进更新 (±5% tolerance, 不 CI-blocking)
```

C# 基线数值**不会**与 Python 完全一致 (引擎行为差异、DFS 顺序差异、元素映射差异)。第一步用 Python 数值作为**参考锚点**，待 C# 测试实际运行后更新为 C# 实际基线值。

---

## 4. 基线数值更新规则

基线数值是 Tier 3 数据，不是 Tier 1 不可变约束。数值随代码演进自然变化。

| 变更类型 | 是否需要更新 | 更新方式 |
|---------|------------|---------|
| 加新页面到 fixture | ✅ steps/nodes 增加 | 更新本文件 §1 基线数值 |
| 修复 DFS 顺序 bug | ✅ steps 可能变化 | 更新本文件 §1 visited_pages 顺序 |
| CompletionPolicy 变更 | ✅ 完成条件变化 | 更新本文件 §1 Completion 字段 |
| 引擎内部优化 (不影响行为) | ❌ 数值不变 | 无需更新 |
| 新增规则验证维度 | ✅ 加新规则到 §2 | 更新本文件 §2 |

更新时同步更新 `SimulationBaselineTests.cs` 中的 Assert 断言值，确保文档数值 = 代码断言 = 实际运行结果 三路一致。

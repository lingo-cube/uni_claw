# KNOWN_LIMITATIONS — settings-full-tree-enumeration-integration

> 环境固化 / 待优化项登记（2026-08，Phase 1-4 已证明后审计）。
> 这些项不影响当前已证明结论（机制层已验证），但换环境（Android 版本 /
> locale / 厂商 ROM / 设备形态）时需要维护。每一项都标了位置、固化内容、
> 换环境影响与候选优化方向。**未修复，仅登记为待优化。**
>
> 分类：
> - `PROD` — 生产代码中的环境结构知识（随产品部署，换环境需产品适配）
> - `TEST` — 测试侧注入的语义/断言（fixture 性质，换环境更新测试资产）

---

## P-1 (PROD) — page-title-role admission: collapsing_toolbar

- **位置**: `src/UniClaw.Runtime.Adapters/Device/AdbUiHierarchySource.cs`
  (`IsPageTitleRoleEvidence`, 代码内已标 `KNOWN-ENVIRONMENT-DEPENDENT`)
- **固化内容**: 该 Android 版本 Settings 的页面标题暴露在
  `com.android.settings:id/collapsing_toolbar` 的 content-desc 上
  （Material collapsing toolbar；非交互节点）。
- **为什么需要**: Phase 3 的 DESTINATION_ALIAS 压力——子页面身份必须来自
  fresh 结构化证据，而标题角色被既有"仅收交互节点"的 admission 过滤。
- **换环境影响**: 旧版 Settings 用 `android:id/action_bar_title`；
  其他版本/ROM 可能不同 → 子页面无法解析 → fail-closed 或需重新适配。
- **候选优化**: 版本感知的 toolbar-title-role 匹配；或把 page-title-role
  契约配置化、交给语义层（而不是 admission 层）持有。

## P-2 (PROD) — parent-return action-role label: "Navigate up"

- **位置**: `src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs`
  (`ParentReturnActionRoleLabel`, 代码内已标 `KNOWN-ENVIRONMENT-DEPENDENT`)
- **固化内容**: 返回按钮的 accessibility action-label 精确匹配
  `content-desc == "Navigate up"`（ImageButton、无 TitleText）。
- **为什么需要**: Phase 2 的 H_EXISTING_MECHANISM_DEFECT——真实返回按钮
  无法被"TitleText == 父页面名"机制匹配，阻塞子页面 completeness。
- **换环境影响**: locale / 厂商 ROM 可能本地化或省略该 label → 返回控件
  无法解析 → fail-closed。
- **候选优化**: locale 无关的平台 action-role 契约（语义层提供）；
  可配置 action-label 列表（带证据契约）；绝不 keyword 扩展。

## T-1 (TEST) — 测试侧语义注入（resolver/authorizer/evaluator）

- **位置**: `tests/UniClaw.Runtime.Tests/Scenario/`（
  `SettingsSingleRecursiveChildTests.ResolveSemanticPage`、
  `AuthorizePhase2/3/4`、各 fake worlds）
- **固化内容**: 页面身份规则（`search_action_bar` 根标识、
  `SettingsSubpage(<title>)`）、授权目标 label（Location / Battery /
  Location services）、ExploreWhileNew / Inventory evaluator。
- **为什么需要**: 架构设计——Runtime 接受调用方注入的语义判定
  （scenario-first；与 COMPOSE-05 capstone 同模式）。
- **换环境影响**: Settings 菜单内容/rid 变化 → 目标选择与断言失效。
- **候选优化**: 语义层产品化（独立生产 resolver/authorizer，由
  vision/结构规则驱动），测试侧退化为纯验证。

## T-2 (TEST) — source 计数断言（16/3/2）

- **位置**: 各真实测试的 evidence/断言
- **固化内容**: Root=16、Location=3、Battery=3、Location services=2。
- **换环境影响**: 不同 Android 版本 Settings 菜单不同 → 计数变化。
- **候选优化**: 断言改为"≥ 关键 source 存在 + epoch 冻结"，不绑死总数。

## T-3 (TEST) — 目标选择依赖审计（"选择有利目标"）

- **位置**: Phase 3/4 的真实测试
- **固化内容**: grandchild/sibling 选定了审计后最干净的目标
  （Location services、Battery）；"See all → Recent access"
  （More-options Unknown）与 "App location permissions"（缺 title-role）
  未跑通。
- **换环境影响**: 换设备后这些已知压力的形态可能变化。
- **候选优化**: Phase 5 capstone 决定是否正面处理 More-options Unknown /
  缺 title-role 页面。

## T-4 (PROD, 既有) — InteractionAffordanceAnalyzer 的 LinearLayout 行规则

- **位置**: `src/UniClaw.Runtime/World/InteractionAffordanceAnalyzer.cs`
- **固化内容**: `class == android.widget.LinearLayout` + title/summary →
  NavigationCandidate（Settings Preference 行结构；本会话前已存在）。
- **换环境影响**: 主题/版本/ROM 的行容器结构变化 → 分类变化。
- **候选优化**: 结构规则配置化；语义层产品化时一并处理。

---

## 关联

- 本登记对应会话审计结论：Runtime 机制层无设备固化；环境结构知识集中在
  PROD 两处 + 既有 analyzer 规则；其余为 TEST 侧 fixture/断言。
- 所有 `KNOWN-ENVIRONMENT-DEPENDENT` 代码标记均指向本文件。

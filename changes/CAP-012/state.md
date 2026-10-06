# CAP-012 — Language Inspection 能力组件（capability-component skill 首次全新实战）

lifecycle_state: implement · disposition: none · depth: standard · base: 待回填

## Intent（WHAT/WHY）

所有者指令（2026-10-06）："使用这个（capability-component skill）做一个语言检查的
capability——比对文本是否符合语言格式。"

skill 首次全新能力实战（CAP-006~009 为存量改造）。按七步纪律全程执行，本 change
同时是 skill 的实地检验。

## 买方五问（skill 第 1 步）

| 问 | 答 |
|---|---|
| 谁消费结果 | Runtime Integration 诊断面（post-commit 检查；§8.1 先例：ObservationAccepted → Finding）；买方证据=所有者指令 |
| 输入 | `LanguageInspectorRequest`（同 capture 有界文本投影 + expectedLanguage；既有 Host-neutral 契约，IntegrationContracts.cs） |
| 输出 | `Finding`（disposition Pass/Violation/Unknown + coverage/diagnostic；既有契约） |
| 是否动主权威 | 否（Finding 默认非权威；进 Assurance 须另立 Promotion——§8.1 明文冻结） |
| 失败策略 | 缺输入/不完整 → Unknown；不支持的 expectedLanguage → Unknown（诚实不猜）；无文本可检 → Unknown |

## Scope

- Kernel L1：`ILanguageInspector : ICapability`（操作面 `Inspect(request) → Finding`，
  同步确定性）+ `LanguageInspectionProtocol` 常量（id `runtime.language-inspector`，
  协议 Language Inspection@1.0——沿用 Simulation fixture 同名协议）。
- Kernel L2：`LanguageFormatInspector`（确定性 Unicode 脚本规则：expectedLanguage
  归一化前缀 → en=Latin / zh=Han；字母字符须属期望脚本，数字/标点/空白中立；
  违例 Finding 带发生项定位；ctor 可注入声明 + canonical 缺省 + WithDescription
  同表视图先例）。R7 健康豁免（纯计算），RL1 常驻无资源。
- Registry 执法（ModelRouting 先例同构）：`CapabilityCategory.LanguageInspection`
  新类别；scope=RuntimeIntegration、恰一协议、角色镜像、依赖-关系镜像（与
  ModelRouting 公共形状提取共享 helper）；**拒绝 description-only**；双向接口
  一致性（声明该类别必须 ILanguageInspector 实例；反之亦然）。
- Host 组合根：`RuntimeIntegrationCapabilityComposition`（ADR-0035 三域独立——
  Runtime Integration 域独立 Registry + 实例注册）；Program.cs 建立第二注册表，
  capability-facts 落盘合并双域事实。
- 白名单 +3 型；文档同步（hub README 实现现状 / Kernel Capability 代码归属 /
  skill worked example 增 CAP-012）。

## Out of scope

- 不支持语言的扩表（fr/ja/… 等真实买方出现再加——Unknown 诚实）。
- 不接 Assurance/Effect（Finding 非权威边界）。
- 不做异步/批量调度（post-commit 订阅属 Hub push 留白）。

## Acceptance

| ID | 行为 | 证据 |
|---|---|---|
| A1 | L2 规则契约 | en/zh 各 Pass/Violation；混脚本报违例带 occurrence 定位；缺输入/不支持语言/无文本 → Unknown；Finding 形状（sourceOwner/disposition/status） |
| A2 | Registry 执法 | 实例注册往返（RuntimeIntegration 域）；description-only/错 scope/错协议/非 ILanguageInspector 实例/接口-类别错配均 fail-closed |
| A3 | 组合根 | 独立域注册表 + Resolve 取回；Program.cs facts 合并双域 |
| A4 | R5 可替换性 | 同一 ILanguageInspector 消费闭包喂 L2 与 stub 双实现，契约互换 |
| A5 | 兼容 | 全量测试绿；白名单 +3；场景再认证 |

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · §8.1 协议冻结与 Simulation fixture 先例取证完毕；编号避让并行会话（CAP-010/011 已被其占用）；七步启动。

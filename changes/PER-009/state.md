# PER-009 — 多源观察与信任：XML producer + 冲突裁决 + 信任等级（竖切）

lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: d45bdda

## Intent

第二个观察源（uiautomator XML）接入 P2，使跨源互证第一次真实发生；
配套统一冲突裁决（免费层）、(源×类别) 信任等级表、共享 subject 常量类、
事后验证路由。Tier 2（深模型）独立后置，由本 change 产出的冲突触发率
数据作为其立项买家。

## Scope

- `SharedSubjects` 常量类（ui.screen / *.state / screen.frame）+ 存量
  5 处字面量迁移（Host 四 feed + HostRunner obligation）
- UiAutomatorDump producer：dump → XML 解析 → claims（含每节点
  class claim 与身份字段）；失败语义见 D8；空树 = OK_EMPTY；
  **checked 三态解析（getChecked()，API 34+；目标设备 api35），
  值域 on/off/partial，不布尔压扁**
- `ControlBeliefView`（ConflictedSubjects 透出）+ `ReobserveFocused`
  策略规则 + 裁剪重扫（Tier 1，复用 /v1/analyze_raw）
- ConflictResolver（判据见 D3/D12/D13）：权威域直接销案 + 剥夺权威
  降级路径；统一销案记录（tier / overruled 标注），冲突转已解决留档
- producer-trust.json：(源×类别)→A/B/C + CSS 级联覆盖（包名/系统特例
  > 类别 > 源默认）；A/B/C 采信门槛生效（B 级孤证不可逆前补佐证）
- 事后验证路由：标准控件 + XML 可用 → XML 验证（基线 §24.3 native
  signal 合法性在案）；非标准 → 截图视觉验证
- Android 无障碍规范正式调研（evidence/ 落档，喂权威字段表与覆盖表初值）

## Out of Scope

- Tier 2 / `/v1/analyze_deep` / LLM·VLM 接入（触发公式 D6 已冻结，
  立项买家 = 本 change 的冲突触发率数据）
- 元素级 subject 共享（假合并，ADR-0027 否决）
- coordinator（无买家）、ObservationNeed 协议边（等 F9）
- platform.settings 转正（候选，另议）；iOS

## Decisions

- D1 多源并行常开全页面：视觉 + XML 永远并行，缺席即数据
  （degraded:no-xml 入 provenance）。[ADR-0027]
- D2 XML 走 P2 当普通 producer，一行裁决特判不加（对质是现有管道
  默认行为，"无条件信 XML"才需要写代码）。
- D3 **[2026-09-22 修订·官方文档校准]** Tier 0 判据 =
  `语义字段权威 ∧ 节点身份唯一解析 ∧ dump 新鲜 ∧ 属性有效`：
  - 语义字段权威集 = checked·checkable / enabled / selected / focused /
    text（AccessibilityNodeInfo 原生序列化，非视觉推断）；
  - 类名（android.* / com.*）只是强佐证，不是判据——自定义控件正确
    暴露 accessibility 状态同样享有权威，反之亦然；
  - IdentityMatched 操作定义：请求 subject 在树中**唯一**解析到节点
    （resource-id/descriptor 匹配唯一命中）；零命中/多命中 = 剥夺权威。
- D4 统一裁决管道：全部冲突走同一 resolver，免费层优先。
- D5 信任等级表 (源×类别)→A/B/C + CSS 级联覆盖维度；落点
  src/UniClaw.Kernel/World/producer-trust.json；修订双通道（人审
  change + 自动调优留痕可回滚）。[ADR-0028]
  级联覆盖同时是对抗域防线：已知撒谎 app 以包名级覆盖降级其 XML 状态。
- D6 **[修订]** 升档触发 = **当前权威证据体系无法定案**（非 confidence
  低）：权威域外 / XML 权威被剥夺 / 视觉 Insufficient·Ambiguous →
  Focused（免费）；Deep 门槛 = 仍悬案 ∧ 涉案 ≥B 级 ∧（不可逆 ∨ 跨 ≥2
  观察周期）；C 级线索不触发；不感知模型类型。
- D7 共享 subject 层 = 三件套，常量类承载 + 存量迁移；半页语义纸
  （值格式 + 元素级不共享理由）；值格式断言进本 change 验收。
  **值域修订：*.state ∈ {on, off, partial}（三态，getChecked()）。**
- D8 dump 失败语义：服务未启用 → 60s 降级窗口 × 每 Run ≤3 次探测；
  瞬时失败不占次数、下周期自然重试；预算 = 视觉耗时 + 500ms；
  空树 = OK_EMPTY。
- D9 事后验证按类别路由（标准控件 XML / 非标准截图）。
- D10 单竖切 change（本 change）；Tier 2 独立后置。
- D11 不做清单见 Out of Scope（docket 有档）。
- D12 **[新增·字段分类表]** 权威状态字段（checked/enabled/selected/
  focused）+ text=结构化文本优先；clickable/scrollable/focusable=
  能力属性，**不得当作视觉状态**；bounds=定位/裁剪依据，非像素内容
  真相；resource-id/class/package=身份类别证据；颜色/图标语义/
  canvas/图片内容/动画阶段=XML 非权威域，视觉域规则裁决。
- D13 **[新增·双类冲突]** 权威域内冲突：XML 直接销案，**confidence
  全盲**（0.55 与 0.999 同等待遇），销案记录 overruled=vision，
  **不升档**；权威域外冲突：XML 不参与终局裁决 → 视觉域 →
  必要时 Focused → 再必要时 Deep。升档与 confidence 完全脱钩。
- D14 **[新增·新鲜度门]** Tier 0 要求 dump 与截图同观察周期且
  Δ(CaptureTime) ≤ 视觉耗时 + 余量（复用 D8 预算）；过期 dump 不赋
  权威，降级为背景佐证——两份证据可能描述两个时刻，不是谁看错。

## Acceptance

1. SharedSubjects 常量类落地，存量 5 处迁移，全量测试绿
2. 真机设置页（api35）：XML claims 与视觉 claims 同帧入 P2（producer
   区分在案）；checked 三态解析，partial 不压扁
3. **跨源碰头第一次真实发生**：构造同 key 分歧，Conflict 记录集成级在案
4. ReobserveFocused 端到端：冲突 → 定向裁剪重扫 → 销案或升级留档
5. **权威域冲突直接销案且不升档**：构造 视觉 ON(高 conf) vs XML
   checked=false → 判 OFF，tier=category-authority，overruled=vision，
   零次升档调用（回归 D13）
6. **权威剥夺路径**：身份多命中 / dump 过期 / 属性缺失 → 不赋 XML
   权威，降视觉域（回归 D3/D14）
7. 信任门槛生效：B 级孤证 + 不可运动作 → 未补佐证前拒绝授权
8. 事后验证路由生效：标准控件目标验证不产生截图调用
9. 值格式断言：XML 产 *.state 值域 {on,off,partial} 与常量类一致
10. 全程零大模型调用（Tier 2 不在本 change）
11. Android 规范调研落 evidence/，权威字段表与覆盖表每格有出处
12. 能力属性误用守卫：clickable 等不进入状态 claim 裁决路径

## Verification

（IMPLEMENT 后按四元组回填；验收 3/5/6 为本 change 核心证据点）

## Status log

- 2026-09-22 · created·persisted · 立项 grill 三轮落定（docket 见
  evidence/2026-09-22-per009-multisource-grill-docket.md；台账 #16）。
- 2026-09-22 · persisted·amended · 用户携官方文档修订 Tier 0 判据
  （D3 改语义字段权威制、新增 D12/D13/D14、D6/D7 值域升级）；
  台账 #17。原"类名前缀"判据作废存档。

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
  class claim）；失败语义见 D8；空树 = OK_EMPTY
- `ControlBeliefView`（ConflictedSubjects 透出）+ `ReobserveFocused`
  策略规则 + 裁剪重扫（Tier 1，复用 /v1/analyze_raw）
- ConflictResolver：Tier 0 标准控件 XML 定案 / Tier 0' 像素域视觉定案
  / Tier 1；统一销案记录（tier 标注），冲突转已解决留档
- producer-trust.json：(源×类别)→A/B/C + CSS 级联覆盖（包名/系统特例
  > 类别 > 源默认）；A/B/C 采信门槛生效（B 级孤证不可逆前补佐证）
- 事后验证路由：标准控件 + XML 可用 → XML 验证（基线 §24.3 native
  signal 合法性在案）；非标准 → 截图视觉验证
- Android 无障碍规范正式调研（evidence/ 落档，喂覆盖表初值）

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
- D3 标准控件 XML 终审（用户裁决："升到啥模型也是它准"）；类别判据
  = XML class 前缀（android.widget/view.* vs com.*）。
- D4 统一裁决管道（用户形态）：全部冲突走同一 resolver 查层级表，
  免费层优先。
- D5 信任等级表 (源×类别)→A/B/C + CSS 级联覆盖维度；落点
  src/UniClaw.Kernel/World/producer-trust.json；修订双通道（人审
  change + 自动调优留痕可回滚）。[ADR-0028]
- D6 Tier 2 触发公式：免费层未销案 ∧ 涉案 claim ≥B 级 ∧（涉不可逆
  动作 或 同一冲突跨 ≥2 观察周期）；C 级线索不触发；不感知模型类型。
- D7 共享 subject 层 = 三件套，常量类承载 + 存量迁移；半页语义纸
  （值格式 + 元素级不共享理由）；值格式断言进本 change 验收。
- D8 dump 失败语义：服务未启用 → 60s 降级窗口 × 每 Run ≤3 次探测，
  超限本 Run 标不可用（下 Run 重置）；瞬时失败不占次数、下周期自然
  重试；预算 = 视觉耗时 + 500ms；空树 = OK_EMPTY。
- D9 事后验证按类别路由（标准控件 XML / 非标准截图）。
- D10 单竖切 change（本 change）；Tier 2 独立后置。
- D11 不做清单见 Out of Scope（docket 有档）。

## Acceptance

1. SharedSubjects 常量类落地，存量 5 处迁移，全量测试绿
2. 真机设置页：XML claims 与视觉 claims 同帧入 P2（producer 区分在案）
3. **跨源碰头第一次真实发生**：构造同 key 分歧，Conflict 记录集成级在案
4. ReobserveFocused 端到端：冲突 → 定向裁剪重扫 → 销案或升级留档
5. Tier 0 生效：标准控件冲突由 XML 定案，销案记录 tier=category-authority
6. 信任门槛生效：B 级孤证 + 不可运动作 → 未补佐证前拒绝授权
7. 事后验证路由生效：标准控件目标验证不产生截图调用
8. 值格式断言：XML 产 *.state 与既有值域一致（"on"/"off" 小写）
9. 全程零大模型调用（Tier 2 不在本 change）
10. Android 规范调研落 evidence/，覆盖表每格初值有出处

## Verification

（IMPLEMENT 后按四元组回填；验收 3 为本 change 核心证据点）

## Status log

- 2026-09-22 · created·persisted · 立项 grill 三轮落定（docket 见
  evidence/2026-09-22-per009-multisource-grill-docket.md；台账 #16）。

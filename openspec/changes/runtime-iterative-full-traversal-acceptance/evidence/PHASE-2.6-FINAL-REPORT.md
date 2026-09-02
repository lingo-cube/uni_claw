# Phase 2.6 冲刺终报（RUNTIME-ITERATIVE-FULL-TRAVERSAL-ACCEPTANCE）

> 编写：Agent（uni-agent session）· 日期：2026-08-30 · 目标：push Phase 2.6 直到一次真实 run 达到
> `terminal=Completed`。本报告为**人可读的完整证据与诊断**，供独立分析。
> 结论先行：**确定性故障层已全部修复并验证（8 项）；19 轮真机 0 次 Completed；全部残余失败为
> "感知/采集/节奏方差"且被运行时正确 fail-closed；没有洗白、没有隐藏失败。**

---

## 0. TL;DR

| 维度 | 结论 |
|---|---|
| 目标 | 一次真机 run 达到 terminal=Completed（root+child epoch 均 proven、分支全落地、无 unknown/无误失效）|
| 结果 | **0/19** Completed；最深 runV 遍历 4 层容器（root epoch 16 ✓ → child epoch 17 ✓ → 子容器 epoch 4 ✓）|
| 完成项 | 8 项确定性修复，全量 C# 套件 2364/5（5=既有环境性），零新增回归 |
| 残余 | 3 类感知/采集/节奏方差 + 1 项未归因偶发事件，全部 fail-closed 正确，无 invariant-合规修复 |
| 下一个决策 | A 感知通道门（推荐）/ B 接受确定性阶段 / C 冻结 |

---

## 1. 运行统计（19 轮 fresh real，settingscampaign 1，emulator-5554，时间戳采证）

| 类 | 计数 | 代表 run | 状态 |
|---|---|---|---|
| root 层 Unknown（感知方差）| 5 | I, M, O, P, S | 正确 fail-closed（不可修）|
| root 层 Normalize Unresolved（回弹/稀疏窗）| 3 | L, U, W | 正确 fail-closed（不可修）|
| 进 child 的过渡 settle 预算 | 2 | G, K | 节奏类（vision 指示已修，仍波动）|
| 深容器稳定性/Unknown | 5 | N, R, T, V, X | N/R/T 已修；V/X 为深度 Unknown |
| 返回控件缺位（修复前）| 2 | H, J | **已修**（重放证实 ReturnToParent）|
| post-completeness 误失效（修复前）| 1 | F | **已修**（tier 范围对齐）|
| 环境空跑 | 1 | Q | 环境类 |
| **Completed** | **0** | — | 未观测到 |

---

## 2. Trace 树（运行时决策层次 → 终止路径）

```
Run = Startup → root 容器探索(滚动稳定确认×N) → root epoch 完整度 → 分支TAP
      → 过渡settle → child探索 → child epoch → 返回验证 → post-completeness → Completed
        │
        ├─ root:Normalize 不连贯 → UNRESOLVED             [L,U,W]  ← 回弹/稀疏窗（§6.2）
        ├─ root:Unknown 残余 → UNRESOLVED(Unknown)         [I,M,O,P,S] ← 感知方差（§6.1）
        ├─ 分支→child:过渡settle 3观测未稳 → FAIL           [G,K]  ← 渲染节奏（§6.1）
        ├─ child epoch ✓ → 返回控件缺位 → FAIL              [H,J]  ← 已修（§4-6/7）
        ├─ child epoch ✓ → post-completeness INVALIDATED   [F]    ← 已修（§4-5）
        ├─ 深容器:title-off 预算耗尽 → FAIL                 [N,R]  ← 已修（§4-8）
        ├─ 深容器:'Settings' 左出误判 → FAIL                [T]    ← 已修（§4-8）
        ├─ 深容器:节标题/描述 Unknown → UNRESOLVED          [V,X]  ← 感知方差（§6.3）
        ├─ 环境空跑（启动即退出，无 stage）→ -               [Q]
        └─ Completed → 0/19
```

每一层都按设计 fail-closed：**不确定的证据被拒绝，绝不当作确定**（这是本项目最硬的正确性）。

---

## 3. 验收证据链（文档索引，可点击）

`openspec/changes/runtime-iterative-full-traversal-acceptance/evidence/`
- `README.md` — 全部 50 份证据的一行摘要索引
- `SOURCE-NORMALIZER-LOGICAL-ORDER-REPAIR-RESULT.md` — 修复 #1
- `FUSION-PUBLICATION-BOUNDARY-REPAIR-RESULT.md` — 修复 #2
- `PATTERN-5-OCCURRENCE-GRANULARITY-REPAIR-RESULT.md` — 修复 #3
- `ROW-BAND-SUB-ELEMENT-BOUNDED-REPAIR-RESULT.md` — 修复 #4（child epoch 首次完整）
- `POST-COMPLETENESS-NAV14-TIER-SCOPE-DIAGNOSTIC-RESULT.md` + `...-REPAIR-RESULT.md` — 修复 #5
- `DEEP-SCROLL-STABILITY-REPAIR-RESULT.md` — 修复 #8（含 RVT212 回归处理）
- `PHASE26-SCROLL-CADENCE-COVERAGE-EVIDENCE.md` — 残余方差的决定性取证（§6 依据）
- `REAL-CONTAINER-EXIT-CAUSE-EVIDENCE-COLLECTION-RESULT.md` — 偶发退出事件
- `PHASE-2.6-STAGE-DECLARATION.md`、`ROW-BAND-SUB-ELEMENT-BOUNDED-REPAIR-RESULT.md`

**Debug 工具与原始数据**（可复核）：
- `/tmp/p26-push-run{F..X}-(stage|frames|fusion|timestamps).json` — 19 轮全量
- `/tmp/p26-classify.py` — 每 run 闸门触发链分类器（`python3 /tmp/p26-classify.py <stage>.json`）
- `/tmp/p26-semantic-replay` — 语义能力重放器（真帧→真实能力→envelope 树；含 ALL-ENVELOPES dump）
- `/tmp/p26-norm-probe` — Normalize 忠实 bisect 探针（重建观测+admitted evidence，逐前缀定位失败窗对）

---

## 4. 确定性修复账本（8 项，全部 RED→GREEN + 全量验证）

| # | 修复 | FDP/First Divergence | 决定性证据 | 验证 |
|---|---|---|---|---|
| 1 | normalizer **逻辑序投影**（StableKey 行分组 + CenterY band 排序）| 感知序列 ≠ UI 逻辑序，顺序谓词对同一页两次采样结果不同 | 真机同页重放：数组序变换即停摆 | 13/13 语义重放 + 真机 |
| 2 | fusion **发布边界** | 行带内部"卫星"（背景/碎片）被重复发布为顶层世界对象 | runD seq10 重放：顶层 satellite 8 条 | 真机 satellite → 0/帧 |
| 3 | Pattern-5 **occurrence 粒度**（聚合所有碎片 fact 再判同源）| 事实碎片化：同行文字副本每帧被判 0 或 N 个 peer，判定不稳 | 14 个 text_block 副本 peer 数全 0（P5 从未生效）| eligible Unknown 8→2，语义套件全绿 |
| 4 | **ROW_BAND_SUB_ELEMENT 谓词**（副文本=含于/紧邻唯一 menu_item 行带、异文本、尺寸/守卫 fail-closed）| 'Not set'/'Will never'（无同行 menu_item 的副文本）→ Unknown 卡完整性 | runF seq31 几何：'Not set' 全含于 'Screen timeout' 带；'Will never' gap 0.010625 vs P7 容差 0.0105 | **child epoch sources=17 首次完整**；41/41 |
| 5 | post-completeness **tier 范围对齐**（B.3 只解析 eligible）| aux(无 bounds) 候选签名结构性不可能在视觉冻结集 → 必然误失效 | runF 'nav:14'=AuxiliaryStructured 'Dark theme'（canonicalOccurrences 取证）| PCC 18/18 + 全量 |
| 6/7 | 返回控件 **父-角色继承 + vision 指示**（图标是 'Navigate up' 的 child 时继承返回角色；唯一顶带图标+回退标记→返回键）| 返回键=无文本图标；structured 无 bounds → Correlate 两通道都断 | runH/J 返回时刻：ParentReturnControl 候选=0 但 XML 有 'Navigate up' | 重放 runJ seq31 → relation:ReturnToParent；47/47 |
| 8 | 深滚 **title-off/身份误判守卫**（前向探索作用域；静止行集+前景未变→确认）| 深滚子页标记全失 → page=null('title-off') 或回退 'Settings'('左出误判') → 静止帧永远无法成为决策基础 | runN/R（title-off ×3 预算耗尽）、runT（page 'Settings' 误判离开）| RED→GREEN（TitleOff 测试）+ RVT212 回归修复 + runV 4 层遍历 |

**验证记录**：每项全套件后 = `2364 passed / 5 failed`（5 项=既有环境性：CORR_HOST×3、Capstone_RealEmulator、ExternalBoundary_RealDevice），零新增。

---

## 5. 关键诊断分析（每类：机理 → 决定性证据 → 为何无合规修复）

### 5.1 root 层 Unknown 方差（5/19：I,M,O,P,S）——感知通道同步漏检

- **机理**：某行在某帧出现"双通道同步漏检"：视觉只出短文本 tb（OCR 短读）、结构化缺该行（uiautomator 漏枚举）。
- **决定性证据**（runM seq7/8）：tb 'Wallpaper'（row_014, Y 0.79-0.81）——同帧**无 menu_item peer**，XML 行列表截于 'Display'（结束 band 0.831-0.952），'Wallpaper' 带位无任何其他通道同类行。'Accessibility'（同帧有同行 mi）→ 立即 NonInteractive ✓（对照成立）。
- **为什么不能修**：把无验证依据的 tb 提为可交互行 = 违反冻结不变量"不按单项证据判 interactivity"；Unknown 是诚实结果。同批其他 run（F/H/J/K/L/N）root 都完成 → 该行在其他 run 通道正常 → 纯方差。
- 变体：runO/S/P = textless 根图标（ICON_TEXTLESS 已登记）、'Lou'（OCR 乱码已登记）、'Bluetooth, pairing'。

### 5.2 root 层 Normalize Unresolved（3/19：L,U,W）——回弹/分屏帧入 epoch

- **机理**：探索滚动快速 fling 在页缘/惯性中产生**分屏帧**（如 runU win14：顶部 row_018/022/009 + 底部 row_015~021 混排，行序非单调）或**纯重复窗**（win20 回到顶部、0 新源）→ stability 局对稳定（相邻两帧相同）→ 接受入 epoch → normalizer 无法单调合并 → 正确 Unresolved。
- **决定性证据**：`/tmp/p26-norm-probe` 忠实重放 runU：prefix..seq17 起 Unresolved；win14 的投影序列 [row_018@0.153, row_009@0.223, row_022@0.25, row_010@0.322, row_015@0.417...] 与 win17 [row_015@0.218 ...] 交错（union 内 018/009/022/010 插在 015 前）→ anchor 连续性破坏。
- **为什么不能修**：接受不连贯证据 = 放宽 fail-closed；运行时已有自适应步长（0.4→0.8）+ 证据质量 settle。属节奏/采集方差。

### 5.3 深页节标题/描述 Unknown（V 的 Accessibility 容器）——语义信号真空

- **机理**：深页（Accessibility）的**非交互节标题**（'Interaction controls'/'Captions'/'Audio'/'General'）与**长描述**（"Hear a description of what's happening..."）无同行 menu_item、无同带 XML、不与任何已知行带相邻到幼 gap → 无合规信号。
- **决定性证据**：runV seq47/50/53/56 的 Unknown 列表（见 §1 表）逐帧分布。
- **为什么不能修**："任意非可点文本→NonInteractive" 被冻结禁止（会被系统性吞掉真交互行）；P7/副文本规则边界几何外 → 诚实 Unknown。

### 5.4 未归因偶发事件（1/19：r5 滚底退回根页）

- 已排除：运行时多发动作、输入翻译偏差、手势碰撞（坐标 70%→30% 中轴）。**未捕获**：设备日志/录屏缺失、受控 0/5 未复现 → 诚实归 I. UNKNOWN。采证管道（时间戳 tap + 录屏/logcat 驱动）已就绪。

### 5.5 采集通道结构性发现（影响 §5.1/§5.2 的根因层）

- uiautomator 同静态页重 dump 结果可整段不同（探针实测 2 行 vs 16 行）；XML 与视觉坐标存在**页相关偏移/缩放**（'Network & internet' XML center 867px vs 视觉 514px），且随帧变化——初步"1939px 帧"假说已被根节点实测 `[0,0][1080,2400]` 推翻，确切像素映射**未完全解析**（诚实声明：部分推断）。这就是感知通道可靠性问题的实质。

---

## 6. 诚实边界声明

- **事实**（有传感器/重放证据）：8 项修复每项的行为改变；19 轮终态分布；各 Unknown 行的几何与通道缺席；normalizer 失败窗对。
- **推断**（标出）：XML/vision 坐标偏移的确切像素映射（部分拟合）；'Wallpaper' 帧的 OCR 是否"短读"（有证据但 OCR 内部不可见）。
- **未知**：偶发退出触发源（I. UNKNOWN）；一次 Completed 是否存在（未观测，不能断言不存在）；感知通道改进后能否达成（未试验）。
- **未宣称**：不宣称 PASS / 毕业（需独立评审 `N-graduation-readiness`）。

---

## 7. 决策选项（供独立分析后裁决）

- **A 感知通道门**（推荐）：① 结构化通道换 accessibility bridge / 截断重采（证据：静态页重 dump 可补齐漏枚举）；② OCR 乱码/短读鲁棒性。独立 Large gate，不动运行时判定。
- **B 接受确定性阶段**：以"机制可信 + 残余编目"为验收点关闭 Phase 2.6（本轮证据=最强交付）。
- **C 冻结**：维持 STOPPED、证据存档。

任何选项都不需要"修完整性/fail-closed"——那些是本轮证明正确工作的部件。
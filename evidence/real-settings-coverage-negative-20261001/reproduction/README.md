# 现场复现说明（2026-10-02）

## 重要声明

AGT-005/006/007 各次真实运行**未持久化截图与 hierarchy XML**——截图在
运行中仅作为视觉服务输入（内存用完即弃），证据里只留 captureId。因此
"当时的截图"不存在；本目录是事后用同型号模拟器（API 35，1080x1920，
同款手势 `input swipe 540 1575 540 549 300`）的**复现**。

## 复现截图

- `01-root-top.png`：Settings 根页首屏（任务起点）。
- `02-after-first-scroll.png`：第一次滚动后（对应任务中 step 2/10）。
- `03-after-second-scroll.png`：第二次滚动后（对应 d3 step 19 之后、
  step 20 "Security & privacy" 点击发生时的屏幕区域）。

## 复现核实（hierarchy 转储，与 03 截图同时采集）

二次滚动后可见：Search settings / Wallpaper / Accessibility /
**Security & privacy** / Location / Safety & emergency / System /
About emulated device 等。

- **Security & privacy 行在场** ✅——即 step 20 点击的目标。
- **Dismiss 按钮不在场** ❌——低电量/系统通知卡**未在全新模拟器上复现**
  （仅出现在长时间运行的会话；d1/d3/d4 中它于 step 20 失败后出现）。
  当时它具体是哪张卡片（电量？充电？安全中心提示？）**无法从留存的
  证据确证**——这正是未持久化现场造成的追溯缺口。

## 由文本证据确证的事实（无需截图）

- step 21 点击 Dismiss 的落点 (943,985)（receiptCommand）→ 屏幕右侧
  中部，符合"卡片右上角按钮"形态。
- 点击后路由变为无标题身份（`android.settings`）→ 确实发生了页面变化
  （navigation-transition check 通过）。

## 追溯缺口与后续

教训：per-step 截图与 XML 应作为证据工件落盘（当前只有 captureId 引用）。
已列为后续 change 候选（证据工件持久化）。

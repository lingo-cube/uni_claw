# UI-TARS 基准测试与性能测试（BENCHMARK）

> 模型：bartowski/UI-TARS-2B-SFT（Q4_K_M + mmproj-f16）· llama-server（Metal, -ngl 99, n_slots=4, ctx 4096）
> 机器：Apple M4 / 24GB · 日期：2026-08-30 · 数据：`artifacts/uitars-bench/bench-results.json`
> **结论先行：模型 grounding 准确（坐标校准后 root 页亚像素级）；"逐框 miss"是坐标系伪影，不是定位失败。**

## 1. 方法（真值独立于 OCR）

每页**截图与 uiautomator dump 配对**（同刻采集，像素空间 1080×2400）；真值= dump 中带 bounds 的文本行
（`truth.json`，4 页 71 行）。对每个真值行发单目标定位查询（UI-TARS 为指令式定位模型，非列举型——
"列举所有元素"会退化，单目标提示后可用）。判定：解析 `<box>` 中 y，与真值 center-y 比较。

## 2. 结果：坐标系发现（关键）

模型输出坐标位于**其自身预处理图像空间**（llama.cpp qwen2vl 预处理把 1080×2400 缩放到 ~1000px 高画布），
**不是**设备像素空间。逐图线性校准（真值 ≈ a×模型 + b）：

| 页面 | 拟合 a | 拟合 b | 校准后平均误差(px, 共2400) | n |
|---|---|---|---|---|
| root-top | 2.44 | −19 | **14** | 16 |
| root-scrolled | 2.51 | −75 | **35** | 20 |
| accessibility | 2.19 | +214 | 137 | 15 |
| display-child | 2.32 | +126 | 118 | 16 |

- root 两页：校准后 14–35px ≈ **亚像素级**（2400 高）——grounding 可用。
- accessibility/display-child：残差 118–137px（系统性但偏大）。假设（未定论）：dump 与截图间瞬时滚动
  差异，或密集文本页定位降级；需用**逐帧同刻验证**排除。

## 3. 性能画像（71 次查询）

| 指标 | 值 |
|---|---|
| 每图首个请求（图像编码+首token）| 25.5–29.0s |
| 热调用 p50（同图后续目标）| **0.3s** |
| 总延迟范围 | 0.1–29.0s |
| prompt tokens（含图）| ~3435 |
| slots | 4（llama-server 并发槽）|
| 内存峰值 | 模型+投影 ~2.5GB（24GB 机器无压力）|

**对真机 A/B 的意义**：每图首调 ~26s 远超运行时的观测节奏预算（settle/quiescence 按 ~1s 帧设计）——
直接替换不可行；可行形态 = **离线并行批处理**（深度优先逐格缓存：一次爬取 50+ 截图，批量化 grounding，
再进 fusion）或 7B+ 量化提速后重测。

## 4. 诚实边界

- 2B 保真上限未知（7B/DPO 待测，需带 mmproj 仓库）。
- 校准系数逐图有差（2.19–2.51/偏移 ±200px）——若做管道，需要**每观测校准**（同 aspect 页面系数可缓存，
  但字偏移/缩放随预处理稳定，需实测验证）。
- 单点/四点多格式输出混用，解析器需兼容 `<box>(x,y,x,y)</box>` 与单点。
- 本节 n=4 页面，统计面小；error ~118-137px 两页的原因未闭环（缺图像目检，本模型无图像输入）。

## 5. 资产

`artifacts/uitars-bench/`：`truth.json`（真值表）、`bench-results.json`（71 条逐行原始+状态）、
`bench.py`（复跑脚本）、`{root-top,root-scrolled,accessibility,display-child}.png/.xml`（配对原始）。
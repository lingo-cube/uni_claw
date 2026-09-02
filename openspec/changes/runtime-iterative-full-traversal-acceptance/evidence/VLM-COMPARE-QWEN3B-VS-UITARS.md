# Qwen2.5-VL-3B-UI-R1 vs UI-TARS-2B — 同场对比基准报告

> 方法：与 2026-08-30 UI-TARS 基准完全相同的 71 查询 grounding 测试
> （4 页真机截图 × uiautomator 真值行；单目标定位提示；判定 = 定位框
> center-y vs 真值 center-y）。同一台 Apple M4/24GB、同一 llama-server
> 配置（Metal, -ngl 99, ctx 4096）、同一提示词、温度 0。
> 数据：`artifacts/vlm-compare/`（两模型逐条原始结果 + 校准系数）。

## 结论先行

**两者互补，各有硬伤，都不能在线上用（首调 ~20s ≫ 1s 帧预算）——
但作为离线批处理语义层，Qwen 3B 的有效精度更高，UI-TARS 的覆盖率更高。**

## 逐页对比（校准后平均误差，px，画面高 2400）

| 页面 | UI-TARS-2B（需逐图校准 a≈2.2–2.5） | Qwen-3B-UI-R1（原生设备坐标 a≈1.0） |
|---|---|---|
| root-top | 14（94% <35px） | **10**（93% <35px） |
| root-scrolled | 35（75%） | **5**（**100%**） |
| accessibility | 137（27%） | 136（20%） |
| display-child | 118（25%） | **6**（**100%**） |

- **UI-TARS 复跑与 8/30 基准逐字一致**（校准系数 2.44/2.51/2.19/2.32 完全
  复现）— 基准可复现性验证通过。
- **Qwen 优势**：输出就是设备像素坐标（无需坐标校准管道）；root 滚动页与
  display 子页达到 5–6px 平均误差（2400px 高上≈亚像素级），其中 display-child
  正是 UI-TARS 最差的页（118px）。
- **共同弱点**：accessibility 页（密集小字文本页）两者都在 136–137px —
  该页对 2–3B 级 VLM 是系统性难点，与品牌无关。

## 覆盖率（关键差异）

| 指标 | UI-TARS-2B | Qwen-3B-UI-R1 |
|---|---|---|
| 有问必答 | **71/71（100%）** | 50/71（70%） |
| "not visible" 拒答 | 0 | **21（30%）** |
| 解析失败 | 0 | 1（多框输出） |

Qwen 拒答的目标集中于：页标题（'Settings'）、**分组标签（'Display'、'System'）**、
**副标题（'38% used - 9.96 GB free'、'Languages, gestures, time, backup'）**、
状态文本（'100%'）。它只对"真实可交互行"给出定位 — 对我们恰好最头疼的
标签/副标题类元素选择拒答而非猜测（fail-closed 风格，但意味着这类元素
拿不到坐标证据）。

## 性能与资源

| 指标 | UI-TARS-2B | Qwen-3B-UI-R1 |
|---|---|---|
| 每图首调（图像编码+首token） | ~26s（旧基准）/ 本次类似 | 20.2–21.3s |
| 热调用 p50 | 0.30s | 0.70s |
| 全程 71 查询 | 1m59s | 2m05s |
| 常驻内存（RSS） | 2.67 GB | 4.08 GB |

两者都远超运行时 ~1s 观测节奏预算 → **只能作为离线并行批处理层**
（爬取批量化 → grounding → 回填 fusion），与旧基准结论一致。

## 对管线问题的直接意义

1. **display-child 页的 6px 定位**证明 Qwen 能在"标签+行"混合页上把
   'Colors' 这类真行定位准 — 若离线批处理需要 VLM 语义佐证，Qwen 3B
   是更强的候选。
2. Qwen 对标签/副标题的**选择性拒答**与我们的 fail-closed 理念同构，
   但意味着"标签归属"问题它不会替我们回答（它只是不说，不是判别）。
3. UI-TARS 的 100% 应答 + 需校准特性，适合"宁可全答再过滤"的场景。

## 边界与回收

- 离线只读基准；零 Runtime/生产行为改动；未违反 Slow/Provider 冻结。
- 测试后已**全部关闭回收**：llama-server 进程全部停止（验证 0 残留）、
  临时下载的 UI-TARS 模型文件（2.4GB）已删除；Qwen 为用户自有本地模型
  （/Users/fran/models，保留原样，进程已停）。
- n=4 页/71 行，统计面小；2–3B 级模型上限未知（7B 待另测）。

## 资产

`evidence/artifacts/vlm-compare/`：`bench-results-uitars.json` /
`bench-results.json`（Qwen）/ `calib-uitars.json` / `calib-qwen.json`
（逐条原始输出、延迟、逐图校准系数）。

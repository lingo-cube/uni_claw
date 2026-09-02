# VLM 类型识别精度对比 — Qwen2.5-VL-3B-UI-R1 vs UI-TARS-2B

> 任务：元素**类型判别**（非坐标）。71 个文本元素（4 页真机截图），真值取自
> 同刻 uiautomator XML 的 resource-id + 可点击祖先（title→行标题、
> summary→副标题、无 id 且不可点击→标签/静态标题）。逐元素单问，三分类
> （row_title / row_subtitle / section_label），温度 0。
> 数据：`artifacts/vlm-compare/type-*.json`（真值 + 两模型逐条预测）。

## 结论先行

**Qwen 类型准确率 82%，UI-TARS 仅 27%（它本质是定位/动作模型，分类不在其
训练分布）。但对我们最关键的"标签 vs 行"判别，两个模型都不可靠 —
2–3B 级 VLM 解决不了我们管线最头疼的那个问题。**

## 总体（static_title 并入 section_label 口径）

| 模型 | 总准确率 | row_title | row_subtitle | section_label |
|---|---|---|---|---|
| Qwen-3B-UI-R1 | **58/71 = 82%** | P 93% / R 80% | P 85% / R 85% | P 50% / R 78% |
| UI-TARS-2B | 21/71 = **27%** | P 60% / R 9% | P 40% / R 59% | P 0% / R 0% |

UI-TARS 的失败模式：把大量行标题叫成 row_subtitle/section_label（32 个
行标题只对 3 个），5 条不可解析 — 类型问答超出其 SFT 能力，属预期。

## 关键案例（幻影行 'Color' 对决）

| 元素 | 真值 | Qwen | UI-TARS |
|---|---|---|---|
| **'Color'（分组标签）** | section_label | **row_title ✗** | section_label ✓ |
| 'Colors'（真行） | row_title | row_title ✓ | **section_label ✗** |
| 'Show all notification content' | row_subtitle | row_subtitle ✓ | row_subtitle ✓ |
| 'Settings'（页标题） | section_label | 1对1错 | 错 |

- **Qwen 把 'Color' 判成行** — 与我们管线的幻影行错误**同向**；它对副标题
  和一般行很准（85–93%），但小字号分组标签倾向当行
- **UI-TARS 把 'Color' 判对了、却把 'Colors' 也判成标签** — 它是"万物皆
  标签"偏置下的偶然命中，无判别力
- **结论：两个模型都不能作为标签/行判别的权威来源**。该问题仍属结构化
  佐证层（XML resource-id 是 100% 权威真值）或检测器能力的领地

## 合并两轮测试的总画像

| 维度 | Qwen-3B-UI-R1 | UI-TARS-2B |
|---|---|---|
| 定位精度（同题 50 题） | **中位 6px，80% ≤25px** | 中位 25px，52% ≤25px |
| 定位坐标体系 | 原生设备坐标（免校准） | 需逐图校准（a≈2.2–2.5） |
| 类型识别 | **82%** | 27% |
| 覆盖/应答 | 70%（拒答标签类 30%） | 100% 应答 |
| 首调延迟 | ~20s | ~26s |
| 内存 | 4.08 GB | 2.67 GB |

**适用建议**：若未来购买离线 VLM 佐证层，Qwen 3B 是唯一候选（定位+类型
双优）；但"标签 vs 行"这一子问题必须走结构化 XML（uiautomator）—
本次真值本身即证明 XML 是 100% 准确的权威源，且模拟器/真机上零成本可得。

## 边界与回收

- 离线只读基准；零 Runtime/生产行为改动；Slow/Provider 冻结未触碰。
- 测试后 llama-server 全部停止（0 残留验证）、临时 UI-TARS 模型（2.4GB）
  已删除；Qwen 为用户自有模型，文件原样保留。
- n=4 页 71 元素；提示词单一（三分类封闭问答），未做提示工程调优 —
  数字是下界而非上限。

## 追加：云端视觉模型难例对决（4 道最难题，同图同题）

模型：opencode-go `deepseek-v4-flash-vision-exp`（云端视觉；720px JPEG 输入，
Cloudflare 拦 urllib 默认 UA 需自设 UA 头 — 已记录）。zai glm-5v-turbo 因
GLM Coding Plan 不含视觉款（1311）无法测试；nova sensenova-6.8 限流（429）。

| 难例 | 真值 | Qwen-3B-UI（本地） | UI-TARS-2B | deepseek-vision-exp（云） |
|---|---|---|---|---|
| 'Color'（分组标签） | label | ✗ row_title | ✓ label | **✓ label** |
| 'Colors'（真行） | row | ✓ row_title | ✗ label | **✗ label** |
| 'Settings'（页标题） | label | 半对（同词两页一次对一次错） | ✗ | **✓ label** |
| '38% used - 9.96 GB free'（数字副标题） | subtitle | ✗ label | ✓ subtitle | **✓ subtitle** |
| **计分** | | 1.5/4 | 2/4 | **3/4** |

关键发现：**没有任何模型同时答对 'Color'+'Colors' 这一对** — 每个模型都
恰好错其中一个。同屏相邻的大小字号判别是所有已测 VLM 的共同盲区，进一步
印证"该问题须走结构化 XML（resource-id 100% 权威）而非更大 VLM"。

延迟：2.7–10.7s/题（云端离线批处理可用）。数据：
`artifacts/vlm-compare/hard4-ocgo-vision.json`。

## 决定性追加：布局文本 + 合理提示词 = 4/4 全对

条件：**不给截图**，只给布局文本序列（XML 序列化：y 范围(高)/x 范围/
clickable/class/text，**已剥除 resource-id 防作弊**），提示词只讲通用判别
原则（框高=字号、clickable=交互性、纵向邻接=归属），模型用普通**文本**款
deepseek-v4-flash（无任何视觉能力）：

| 难例 | 真值 | 布局文本+提示词（文本模型） |
|---|---|---|
| 'Color' | label | **✓ label** |
| 'Colors' | row | **✓ row**（全场首次与 'Color' 同时答对） |
| 'Settings' | label | **✓ label** |
| '38% used - 9.96 GB free' | subtitle | **✓ subtitle** |

结论：**失败从来不是信息缺失，而是 VLM 的视觉通道**。同一信息以结构化
文本呈现时，判别信号（高度比/可点性/全宽/邻接）变为显式，普通文本模型
即可全对。这是"结构化佐证层（路线 B）"的最强实证 — uiautomator XML 在
模拟器/真机零成本，甚至无需直接消费 XML，"XML→紧凑文本→轻量模型/规则"
即可作为离线语义佐证层。延迟 2.8–30s/题（离线批处理可用）。
数据：`artifacts/vlm-compare/hard4-layout-text.json`。

## 终局追加：感知自身原始输出（YOLO+OCR）序列化 = 4/4 全对

条件：**不用 XML、不用截图** — 把感知管线自己的原始输出（YOLO 21 类裸框 +
逐框 OCR，含全部噪声：乱码/重复框/抖动）序列化为逐行文本
（y范围(高)/x范围/conf/[类]/ocr文字），提示词只讲几何原则（高度=字号、
模态行高、纵向邻接=归属），无 clickable、无层级。模型 deepseek-v4-flash
（纯文本，无视觉），4 类（+page_header）：

| 难例 | 原始 OCR | 真值 | 预测 |
|---|---|---|---|
| 'Color' | 'Color'（清晰） | label | **✓ label** |
| 'Colors' | **'0 O S'（全乱码）** | row | **✓ row**（靠 h=43 匹配行高模态） |
| 'Settings' | 'Settings S' | header | **✓ header**（h=89 远超行高） |
| '38% used - 9.96 GB free' | 含数字 | subtitle | **✓ subtitle** |

结论：**判别信号完全存在于感知已有输出中，只需换表示形式**（像素→坐标
文本）。视觉模型从像素提取不出该结构；文本模型读坐标序列即可 — 且对
OCR 乱码鲁棒（几何承载信号）。工程含义：离线语义佐证层可直接挂接现有
fusion 原始检测（每帧已在采集），零新传感器、零 XML 依赖；与 label-height
几何规则同源（模型用的正是高度模态信号）。延迟 4–76s/题（离线可用）。
数据：`artifacts/vlm-compare/hard4-yolo-raw-layout.json` +
`yolo-ocr-layout.json`（原始脏 dump）。

## 7B 对照（ggml-org Qwen2.5-VL-7B-Instruct Q4_K_M，本地 llama-server，同 9 题双模式）

| 配置 | 9 题得分 | Color/Colors | 延迟 |
|---|---|---|---|
| 3B-UI 图像 | 7/9 | ✗✓ | 热 0.3s + 每图 ~20s 税 |
| **7B 图像** | **6/9** | **✓✗（错误反转）** | **~31s 每次**（无热缓存收益） |
| 3B-UI 文本 | 4/9 | ✗✓ | 1.4s 平 |
| 7B 文本 | 4/9 | ✗✗ | 4.2s 平 |
| 云端 DS 文本 | 8/9 | ✓✓（唯一双对） | 2.4–45s |

结论：**本地放大到 7B 是此任务的死路** — 图像模式不升反降（6/9）且每次
~31s；文本模式与 3B 同分（4/9）。3B-UI-R1 的 UI 微调（而非规模）才是本地
图像表现的来源；Color/Colors 在 7B 图像下错误方向反转（判对 Color 判错
Colors），仍无本地模型双对。云端 deepseek-v4-flash + 布局文本（8/9，
唯一 Color/Colors 双对）保持全场最优。7B 内存 5.9GB（3B 4.1GB）。
数据：`artifacts/vlm-compare/dual/subset-7b-both.json`。

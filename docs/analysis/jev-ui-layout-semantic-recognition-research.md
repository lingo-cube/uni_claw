# Jev 能否做 UI 布局识别与语义识别？（GUI 自动化适用性调研）

> Status: `CANDIDATE / NOT_ADOPTED`（web-only 调研产物，未授权实现）
> Authority: `NONE`（调研记录，不是架构决策；与 ADR / baseline 冲突时以后者为准）
> Date: 2026-10-21（一手文档修订状态截至 2026-10-15；未做本地实测、未调用 API）
> Scope: 只回答一个问题——Jev 如何被用于「元素在哪里 / 结构几何」（布局识别）与
> 「每个元素是什么 / 角色 / 标签 / 可供性 / 该做什么动作」（语义识别）。仅使用
> 公开 web 来源。

**证据分级图例**（全文贯彻）

- **[官方]** = TypeSafe 一手文档 / OpenAPI / 一手仓库 README。
- **[集成方]** = 下游一手代码或集成方文档（Anchor 文档、browser-use 仓库等）——对
  Jev 的用法是事实，对 Jev 内部机制只是推断。
- **[媒体/社区]** = 新闻稿、聚合站、X 帖子、二手评测。
- **[未验证]** = 找不到一手来源，或来源互相冲突。

---

## TL;DR 裁决

**Jev 不是版面解析器，也不能单独充当语义识别器；它是「在既有候选集上做类型化判断」
的决策层。** 三条硬事实：

1. **输出永远是你给的选项之一，不是新内容。** Choice / Score / Noul 返回
   `choice|score|noul` + 概率分布 + confidence；它不生成文本、不写坐标、不写选择器
   （[官方：primitives](https://docs.typesafe.ai/primitives)、
   [官方：System One](https://docs.typesafe.ai/concepts/system-one)）。
2. **几何只有一条窄路：`Box` 原语**，返回图片归一化四边（0–1），但**一道题只能框一个
   目标**、需要图片、需要 `jev-1.14`（`jev-preview`）、要开 `TypeSafe-Danger-Zone-Images`
   头，且官方自称 "Dreamer-ready"——**明确不到生产标准**
   （[官方：Box](https://docs.typesafe.ai/primitives/box)）。
3. **所有真实落地的 GUI agent 都把「感知」放在 Jev 之外**：DOM 索引表
   （[browser-use/jev-ultrafast](https://github.com/browser-use/jev-ultrafast)）、
   无障碍树（[jev-mobile](https://github.com/xinwang-nwpu/jev-mobile)）、
   端上 CoreML 分割 + OCR（[Milind S](https://x.com/milindlabs/status/2100631847155994852)）。
   Jev 消费的是**它们产出的文本/JSON 候选表**。

**对「既要几何+结构、又要语义」的引擎：Jev 只能承担 (b) 的判定与仲裁，且必须先由
DOM/A11y 提取器、视觉解析器（OmniParser 类）或 OCR 把候选做出来；(a) 必须由别的组件
提供。** 详见 §10 与 §12。

---

## 1. Jev 到底是什么

| 维度 | 事实 | 来源 |
|---|---|---|
| 类别 | TypeSafe 的旗舰模型，也是首个 "System One" 模型（一类**决策模型**，非 LLM 文本生成） | [官方](https://docs.typesafe.ai/introduction) |
| 厂商 | TypeSafe AI；联合创始人 Diogo Almeida（官方称其为 RLHF / InstructGPT 的共同发明人） | [官方 primer](https://docs.typesafe.ai/introduction/machine-learning-primer) |
| 形态 | **托管 API**（`POST https://api.typesafe.ai/v1/systemone`）+ Python/JS SDK + 官方 Agent Skill + Playground；**不开源权重** | [官方 quickstart](https://docs.typesafe.ai/introduction/quickstart)、[OpenAPI](https://docs.typesafe.ai/api/system-one) |
| 开源状态 | 闭源。"Jev is a closed model, served from TypeSafe's API, and the weights have not been published." | [媒体/聚合](https://madewithjev.com/open-source-jev) |
| 发布 | 2026-09-15（聚合口径给日期；官方 `GET /v1/models` 的示例里 `jev-latest` 的 `release_date` 同为 2026-09-15）；`jev-latest` = `jev-1.13.0`；`jev-1.14.0` 自 2026-10-14 起可用（`jev-preview` 指向它） | [聚合](https://madewithjev.com/what-is-jev)、[官方 models](https://docs.typesafe.ai/models)、[官方 list-models 示例](https://docs.typesafe.ai/api/list-models) |
| 定价 | **$42/Btok = $0.042 / 百万输入 token，输出 token 免费** | [官方 models](https://docs.typesafe.ai/models) |
| 上下文 | 每请求 64k / 每题 32k；开 `TypeSafe-Danger-Zone-Long-Context` 后均 262k | 同上 |
| 名称由来 | Jevons 悖论（便宜→用量涨）；"System One" 取自 Kahneman 的快思考 | [媒体](https://developer.aliyun.com/article/1765567)、[官方](https://docs.typesafe.ai/concepts/system-one) |

**关于 "APUS"：APUS 不是 Jev 的作者。** 一手链路是：Jev 由 TypeSafe AI 发布；中国厂商
APUS（麒麟合盛）在 2026-09-19 **独立复现**了 Jev 的决策范式，并以 **MIT** 协议开源为
Agent Skill `fast-browser-use`，用本地 Qwen3.5-9B 做单次前向打分，宣称在 Apple M2 Pro
上离线完成真实维基百科检索任务中位约 18 秒、单任务模型打分 4 次
（[新华网](https://app.xinhuanet.com/news/article.html?articleId=202609216ad2782938944456924e1016dd3b84fd)、
[央广网](https://tech.cnr.cn/techph/20260920/t20260920_527819594.shtml)）。
注意：这是**媒体转述 APUS 自述**，`fast-browser-use` 的仓库/代码本身我未定位到可核验地址
（**[未验证]**）。因此本文谈 "Jev" 一律指 TypeSafe 的托管模型；谈 APUS 时只作为
"第三方复现 + 机制推测"。

### 1.1 可核验的自托管替代：APUS Reflex（不是 Jev 本体）

比媒体稿更硬的一条自托管路径是 HF 上的 **APUS Reflex**（即历史 `APUS-OpenJev-v1` 更名后的
模型族）：4B / 9B / 35B-A3B 三档，Qwen3.5 底座，代码 MIT、Qwen 派生权重 Apache 2.0；
自带 `effort=low|high` 的「可调计算深度」，并提供与 `typesafe-sdk` 兼容的
`POST /v1/systemone` 部署包（见 [模型卡](https://huggingface.co/apus-ailab/APUS-Reflex)）。

但这**不是同一个模型**，模型卡自己写明三点，直接决定它在任何严格验收里只能算「另一个
provider」：

- 兼容性「不声称相同模型行为或官方概率校准」；
- 「**Candidate probabilities are not calibrated confidence**」，业务阈值必须重新验证；
- BF16 merge 已改变部分候选概率，校准与路由阈值**必须重校**。

其自报数据（Frozen80 80 题）为 35B-A3B 88.75%、9B 85.0%、4B 82.5%，同面板 Jev API 82.5%；
9B 单卡（RTX PRO 6000 96GB）vLLM 全响应 P50/P95/P99 = 25.58 / 222.36 / 275.58 ms。
这些是**同厂商自测**，不能拿来反推官方 API 的延迟或准确率。

---

## 2. 输入：它吃什么

### 2.1 官方定义的输入形状

`state` 接受 **string / JSON object / array**；questions 的 `instructions` 与 `criteria`
也同样接受嵌套 JSON 结构，可用反引号路径引用 `state` 里的字段
（[OpenAPI schema](https://docs.typesafe.ai/api/system-one)、
[primitives](https://docs.typesafe.ai/primitives)）。

```jsonc
// 真实请求体（browser-use/jev-ultrafast 的 model.py，节选）
{
  "model": "jev-latest",
  "state": {
    "page": {"url": "...", "title": "...", "text": "..."},
    "elements": [
      {"index": "1", "role": "button", "label": "Change ticket type", "value": "", "operations": ["CLICK"]},
      {"index": "2", "role": "combobox", "label": "Where from?", "value": "San Francisco", "operations": ["CLICK", "TYPE_TEXT"]}
    ],
    "recent_actions": [{"action": "CLICK", "kind": "...", "text": "...", "page_changed": true}]
  },
  "questions": { "operation": {"type": "choice", "criteria": {"CLICK": "...", "DONE": "..."}} }
}
```

来源：[`jev_ultrafast/model.py`](https://github.com/browser-use/jev-ultrafast/blob/main/jev_ultrafast/model.py)。
**这就是 Jev 在 GUI 场景的真实输入形态：一张由外部代码构建的、带编号与语义属性的
元素表，加上页面可见文本。**

### 2.2 图片：能，但只在 "Danger Zone"

- 图片作为 asset（PNG/JPEG，`part` 或 `data_base64`），可出现在 `state`、
  `instructions`、`criteria`（[官方：Images](https://docs.typesafe.ai/images)）。
- 必须先发 header `TypeSafe-Danger-Zone-Images: true`，否则 400。
- 硬限制：单边 **280–1568 px**、总像素 78,400–2,458,624、单图 16 MiB、请求 32 MiB；
  超限直接拒绝，**不缩放**；EXIF 方向不处理（需自己转正）。
- 官方验收状态：**"Dreamer-ready. Images and Box questions aren't yet up to our standards to
  call ready for production."**——格式与精度都会随版本变。

### 2.3 官方文档自相矛盾（必须标注）

- [concepts/system-one](https://docs.typesafe.ai/concepts/system-one)：
  "Jev currently accepts text input only… Images, audio, and video are not supported (yet)."
- [models](https://docs.typesafe.ai/models)：1.13 / 1.14 的 Input 一栏都写作 **Text**。
- [images](https://docs.typesafe.ai/images)：却写 "Jev 1.13 and Jev 1.14 both accept images.
  Box questions need Jev 1.14."

→ 结论：**图片输入是"预览态、按版本漂移"的能力**，一手文档没有统一口径（**[未验证]**：
哪个页面是准的）。工程上必须按 `jev-1.14.0` 显式钉版本并自测。

### 2.4 视频 / DOM / a11y 树

- **视频：不支持**，只能自己导帧成静态 PNG/JPEG（同上 images 页）。
- **DOM / A11y 树：不是原生输入类型**，必须由调用方序列化成文本/JSON 再塞进 `state`。
  这正是所有集成方的做法：
  - `jev-ultrafast`：**DOM 直读**（不是 a11y 树），一次快照读出可见控件的
    name/value/text 与真实 DOM 节点引用（[README](https://github.com/browser-use/jev-ultrafast)）。
  - `jev-mobile`：A11y 树 → 索引化元素表，字段含 `className / text / contentDescription /
    resourceId / bounds / checked / selected`（[README](https://github.com/xinwang-nwpu/jev-mobile)）。
  - `jev-browser`：明确写 **"Elements come from the DOM directly, not the accessibility tree,
    because accessibility trees under-report inputs"**——换用 DOM 之后才找到 DuckDuckGo 的
    搜索框（[README](https://github.com/jkudish/jev-browser)）。

### 2.5 "computer use without screenshots" 到底是什么意思

那句宣传语的原文（Milind S，X）：

> "Jev can actually do computer use really well **Without any screenshots, or LLMs and no Pixels
> leave my mac** … A local CoreML model segments every button and UI element on screen.
> On-device OCR reads the labels. **That text is all Jev gets.** It returns a probability across
> those elements and tells me the best one to click. Then it clicks, re-runs detection, and
> decides again. **~90ms per decision.**"
> — [x.com/milindlabs](https://x.com/milindlabs/status/2100631847155994852)（[聚合镜像](https://madewithjev.com/builds/computer-use-without-screenshots)）

拆开看，它成立的是三件事，而不是"Jev 不需要感知"：

1. **不把像素发到云端**（隐私），2. **不用 VLM/LLM 做决策**（延迟），
3. **Jev 只收文本**——因为**有人在本地替它做了分割 + OCR**。

同一模式的另两个一手佐证：Anchor 的 `jev` agent 由 **agent 侧**读页面并"builds a numbered
list of the controls"，Jev 只在这张表里选（[Anchor 文档](https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev)）；
`jev-ultrafast` 明确 "**No screenshots in the default agent loop.** Jev consumes structured
state."（README）。

> **判定：** "without screenshots" = **不进模型的截图**，而不是 **没有视觉感知**。
> 感知被搬到了本地/前置组件，且其质量直接决定 Jev 的上限。

---

## 3. 输出：格式、能不能当布局表示

### 3.1 四种原语与返回字段（官方）

| 问题类型 | 语义 | 返回 |
|---|---|---|
| `choice` | 从固定选项里选一个 | `choice`、`probabilities`（每个选项一个概率，和≈1）、`confidence` |
| `score` | 按有序档位打分 | `score`（期望值，可落在档位之间）、`legend`、`probabilities`、`confidence` |
| `noul` | 判断题的概率 | `noul`（0–1） |
| `box` | 图里某物在哪 | `box{top,bottom,left,right}`（0–1 归一化）+ `confidence`（**可能为 null**） |

来源：[primitives](https://docs.typesafe.ai/primitives)、[Box](https://docs.typesafe.ai/primitives/box)、
[OpenAPI](https://docs.typesafe.ai/api/system-one)。

```jsonc
// 官方响应示例（choice），answers 以你的 question id 为键
{
  "model": "jev-1.13.0",
  "answers": {
    "department": {"type": "choice", "choice": "returns", "confidence": 1.0,
                   "probabilities": {"shipping": 0.0, "returns": 1.0, "billing": 0.0}}
  },
  "usage": {"input_tokens": 328, "output_tokens": 34}
}
```

```jsonc
// 官方响应示例（box）——唯一的几何输出
{"answers": {"mug": {"type": "box",
  "box": {"top": 0.25, "bottom": 0.82, "left": 0.44, "right": 0.84},
  "confidence": 0.92}},
 "usage": {"input_tokens": 290, "output_tokens": 21}}
```

### 3.2 它**不**返回什么（这是关键）

- **不返回结构化元素列表。** 没有 "elements[]" 输出；元素表是**输入**，Jev 的答案是
  表里某个键（索引 / 选项名）。
- **不返回元素 ID、选择器、坐标**（除 Box）。`jev-ultrafast` 的执行器把选中索引映射回
  真实 DOM 节点，并在点击前重新解析几何与遮挡：
  "Every executed target is resolved from an observed node… **Model output never becomes
  selectors, coordinates, shell commands, or executable JavaScript.**"（README）
- **不返回自然语言。** 官方 jaggedness 第 9 条：`jev-1.13` is not trained to generate text
  （[官方](https://docs.typesafe.ai/model-jaggedness/jev-1.13)）。Anchor 文案同调：
  "Jev returns a choice, never text."（[Anchor](https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev)）

### 3.3 所以它作为"布局表示"可用吗？

**分层回答：**

| 需求 | Jev 能提供 | 缺口 |
|---|---|---|
| 单元素 bbox | ✅ `box` 原语（归一化四边） | 一题一框；需图；`jev-1.14`；预览态；无 "not found" |
| N 个元素的 bbox | ⚠️ 只能发 N 道 box 题（可同请求并行） | 无批量接口；成本/延迟随 N 线性；图像分辨率上限压住小控件 |
| 元素顺序/分组/容器/栅格/对齐 | ❌ 无任何原语 | 全部要由提取器或代码给出 |
| 元素 role/label/state | ⚠️ 只能"在给定候选里选"或"按给定档位打分" | 不能凭空产出你没给的标签词 |

**结论：Jev 的输出不是布局表示，是"对布局表示的裁决"。几何必须来自别处（或 box 逐目标
打点），结构必须由代码组装。**

---

## 4. 布局识别：它真的"解析版面"吗？

### 4.1 官方对 Box 的定位就是"单目标定位"

> "**One box per question.** Box is designed to find one target, and every answer is a single
> box. To find several different things, ask one Box question for each in the same request."
> — [官方 Box 页](https://docs.typesafe.ai/primitives/box)

官方示例目标全是**自然物体/文档**（咖啡杯、扫描件签名、门口的人），**没有任何 UI 元素
或界面版面的示例，也没有 UI 上的精度数据**（**[未验证]**：Box 在 UI 截图上的可用性）。

同页还给了三条"粗糙几何"的工程事实，正好说明它不是版面解析器：

- **分辨率陷阱**："A target a few dozen pixels wide in a very large image can be missed."
  必须先粗后细：先用 Choice 在**图像切片**里选区域，再在切片上问 box，最后把坐标换算回
  全图（即 **tiling + 二分**，而不是一次解析）。
- **Box 没有 "not found"**：目标不存在也返回四条边，必须配一道 Noul 兜底。
- **框裁切要靠 Noul 逐边检查**：36 张测试裁剪图上，四边问题抓到 30 处裁切，单问
  "整体是否在内" 只抓到 26。

### 4.2 唯一接近"结构恢复"的一手证据，在文本域而非像素域

官方 cookbook *Structure recovery*（把丢失格式的纯文本还原成 Markdown）是 Jev 做"结构化"
最完整的公开示范，值得逐条对照 UI：

- **边界由代码切**：行切分、空行、`- ` / `1.` / `#` 标记全部在代码里读，不送模型。
- **Jev 只判"这两行是不是被折断的一句"**（Noul，逐对），再由代码合并成 block：
  `28 lines → 17 blocks`，merge 阈值随上一行标点分档（0.2 / 0.5）。
- **Jev 只判"这个 block 是什么类型"**（Choice：heading / paragraph / list_item / quote /
  code / callout），外加 heading level、step order、callout kind 的伴随问题。
- **分组/编号/渲染全在代码**：连续 list_item 合成一个列表，`mean(step) ≥ 0.5` 才编号——
  官方明说这是 "a group-level decision **no single question asked directly**"。
- 代价：2 次请求、10,211 tokens、0.8s、$0.0003~0.0015（[官方 cookbook](https://docs.typesafe.ai/cookbooks/autoformat)）。

**这段是本文最重要的类比证据**：Jev 在"结构恢复"里承担的是**逐单元的标签判定 + 概率**，
而"单元边界、层级、分组、顺序、渲染"这些真正的 layout 决策，全部留在代码里。UI 上同理。

### 4.3 反向证据（集成方视角）

- `jev-ultrafast` 的元素表**只有 index / role / label / value / state / operations**，
  **没有 bbox**；几何在执行前由 `browser.py` 现场从 DOM 节点解析，并做"页面是否变了 /
  是否被遮挡"的校验（README）。
- `jev-mobile` 的 `bounds` 来自 A11y 树，不来自 Jev；Jev 只回答 `operation` 与
  `click_target`（索引）。
- Anchor 的 Jev agent **明确不支持** `detect_elements`（连同 `output_schema`、
  `human_intervention`、`use_os_control` 一起列为"not available yet"）——也就是说，
  官方托管浏览器里**拿不到元素检测结果**（[Anchor](https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev)）。

### 4.4 §4 判定

**Jev 不做版面解析。** 它能做的是：在**已切好的候选**上判定归属/类型/优先级（语义），
以及在**图像**上做**单目标**粗定位（几何，预览态）。区域、容器、阅读顺序、对齐、分组、
栅格——一手来源里没有任何一个原语或示例支持（**[未验证]**：不存在"只是没写进文档"的可能，
但文档级证据是全负的）。

---

## 5. 语义识别：角色、标签、可供性

### 5.1 能做（有据）

| 能力 | 证据 |
|---|---|
| 在给定候选元素里选"该动哪个" | `operation` + 每操作一个 `*_target` 的 Choice（[model.py](https://github.com/browser-use/jev-ultrafast/blob/main/jev_ultrafast/model.py)） |
| 判断"目标是否达成" | 独立 Noul（`jev-mobile` 在同请求里评"当前目标是否已达成"，≥0.5 收工）；`jev-browser` 用 goal Noul + stuck Noul，阈值 0.85 |
| 判断"是不是卡住了" | stuck Noul（同上） |
| 给候选打类型/档位 | Choice ≤ **255** 个选项；Score ≤ 10 档（[官方 Choice](https://docs.typesafe.ai/primitives/choice)） |
| 消歧"这句自然语言指令指什么" | Figma 插件把"在这里加一个圆"拆成 3 道 Choice（做什么 / 什么形状 / 放哪里），"这里"的坐标来自拖放定位点，**不由模型猜**（[README](https://github.com/voidning/jev-figma-design-plugin)） |
| 提供可用的弃权信号 | `probabilities` + `confidence`；官方 pattern 就是 confidence-gated routing（[官方](https://docs.typesafe.ai/primitives)） |

### 5.2 不能做（有据）

- **不能描述/字幕化一个界面**（不生成文本）。
- **不能"回答关于屏幕的问题"**——除非答案被预先框成 Choice/Noul 的选项。Anchor 明说：
  "the agent… **does not summarize, extract, or answer questions**, and the result is the final
  page state. Use another agent when the deliverable is generated content."
- **不能按名字引用模型没见过的元素**："the price field" 只有在候选表里出现该标签时才能被选中；
  它无法输出一个表外的名字。
- **不能可靠计数/做数值比较**（"does not count reliably"；颜色用 hex/RGB 会退化，用英文色名
  更准）（[官方 jaggedness](https://docs.typesafe.ai/model-jaggedness/jev-1.13)）。

### 5.3 谁在生产语义？

一手证据齐刷刷指向**外部**：`role/label` 来自 DOM 或 a11y 树；`jev-ultrafast` 的
`TARGET` 提示词只说 "Choose only an offered element index"
（[questions.py](https://github.com/browser-use/jev-ultrafast/blob/main/jev_ultrafast/questions.py)）。
Milind 的本地方案更极端：`label` 来自**端上 OCR**，Jev 只做概率分配。

### 5.4 页面级（整屏）语义：能判、不能述

「页面语义识别」有两种截然不同的问法，Jev 对它们的可行性相反：

| 页面级问题 | Jev | 说明 |
|---|---|---|
| 这屏属于哪类页面（给定 taxonomy） | ✅ Choice | 候选类目必须由你给全（含 `unknown` / `none of the above`）；答案只会回到你给的类目，不产生新词 |
| 是否有弹窗/覆盖层、属于哪类弹窗 | ✅ Noul + Choice | 与元素级同构，判据仍是屏幕上的文本；没有语义标签的纯图形弹窗判不了 |
| 是否到达目标页 / 是否卡住 / 进度档位 | ✅ Noul / Score | Anchor、`jev-browser`、`jev-mobile` 的 goal / stuck Noul 就是这一类，属最强项 |
| 这屏讲了什么（摘要、分区、描述） | ❌ | 不生成文本 |
| 开放问答（"为什么开关没生效"） | ❌ | 只能答被框成选项的问题 |
| 计数（"还剩几个开关没开"） | ❌ | 官方：does not count reliably；计数必须在代码里 |
| 跨页归纳（"前面点了 3 次都失败"） | ❌ 弱 | 官方失败模式 `Indirection`；历史必须先由代码压成一句结论再送 |

**页面级输入的构造（page digest）是新的工程面，三条硬规则：**

1. 只送判断所需：route 候选、可见文本前 N 行（去重）、元素类型直方图、白名单命中情况。
   整棵 Hierarchy 塞进去正撞官方失败模式 #5（大而杂的 state 掉分）。
2. 与元素表共用同一 capture/cycle 与同一 `state` 结构 —— 官方所有问题在**同一请求内并行**
   评估，页面级与元素级可以一次问完，加问题基本不加延迟。
3. digest 构造规则必须版本化：同一帧两次判断不可比，往往不是模型变了，而是 digest 变了。

最小页面级问题集（一次请求）：

```jsonc
{
  "model": "jev-1.13.0",
  "state": {
    "route_candidates": ["android.settings", "android.settings.security"],
    "title": "Security & privacy",
    "visible_text": ["..."],
    "element_histogram": {"button": 6, "switch": 4, "text": 14},
    "popup_resource_ids_hit": []
  },
  "questions": {
    "page_kind": {"type": "choice", "instructions": "Which known page is this?",
                  "criteria": {"android.settings": "...", "android.settings.security": "...", "none": "..."}},
    "popup":     {"type": "noul", "instructions": "Is an overlay covering the page?"},
    "goal":      {"type": "noul", "instructions": "Has the target page been reached?"},
    "stuck":     {"type": "noul", "instructions": "Is progress blocked with no visible way forward?"},
    "progress":  {"type": "score", "instructions": "How far through this page's items are we?",
                  "criteria": ["nothing done", "some", "most", "all"]}
  }
}
```

> **§5 判定：** Jev 是**语义仲裁器/判官**，不是语义**生产者**。它能显著提升"在已有候选里
> 选对、判断是否完成、判断是否卡住"的准确率与可解释性，但不解决"候选从哪来、每个元素叫
> 什么、角色是什么"。

---

## 6. 性能与 "ultrafast / System One" 的机制

### 6.1 官方口径

- "**Frontier intelligence at real-time speeds (150ms)**"（[官方 use-case map](https://docs.typesafe.ai/concepts/use-case-map)）。
- 机制（官方）：不做自回归解码；返回类型化答案；**一个请求内所有问题并行、互不影响**——
  "Adding questions barely changes the response time"（[官方](https://docs.typesafe.ai/primitives)）。
  官方 cookbook 实测：13 道题合并成 1 次调用 vs 13 次调用，"11.5x cheaper and 9.6x faster"；
  另一处写作 "12.2x cheaper and 10.0x faster"（两处数字不一致，**同一文档族内自述差异**，
  见 [primitives](https://docs.typesafe.ai/primitives) 与 [llms.txt 条目](https://docs.typesafe.ai/llms.txt)）。
- 训练侧：**RLCD**（reinforcement learning for calibrated decisions），目标是概率校准，
  而非人类偏好（[官方 primer](https://docs.typesafe.ai/introduction/machine-learning-primer)）。

### 6.2 媒体口径（**不是**一手）

- "最快提速近 200 倍、成本最低约 1/400"——新华网转述 TypeSafe 官方（[新华网](https://app.xinhuanet.com/news/article.html?articleId=202609216ad2782938944456924e1016dd3b84fd)）。
- "响应约 70–500ms"、"$0.042/Mtok、输出免费"——阿里云社区长文（同一作者稿件亦发于
  [腾讯云](https://cloud.tencent.cn/developer/article/2749332)）。属**媒体转述**，未见官方 SLA。

### 6.3 社区实测（各自单任务，非基准）

| 场景 | 数字 | 来源 |
|---|---|---|
| macOS 端上分割+OCR → Jev 选元素 | **~90 ms / 决策** | [Milind S](https://x.com/milindlabs/status/2100631847155994852) |
| Google Flights 单次决策 | 164 ms；整任务 **7.1 s**、$0.0039 | [阿里云转述 Browser Use 创始人](https://developer.aliyun.com/article/1765567) |
| 跑酷游戏单帧决策 | 192 / 233 / 242 ms | 同上 |
| X 时间线过滤扩展 | 中位检测 382 ms、最近往返 254 ms | 同上 |
| Android A11y 快路径 | 决策均值 **540 ms**（含设备读取；`2–3s/步` 的演示含设备往返） | [jev-mobile README](https://github.com/xinwang-nwpu/jev-mobile) |

### 6.4 官方/集成方对比数据（注意样本量）

- Anchor：同一 Wikipedia 任务，`jev` **7.6 s / 1 次文本模型调用** vs `browser-use` 默认
  **34.9 s / 6 次**（"Results vary with the site and the task"，单任务）。
- `jev-ultrafast`：6 次交替运行，两者均 3/3 通过；**中位任务时间 9.450 s → 7.092 s（−25%）**，
  浏览器协议调用 **1,092 → 101**。作者自己限定："This is three repeats of one task on one
  browser profile, not a general reliability benchmark."（README）

### 6.5 "为什么快"的可验证解释

1. **每步只一次网络往返**：操作 + 所有目标头 + 完成判定打进同一请求
   （`jev-ultrafast` / `jev-mobile` / `jev-browser` 三份 README 一致）。
2. **不生成 token**：输出是选项 + 分布，长度恒定、极小（示例 `output_tokens` 12–73）。
3. **不重试/不解析自由文本**：客户端做 schema 校验（`validate_choice` 检查
   `choice ∈ ids`、概率和≈1、选择项为最大概率）。

**APUS 的机制推测**（"跳过自回归解码、单 Token Logits 直接打分、KV-Cache 广播"）**只见于
媒体转述的 APUS 自述**，TypeSafe 文档从未这样描述（**[未验证]**）。

---

## 7. 集成形态（HTTP / SDK / 平台）

### 7.1 接口

```
POST https://api.typesafe.ai/v1/systemone
Authorization: Bearer <TYPESAFE_API_KEY>
Content-Type: application/json          # 或 multipart/form-data（图片推荐）
```

- 请求字段：`model`、`state`、`questions`（≥1）、可选 `assets`。
- 头：`TypeSafe-Danger-Zone-Images: true`（图片/box）、`TypeSafe-Danger-Zone-Long-Context: true`（262k）。
- 错误：400（图片未开/资源非法/超上下文）、413（超限）、422（schema）、429（限流，SDK 默认重试并遵守 `retry-after`）。
- 模型发现：`GET /v1/models`；alias 会移动 → **响应里的 `model` 字段回报实际版本**，用于日志。
- 限流：按组织，控制台查（[models](https://docs.typesafe.ai/models)、[errors](https://docs.typesafe.ai/api/errors)）。
- SDK：`pip install typesafe-sdk`（Py≥3.10）、`npm i @typesafe-ai/sdk`（Node≥20）。

### 7.2 平台覆盖（靠"谁给它做感知"）

| 平台 | 感知来源 | 代表实现 |
|---|---|---|
| Web/浏览器 | DOM（不是 a11y 树）+ 可见文本 | [jev-ultrafast](https://github.com/browser-use/jev-ultrafast)、[jev-browser](https://github.com/jkudish/jev-browser) |
| 托管云浏览器 | agent 侧构建控件清单 | [Anchor `agent='jev'`](https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev) |
| Android | A11y 树（Portal/uiautomator）+ ADB 执行 | [jev-mobile](https://github.com/xinwang-nwpu/jev-mobile)、[mobile-jev-ultrafast](https://github.com/cnhuye/mobile-jev-ultrafast) |
| macOS 桌面 | 本地 CoreML 分割 + 端上 OCR | [Milind S](https://x.com/milindlabs/status/2100631847155994852) |
| Figma | 插件上下文 + 拖放定位点 | [jev-figma-design-plugin](https://github.com/voidning/jev-figma-design-plugin) |

**桌面 OS 没有官方支持**：Anchor 把 `use_os_control` 列为 Jev agent 尚不支持；
Windows/Linux 桌面只能自行接 UIA/A11y/OCR 后走文本协议（**[未验证]**：无一手 Windows 实现）。

### 7.3 认证与密钥

`TYPESAFE_API_KEY`（Bearer）。Anchor 托管路径下 Jev 由 Anchor 代付/代管，"Not affected by
`provider`, `model`, or BYOM"——只有 `TYPE_TEXT` 的打字模型用你的 key/BYOM（[Anchor](https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev)）。

### 7.4 第三方网关（社区一手）

`jev-browser` 支持 TypeSafe 直连 / OpenRouter / Cloudflare Workers AI / Vercel AI Gateway
四种传输，`JEV_PROVIDER` 可强制；它同时说明了**协议兼容性价值**：只要能回
`{answers, usage, model}`，就能替换成自托管/第三方 jev-style 模型
（[jev-browser](https://github.com/jkudish/jev-browser)、[mobile-jev-ultrafast](https://github.com/cnhuye/mobile-jev-ultrafast)）。

---

## 8. 已知限制与失败模式

### 8.1 官方承认的（`jev-1.13` jaggedness，9 条）

字面理解（literal reading）、数学/计数、日期比较、间接推理、**大 state 干扰（context rot）**、
对抗性内容可被引导、instructions 与 criteria 冲突、**Choice 选项顺序偏好（偏向第一个）**、
不会生成文本（[官方](https://docs.typesafe.ai/model-jaggedness/jev-1.13)）。
另：CJK 可用但不如英文，官方建议先自测并用 confidence 做路由（[models](https://docs.typesafe.ai/models)）。

### 8.2 GUI 场景的结构性限制

| 限制 | 后果 | 来源 |
|---|---|---|
| Choice 上限 **255** 个选项 | 元素表必须截断；密集页面上目标可能压根没被提供 | [Choice](https://docs.typesafe.ai/primitives/choice)、[jev-browser](https://github.com/jkudish/jev-browser)（240/步） |
| 集成方的候选/动作上限 | `jev-ultrafast` 每次观察最多 **250 个候选**、整任务上限 60 个动作 / 120 次决策请求；截断是作者承认的真实失败模式 | [直读 README 与 performance.md](https://github.com/browser-use/jev-ultrafast) |
| 图片单边 ≤1568 px | 1080p/4K 截图必须缩，小控件丢细节；官方建议切片二次问 | [Images](https://docs.typesafe.ai/images)、[Box](https://docs.typesafe.ai/primitives/box) |
| Box 无 "not found" | 目标不存在也返回框，必须配 Noul | 同上 |
| 需要"真实控件" | canvas / 地图 / 游戏 / 图形验证码"offer nothing to pick from"，官方建议改用截图型 agent | [Anchor](https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev) |
| shadow DOM / iframe / canvas / 上传 / 弹窗标签 / 嵌套滚动 / 任意键盘控件 | MVP 之外 | [jev-ultrafast](https://github.com/browser-use/jev-ultrafast) |
| 长程任务弱 | 二手转述：换成更长程任务集后，"纯 Jev agent 20 题对 1 题，BrowserCode+Luna 17 题" | [阿里云转述](https://developer.aliyun.com/article/1765567)（**[未验证]**，无原始基准） |
| 完成判定 ≠ 真完成 | "A `DONE` choice still requires independent outcome verification"；`jev-mobile` 也要独立 Verifier 复核 | README 同上 |

### 8.3 确定性与可重放性：**不支持**

- 没有公开的 seed/temperature/采样参数（`/v1/systemone` schema 无相关字段）。
- 官方明示 alias 会移动、**"its answers can change without any change to your code"**；
  同时提醒"若你针对某版本调过 confidence 阈值，请直接发版本 ID"
  （[models](https://docs.typesafe.ai/models)）。
- 官方 cookbook 的做法是**把每次响应缓存进 `json_cache.json`**，才能"replays the published
  numbers without calling the API"（[autoformat](https://docs.typesafe.ai/cookbooks/autoformat)）——
  这反证了调用本身不保证逐字节可重放。
- 官方还承认选项顺序会左右答案 → 同一输入换序可能换结论。

→ **工程含义：要可重放，必须（a）钉版本 ID、（b）缓存响应/记录 trace、（c）把阈值决策留在代码。**

---

## 9. 与替代方案对比（只写有来源的）

| 方案 | 输入 | 输出（含几何？） | 每元素语义？ | 结构/版面？ | 权重/许可 | 延迟/成本信号 | 来源 |
|---|---|---|---|---|---|---|---|
| **Jev（Choice/Score/Noul）** | 文本/JSON 候选表（可选图片） | 选项 + 概率 + confidence；**无 bbox** | 只能判定给定候选的类型/优先级 | **无** | 闭源托管 API；$0.042/Mtok 输入 | 官方称 ~150ms 级；社区 ~90–540ms | [官方](https://docs.typesafe.ai/primitives)、[定价](https://docs.typesafe.ai/models) |
| **Jev + Box** | 图片（280–1568px） | **归一化 bbox，一题一框** | 需另问 Choice | 只能靠 tiling 逼近 | 同上；"Dreamer-ready" | 预览态，无 UI 精度数据 | [Box](https://docs.typesafe.ai/primitives/box) |
| **OmniParser** | 截图 | 结构化元素：**可交互区域 + 每元素 caption** | ✅（专用 caption 模型） | 元素级，非容器/阅读序 | 开源（微软） | 需本地检测+caption 推理 | [论文](https://arxiv.org/abs/2408.00203)、[repo](https://github.com/microsoft/OmniParser) |
| **UI-TARS-1.5 / 2** | 截图 | 动作 + 坐标（`click(start_box=...)`） | 有 Thought 推理；**坐标是绝对像素** | 端到端隐式，不显式输出结构 | 7B 权重开源（HF） | 需较大算力；OSWorld 42.5 / ScreenSpot-V2 94.2 | [repo](https://github.com/bytedance/UI-TARS)、[论文](https://arxiv.org/abs/2501.12326) |
| **SeeClick** | **仅截图** | 坐标/元素定位（grounding） | 弱（预训练 grounding，非元素级描述） | 无 | 开源（模型/数据/代码） | 论文级，无产品延迟 | [论文](https://arxiv.org/abs/2401.10935) |
| **DOM/A11y 启发式** | DOM / a11y 树 | **精确 bbox + 角色/名称/状态** | ✅（但语义弱、命名脏） | 结构天然有（树） | 无模型 | 近零延迟/成本 | [jev-browser](https://github.com/jkudish/jev-browser)（"a11y trees under-report inputs"）、[jev-mobile](https://github.com/xinwang-nwpu/jev-mobile) |
| **Claude computer use** | 截图 + 鼠标键盘工具 | 坐标动作（客户端工具集） | 模型自述/推理 | 无显式结构输出 | 闭源 API | 闭源，按 token | [官方文档](https://platform.claude.com/docs/en/agents-and-tools/tool-use/computer-use-tool) |
| **GPT-4V/4o 直用** | 截图 | 动作/描述 | 有，但 grounding 弱 | 无 | 闭源 | — | OmniParser 论文：其动机即 GPT-4V 缺乏可靠 screen parsing，加 OmniParser 后 ScreenSpot 显著提升 |

**读表要点：**

1. **几何 + 结构 + 每元素语义**这一格，**OmniParser 是唯一天然三者齐全的开源件**（检测区域 +
   caption），但它不给容器层级/阅读顺序。
2. **UI-TARS / SeeClick** 是"端到端出坐标"，对你"要元素表"的需求是错位的：它们不做元素清单，
   只做 grounding/动作。
3. **DOM/A11y** 几何与结构最准，语义最脏（且 a11y 树会漏 input）。
4. **Jev 的位置是"判官"**：它不与其他三者竞争"感知"，它竞争的是"在感知之上做便宜、快、
   有概率的决策"。这也是所有落地项目的实际拼法。

---

## 10. 对"既要元素几何+结构、又要元素语义"的 UI 自动化引擎意味着什么

### 10.1 能/不能清单

| 你的需求 | Jev 能否独立满足 | 必须配对谁 |
|---|---|---|
| (a1) 每个元素的 bbox | ❌（文本路径无几何；box 一题一框且预览态） | DOM/A11y 取框，或视觉检测器（OmniParser/自训分割），或 box 逐目标打点 + 校验 |
| (a2) 容器/区域/阅读顺序/分组/对齐 | ❌ | 提取器的树结构 + 代码的分组规则（官方 cookbook 的 `merge`/`group` 就是范式） |
| (b1) 每个元素的 role/label | ❌ 生产不了，只能判定 | DOM/a11y 属性、OCR 文本、caption 模型 |
| (b2) 元素的可供性/该做什么动作 | ✅ 强项（在候选动作里选，给概率） | 候选动作集必须由代码枚举（含可用性/禁用态） |
| (b3) 目标是否达成 / 是否卡住 | ✅ 强项（Noul） | 需要独立校验（官方也承认 DONE 需外部验证） |
| (b4) 低置信时弃权/升级 | ✅ 强项（probabilities + confidence） | 阈值在代码，按版本校准 |
| (b5) 界面描述/字幕/问答 | ❌ | 生成模型 |

### 10.2 明确裁决

> **Jev 不能单独同时充当布局识别器与语义识别器。**
>
> - 作为**布局识别器**：不合格。它没有区域/容器/顺序/分组原语；唯一的几何输出 `box` 是
>   "一题一框 + 预览态 + 分辨率受限"，适合"在已知大致区域里精修一个目标"，不适合"一次
>   产出界面结构"。
> - 作为**语义识别器**：它是**判官而非标注器**。它能高质量地回答"这些候选里哪个是提交按钮 /
>   这个块是标题吗 / 目标达成了吗 / 卡住了吗"，但元素的角色与标签必须由 DOM、A11y、OCR 或
>   caption 模型先生产出来。
>
> **正确拼法（也是所有一手落地项目的拼法）：**
> `感知/提取器（DOM | A11y | 视觉检测+OCR） → 候选表（id, role, label, state, bbox） →
> Jev 判定（角色归一化 / 分组归属 / 目标选择 / 完成 / 卡住 / 弃权） → 代码拥有几何、顺序、
> 阈值、重放`。
> 其中**几何永远不经过 Jev**（除 box 精修路径），**语义的"词汇表"永远由提取器定义**。

### 10.3 对本仓库的落点提示（分析，不构成决策）

- 若把 Jev 引进 UniClaw：它落在**决策/仲裁缝**，不是元素生产者。`UI Element Perception`
  的 producer 责任不变——Jev 只消费 Fast/提取器产出的元素表；它能提供的仅是
  `Semantic Perception` 那一层的判别型 proposal（见 §10.4）。
- Jev 的**真价值**是三点工程性质，而不是"多一个视觉模型"：
  1. **一个往返/一步**（操作+目标+完成判定打包）；
  2. **概率 + confidence 天然可弃权**（比 LLM 的"自信胡说"更适合无人值守）；
  3. **输出被约束在候选集内**（结构上不可能给出不存在的元素 → 消除一类失败模式，
     但**不等于答案正确**）。
- 若目标包含**桌面 OS**：Jev 没有 OS 级控制/元素检测（Anchor 明确 not available），必须自建
  UIA/A11y/OCR 提取层，Jev 只做文本判定。
- 若目标是**一次拿到版面结构**：不要指望 Jev；走 OmniParser 类解析器或 DOM 树，Jev 只用于
  "结构归属的打标/复核"（cookbook 的 block-typing 模式可以几乎照搬）。

### 10.4 接在本仓哪条缝上（本仓约束下的落点，非决策）

本仓已有两条感知协议（`docs/design/runtime-capability-integration-seams-v0.1.md` §4.0.2）：
`UI Element Perception`（capture-local occurrence / role / bounds / 关联）与
`Semantic Perception`（语义 claim + `PerceptionAssessment`）。按 §3–§5 的结论：

| 本仓既有物 | Jev 的位置 | 结论 |
|---|---|---|
| `UI Element Perception` producer | 无枚举、无结构、无稳定几何 | **不能承担**；只在 Fast 产出元素表**之后**出现 |
| `Semantic Perception` proposal | 在既有元素表上做有界判别（选哪个 / 是否 / 打分） | **可作一种 realization**，输出非权威 claim + 概率 |
| `PerceptionAssessment.ProviderConfidence` | 概率与 confidence 天然落这里 | 只能作校准输入/诊断，**不得**成为 Admission / Assurance / Effect Gate |
| `UniPerception`（Fast YOLO+OCR → Slow Text） | 判别 vs 生成，不是同类 | 可作 Slow 之前的一层低成本判别，或与 Slow A/B 对照；不替换 Fast |
| `FastPerception` 的「空检测 ≠ 无元素」纪律 | Jev 答「都不匹配」也只是低概率判断 | 必须保留 `Unknown` / `Unaligned`，不能用空结论冒充负面证据 |
| ADR-0020（确定性锚 = 响应 JSON 原文） | 响应 JSON 可作 derived artifact 落盘 | replay 用**存下来的响应**，不重跑推理；托管 API 的 alias 漂移由身份如实报告 |
| ADR-0021（provider 进程外黑盒、变体可选不可携带） | 只能作预声明变体 | `model` 版本 + 元素表构造规则 + 问题集版本必须进 lineage；请求不得携带 prompt 内容 |

对 Android 模拟器场景，最贴近的现成范式是 `jev-mobile`：A11y 树 → 索引化元素表
（`className / text / contentDescription / resourceId / bounds / checked / selected`）
→ 每步一次 Jev 决策（`operation` + `click_target` + 完成/卡住 Noul）→ ADB 执行，
不截屏、几何来自 A11y 树的 `bounds`。这与本仓「Fast 出元素表、几何来自上游」的纪律同构。

**若要做一次『是否值得继续』的对照（不授权实现）**：

1. 用已捕获的 `capture PNG + VisionService 响应 JSON` 固定 corpus，元素表字段与排序规则
   版本化（官方承认选项顺序会影响答案）。
2. 选 3 类有 buyer 的判别：同名按钮消歧、弹窗处置（关闭/忽略/继续）、目标是否在表中（Noul）。
3. A/B：现有 Slow Text（生成式）vs Jev（Choice/Noul），同帧同表；指标 = 判别准确率 /
   p50、p95 / confidence 分桶校准 / 弃权率。
4. 门控：`confidence < τ` 交给 Slow Text 或保守路径，τ 用留出集定；另测选项换序一致性
   与屏幕文本注入用例。
5. 判据：判别准确率显著优于现有规则/Slow，且 p95 不劣化交互预算，才进入下一步；否则
   `NO_CHANGE`。

**不要做**：不要让 Jev 产坐标（几何只来自上游元素表或 Box 精修路径）、不要把它接成新的
Evidence/Binding 授权方、不要用自托管 Reflex 的数字去替代官方 API 的验收基线。

### 10.5 页面语义在本仓已分三层，可碰性不同

「页面语义识别」在本仓不是空白，而是已经由**确定性规则**占了三层；Jev 只能落在规则失效的
边缘，且每层的授权不同：

| 层 | 现实现（事实） | Jev 可否介入 |
|---|---|---|
| 屏幕身份 `ui.screen` | `ProductAssociationStrategy.ScreenIdentitySubject`：字节签名逐字节相等才 `Matched`，直接决定 container identity | **绝不能**。身份语义；模型参与即破坏 association 与 replay 的可判定性 |
| 路由指纹 `ui.screen.route` | `DeriveRouteKey`（`settings_traversal` 侧的多信号：`homepage_title` / `title` / up 按键；无标题回退 `android.settings`）；普通世界证据，不参与 association | 只可作**校准/诊断**（例如"该 key 是否仍是这屏"），不得改写 key；改 key 会让 coverage 计数与历史 corpus 失配 |
| 弹窗声明 `ui.overlay.popup` | `present` / `absent` typed claim，来自 `PopupResourceIds` 白名单与 `ObstacleTargets`（CANCEL/DISMISS/button1/button2） | **可以补充**：白名单未命中的弹窗、弹窗类别（权限/更新/广告）、"是否还有第二层覆盖"。仍是普通 evidence，不参与 association、不触发清障动作 |
| Coverage 判定（root seen / second-level pages / steps verified） | `SettingsCoverageLedger` 规则账本 | **不得**。这是 Product truth 的判据；Jev 只能产出诊断性 finding |
| "卡住 / 无进展"（AGT-019 进行中） | 规则 + 上下文（近期失败、重复动作） | 可作为**一条**非权威输入（stuck Noul），不能替代既有 fail-closed 决策 |

两条本仓特有的纪律，页面级比元素级更容易违反：

1. **未知页不是"猜出来"的。** `DeriveRouteKey` 失败即 `android.settings`（未知页语义），
   `SettingsCoverageDirector` 的处理是 observe again / resample，不是"让模型认一下"。
   Jev 的 `page_kind` 答案只能作为声明，不能消解 `UnknownRouteIdentity` 这条既有语义。
2. **评估集必须只取规则边缘样本。** 拿 `DeriveRouteKey` 的结果当 ground truth 去评 Jev，
   等于用规则给自己的复制品打分；有意义的样本是：`UnknownRouteIdentity` 帧、白名单未命中
   的弹窗帧、no-progress 的边界帧。这些帧的数量决定这件事值不值得做。

### 10.6 过度分割（一行菜单被切成两项）：Jev 能补救，但只该补最后一环

用户场景：菜单行是「大标题 + 小标题」，YOLO/OCR 把一个菜单项识别成两个元素，问 Jev 能否纠正。

先看本仓的真实产生路径（`platforms/perception/uniclaw_perception/fusion/engine.py` 事实）：

1. `fuse_evidence` 为每个 YOLO detection 建一个 candidate，并吸收**可垂直归属**的 OCR token
   —— `_vertically_attributable` 只排除"完全落在框上方更高行"的 token，**框下方的 token 保留**
   作从属证据；`primary_line_text` 取最上面一行作为 `text`；
2. 匹配窗口 = 屏幕对角 × `maxOcrDistanceRatio`（默认 `0.055`），另受水平重叠守卫
   （`_MISATTRIB_H_OVERLAP_FRAC`）约束；
3. 没被任何 detection 吸收的 token，在 `promoteUnmatchedOcr: true`（默认）下**各自提升为
   `text_block` + `ocr_only`**，confidence ×0.75。

所以"一个菜单项变两个"只有两条路径：**YOLO 对同一行出了两个框**，或**副标题 token 落在匹配
窗口/重叠守卫之外被 promote**。这是上游几何归属的边界条件，不是语义错误 —— 而"边界条件"
意味着它首先该由几何规则与参数解决。

三层处置，顺序不能倒：

| 顺序 | 手段 | 成本 | 覆盖 |
|---|---|---|---|
| 0 | 有 XML/Hierarchy：`android:id/title` + `android:id/summary`（或父子节点）直接给关系 | 零 | 有 dump 的原生页面全覆盖；纯视觉路径不适用 |
| 1 | 确定性合并规则：同左边界（高 h_overlap）、垂直间距 < k×行高、一方 `ocr_only`/不可点、token 高度更小（字号） | 零 | 多数 Android 原生行 |
| 2 | 调参对照：`promote-off` 变体 / `maxOcrDistanceRatio` / 重叠守卫，量出 promote 制造了多少多余元素 | 低（变体现成） | 直接回答"是不是 promote 的锅" |
| 3 | Jev 逐对判定 | 中（每帧 3–8 对，一次请求并行） | 只剩规则犹豫的对 |

Jev 的正当问法是**逐对**，不是整屏找关系：

```jsonc
"pair_3": {"type": "noul",
           "instructions": "Are A and B two lines of ONE menu row (a title and its summary), or two separate rows a user taps independently?"}
```

要让这一问有意义，`state` 必须带上**它看不到但你能算的信号**，否则它只能从文本猜：

- `same_left_edge` / `h_overlap_ratio`（同列对齐）；
- `vertical_gap_px / row_height_px`（间距与行高之比）；
- `a.kind` 与 `b.kind`（`menu_item` vs `ocr_only` 的 `text_block`）、`b.tappable = false`；
- 两侧 token 高度比（副标题通常更小）；
- 同一容器内的兄弟行数量（若可得）。

**合并由代码做，不交给模型**：bounds 取并集、`role` 取可点击者、标题进 `text`、副标题进
description/child；被合并的两个 occurrence id、本批 `criteria` 版本、`model` 版本一起进
lineage。否则"元素数变化"不可解释，replay 也对不上（ADR-0020/0021）。

三条必须先承认的边界：

1. **纯文本歧义时判不了。**「Wi-Fi」+「已连接」是状态摘要；「Wi-Fi」+「Wi-Fi 偏好设置」是子项
   候选。只看两段文字无法区分，必须有几何/字号/可点性。信号给少了，它就只能猜 —— 而且会猜得
   很有信心（官方承认 `confidence` 不保证单条正确）。
2. **合并 vs 父子是产品语义，不是模型语义。** 副标题本身可点（有的 summary 是链接）时，合并
   会丢掉一个可操作目标；此时应建父子关系而不是合并，问题也要拆成两问（是否同一行 / 谁可点）。
3. **它会放大相反方向的错误。** 两个真独立项（「Wi-Fi」与「Wi-Fi 偏好设置」）语义上很像，模型
   倾向合并。因此样本必须「真对 / 假对各半」，且低置信一律走保守：不合并、保留 `ocr_only`
   riskFlag，把不确定性交下游。

验收指标不是"合并准确率"，而是**下游 grounding 是否变好**：原本点中副标题（错目标）或点不中的
比例是否下降；同时盯元素总数与 coverage 计数是否被破坏 —— 过并和漏并都会减少可点元素。

> 一句话：**Jev 在这类问题上能生效，但它是"规则犹豫对的裁决者"，不是"过度分割的修复者"；
> 有 XML 时根本不该用它。**

### 10.7 「操作前语义 + 动作 → 推断操作后语义」：在期望侧成立，在观察侧违法

这个想法的合理内核是对的：单帧识别有歧义时，动作上下文是有效消歧信号。但**推断出来的
post-state 不能当观测用**，因为本仓有一条已冻结的不变量：

> ADR-0012：`ControlIntent / 期望的 transition → MUST NOT 进入 WorldModel reconciliation`；
> expectation 不得修改 observation interpretation；TransitionContext 只能作 candidate prior，
> `MUST NOT` 单独 establish `Matched` / `New`，`MUST NOT` 创建或替换 `ContainerIdentity`。

把 Jev 的推断结果写成"操作后页面语义"喂给 P2，就是这个不变量禁止的形状。四种可行的问法，
授权各不相同：

| 问法 | 输入 | 输出 | 落点 | 判定 |
|---|---|---|---|---|
| **A. 推断 post-state**（把推断当观察） | pre digest + action | post 语义 | 观察/证据侧 | **不允许**（ADR-0012 不变量 16） |
| **B. 提 expected effect**（期望谓词） | pre digest + action + 封闭谓词词汇表 | `StateBecomes` / `ChangePredicate{ContentChanged, RouteStable}` 实例 | 期望侧（ADR-0049 §2：authoring 投影，不进 Contract/Binding） | 允许；post 仍用真实观测对照，期望与观测分离 |
| **C. 对照判定**（pre + action + 真实 post） | 双侧证据 | Noul / Choice 偏差类别 + 概率 | Inspector / Finding | 允许；只作诊断，不授权、不回滚 |
| **D. 选动作**（goal + pre digest + 候选动作） | 候选动作集 | operation + target | Control / Advisory | 允许（`jev-ultrafast` 模式）；每步闭环重看，不预测未来帧 |

**A 不只是违规，还买不到新信息。** 四条理由：

1. post 的候选集只能来自已知页面清单或历史转移表 —— 模型只是在给转移表**加权**，而转移表本身
   是确定性数据；
2. Jev 没有 UI 世界模型：它不知道"点 Wi-Fi 会进子页、可能先弹权限"，而官方把
   `Indirection`（多跳）列为已知弱项；
3. 它不生成文本，所以"推断出的页面语义"必然是你候选集里的某一个 —— 你无法区分它是在推断，
   还是在复述你选项措辞里的暗示；
4. **自举误差**：pre 语义若来自它上一轮的判断，错误会在下一轮复合，而"看起来合理"恰好掩盖它。

**同目的、更便宜的替代（先做这三件）：**

- **确定性转移表**：`SettingsCoverageLedger` 已记录 `RouteBefore` / `RouteAfter` 与 steps verified，
  转移先验现成；P22 允许它作为 non-evidentiary prior 影响 association 排名（ADR-0012），
  但不得 establish `Matched`/`New`。
- **重观察**（ADR-0016：同一 producer 重看同一表现是 revise，不是 conflict）—— 比推断便宜，
  且可归因。
- **确定性变化判定**：route 未变 + 可见 occurrence 集未变 ⇒ "页面语义未变"是结论，不需要模型。

若确实要用 B（只有当 route 派生失败率高、重观察仍不确定时才值得），形状是"选谓词、不编参数"：

```jsonc
"expected": {"type": "choice",
  "instructions": "Which expected effect does `action` have on `page`?",
  "criteria": {"route_changes": "...", "content_changes_only": "...", "route_stable": "..."}}
```

谓词类别由 Choice 选，`subject` / `value` 由代码从元素表填；**低置信即视为"无期望"**
（fail-closed，ADR-0049 对 visual-path 的 `StateBecomes(present)` 保留字已有先例），不得让
模型凭空补出 subject 或 value。

**判据**：先量 `UnknownRouteIdentity` 帧占比与"重观察后仍不确定"的比例。这个比例低，本方案
收益接近零；比例高（WebView / Compose / 三方 App 自绘控件），才轮到 B 或 C 出场。

### 10.8 操作后调用 Jev：世界模型"知道"反而抬高了门槛

把调用点放在**操作后**、并带上世界模型已有信念（pre route、container、可见集 diff），这不是
§10.7 里被否的 A（把推断当观察），而是 C（对账）。形态合法。但它要过三道门槛：

**门槛一：它不能"纠正"世界模型，只会制造 Conflict。** ADR-0016 的四分表是硬的：

| 情形 | 结果 |
|---|---|
| 同 subject 同值 | Reaffirm（无收益） |
| 异值 ∧ 同 Producer ∧ 异 Scope | Revise（Jev 不是既有 producer，这条用不上） |
| 异值 ∧ 同 Producer ∧ 同 Scope | Conflict（同帧真矛盾） |
| **异值 ∧ 异 Producer** | **Conflict（跨源矛盾）** |

Jev 是新 producer，所以"它不同意 route 派生结果"= 跨源 Conflict。PER-003 的实测教训是冲突
**单调累积**：Slice 继续供旧值、目标 subject 的 `HasConflictOnTarget` 永久 fail-closed。也就是
说，让 Jev 去复核世界模型已经知道的事，最坏结果不是"纠正成功"，而是"把这一屏变成不可用"。

**门槛二：可调用面只剩"世界模型没有的 claim"。** 具体是三类：无 resource-id 的自绘控件语义、
偏差原因（无变化/弹权限/跳错页/仅加载）、未知页归类。它们必须经 P2 admission，采信度按
ADR-0028 的 `(源 × claim 类别)` 级联表定级；页面语义这类**建议落在 C（只算线索）**，A/B 需
单独裁决 —— C 的制度含义正是"不能改变决策"。

**门槛三：输入独立性要显式选择。** 把 route/container 值喂给 Jev 再问"这屏是什么"，它会倾向
复述你给的答案。两种问法都合法、但语义不同，不能混：

- **不喂结论、只喂证据** → 独立判定（可新增 claim，也可能与既有 claim 冲突）；
- **喂结论、问"是否矛盾"** → 冲突探测（只出 finding，不改世界）。

选哪一种要写进 lineage，否则同一批判定不可解释。

合法调用序列：

```text
post observe (xml + png)
├── 确定性：route 派生 + identity 判定 + 可见集 diff
├── route 已知 ∧ diff 可归类 → 结束（0 次模型调用）
└── else → Jev(post digest, action, 证据; 不喂结论)
      ├── 低置信 / Unavailable → Unknown（不产生 claim）
      └── 高置信 → proposal → P2 → reconcile
            ├── 新增语义 claim（grade C）→ Slice 可作线索消费
            └── 与既有 claim 异值 → Conflict（留痕、不覆盖、不改 identity）
```

判据同 §10.7：gate 几乎不放行时，这条路径的期望收益接近零（每步一次外网往返 P50 约 280 ms，
换 0 信息）；只有当"route 未知 / diff 无法归类"的比例足够高，才值得为它付这次往返。

### 10.9 把 Jev 当"页面语义确认"：单向 veto，不是第二次识别

形态定死：输入 = 既有的页面语义判断（route / container / 标题）+ 支持它的证据；输出 = 三元
（`confirm` / `refute` / `insufficient`），**不产出新的页面归属**。

三条纪律：

1. **单向门。** `confirm` → 什么都不做；`refute` → 有界升级（重观察一次 → `Unknown` / 慢路径 /
   交人）。不能反向使用（"Jev 说对，所以可以放行"）：`confirm` 不提供新信息，而它错误放行的
   代价高于错误拦截的代价。确认只能**拦**，不能**批**。
2. **落 Finding / Inspector，不进 P2。** 一旦进 WorldModel，它就是跨源 claim，异值即 Conflict
   （§10.8）；Finding 天然"不能改变决策"，冲突面为零。
3. **触发式，不是每步。** 新页面身份首次出现、跨源信号打架（典型是 AGT-010 的撞名页：
   `Security & privacy` 的标题是 `Settings`，靠 up 键在场才区分开）、不可逆动作前。每步都确认
   = 每步多付约 280 ms 换 0 信息。

问法必须是"验证判断"，而不是"再求一次判断"：

```jsonc
"page_check": {"type": "choice",
  "instructions": "`claim` says this screen is `route`, reached by tapping `via`. Do the evidence fields support that, contradict it, or say too little?",
  "criteria": {"confirm": "...", "refute": "...", "insufficient": "..."}}
```

两个陷阱：

- **送分题**：把 claim 的 route 名与标题一起喂进去，模型只需字符串匹配，确认没有信息量。要它判
  的是**证据是否支持这个判断**（up 键、进入路径、条目集），不是名字像不像。
- **"确认成功"不是正确性保证**：它只降低漏检概率，不提高正确率；而最需要确认的撞名页恰恰需要
  多跳推理，正是官方 `Indirection` 弱项。评估看 **veto 精确率**（说 `refute` 时确实不是）与
  **漏放率**（说 `confirm` 时其实不是），不是"一致率"。

### 10.10 复核"点击是否符合预期"：谓词先，Jev 只补三处缝

本仓的预期核对已有权威实现：ADR-0049 的期望载荷（`StateBecomes` / `ChangePredicate{ContentChanged,
RouteStable}`），消费面是 RuntimeAssurance 的谓词求值。**凡是能写成封闭谓词的，Jev 一律多余**
——它更慢（约 280 ms/次）、更不确定，而且结论进不了 `Verified Effect`。

Jev 只在三处有缝可补：

| 缝隙 | 形态 | 例子 |
|---|---|---|
| **期望生成**（期望太弱时） | B：pre 摘要 + 动作 → 候选谓词 | visual-path 的 tap 只有保留字 `StateBecomes(role,"present")`；可提议"应发生 route 变化 / 开关翻转 / 无变化"，由代码裁成载荷 |
| **观测无法机械表达** | C：Choice 偏差类别（无变化 / 弹权限 / 跳错页 / 仅加载），不是 Noul 是非 | WebView 卡片点击后无 route、无 `checked`，但可见文本从「登录」变为「我的账户」 |
| **语义同一性** | C：Noul | 页面重绘后 occurrence id 变了，问"这是否仍是同一个目标" |

硬边界一句话：**Jev 只吃文本，复核能力上限 = 变化能被序列化成文本的程度。** 纯视觉变化（开关
颜色、勾选对勾、进度条增减）它天然看不见；拿"是否达预期"去问它，只会得到低置信或错误答案。
这类必须交视觉模型，不是 Jev。

授权与触发：

- 结论只作 **Finding**，不得修改 `Verified Effect` / Assurance 判定；低置信 = 不判断（`Unknown`）。
- 只在**谓词灰区**触发：匹配到 0 个或 >1 个 occurrence、谓词不可求值、多源信号冲突。每步都复核
  等于每步付 280 ms 并制造噪音。
- 复核输入不喂"我认为结果应该是 X"，只喂 pre 摘要、动作、post 证据；否则它会顺着你的结论说。

### 10.11 作为"每次操作后的检验器"：角色可以，频率不行

角色是对的——它就该在动作后、以检验者身份出现，而不是在动作前替代感知或决策。但**"每一次"
作为默认执行不行**，三条理由按杀伤力排：

1. **多数步骤的信息增量为零。** post 判定若已由 `route` 派生 / 可见集 diff / `StateBecomes`
   谓词求值给出结论，Jev 复核只会重复该结论，而它**无权改动** `Verified Effect`（§10.10），
   于是净效果是"零信息 + 非零延迟 + 新增一轮误报面"。
2. **成本是线性的、可观的。** 每步一次外网往返（官方 API pooled P50 280.80 ms / P95 376.56 ms）。
   假设一次 Settings 遍历 200 步，全量检验就是 +56 s（p50）到 +75 s（p95）；若只有 20% 的步落在
   灰区，同一件事只需 +11~15 s。
3. **误报比漏报更贵。** `refute` 会触发有界重试/升级；一个看不见视觉状态、多跳又弱的判别器，
   在"开关颜色变了但文本没变"的步骤上会稳定地喊错，导致**正确的动作被重做**——在不可逆操作上
   这是真实风险。反过来 `confirm` 给的是虚假安心，不降低实际错误率。

**唯一合法的"每次"是影子模式**：每步都调用，但结论只进 trace / 离线数据集，**不参与任何运行时
判断**。它零风险，且是上线前的必经阶段——用真实流量回答三个问题：灰区占比多少、Jev 在灰区
的 veto 精确率/漏放率多少、哪些动作类型它根本无能为力（应改走视觉模型）。

**上线形态用三层 gate，不用"每次"：**

| 层 | 条件 | 依据 |
|---|---|---|
| 动作风险 | 不可逆动作（卸载/删除/退出登录/改开发者选项）必检；幂等导航动作抽样 | 误报代价不对称（ADR-0017：非幂等动作安全是 pre-dispatch 问题） |
| 谓词状态 | 仅在灰区检：匹配 0 或 >1 个 occurrence、谓词不可求值、多源信号冲突 | §10.10 |
| 频率 | 固定抽样率（例如 1/10）用于持续估计漏检率 | 无抽样则无法度量漂移 |

**前置条件（否则"每次"只会更快地出错）**：post 观测必须已经稳定（动画/加载未完时会判中间态，
本仓已有 settle/等待机制）；输入不喂结论；低置信一律 `Unknown` 不判断。

**用数字决定频率**：若灰区占比高到"几乎每步都是灰区"（典型是 WebView / Compose 的遍历），
那么"每次"自然成立——但这时要的往往不是 Jev，而是视觉模型；若灰区只占一两成，按 gate 触发即可。






---

## 11. 在 Harness 中的定位与边界（目标架构，不看当前实现）

一句话：**Jev 是判别器（judge）。** 它把"需要自由生成或自由推理的一步"，换成"在有限候选上的
一次可校准选择"。它不制造信息，只做收窄。

### 11.1 三种职责（作用）

| 职责 | 输入 | 输出 | 谁受益 |
|---|---|---|---|
| **候选判别**（selection） | 候选表 + 目标/约束 | 选一个 + 每项概率 | 目标 grounding 消歧、动作选择、弹窗/页面归类 |
| **一致性检验**（verification） | pre 摘要 + action + post 证据 | `confirm` / `refute` / `insufficient` | post-action 检验；**只拦不批** |
| **期望提出**（authoring） | pre 摘要 + action + 封闭谓词词汇表 | 谓词候选（`StateBecomes` / `ChangePredicate`） | 期望太弱时补全（由代码裁剪成载荷） |

可选的第四项：**意图消歧**（一句自然语言指令 + 候选动作/任务 → 选一个）。

### 11.2 六条边界（不可跨越）

1. **输入边界**：只吃文本 / JSON。不读像素、不读结构树 —— "看"是 Harness 的责任。
2. **感知边界**：不产元素枚举、不产 bounds、不产结构（`Box` 只是实验性的单目标定位）。
3. **身份边界**：不铸造 identity（页面 / 容器 / 元素）。只回答"是否 / 哪个"，不回答"是什么新东西"。
4. **证据边界**：不进 Evidence / WorldModel；至多 Finding / Advisory；期望不得进入 observation interpretation。
5. **授权边界**：不 gate 动作、不改 `Verified Effect`；`confidence` 只作校准输入，不是阈值权威。
6. **时间边界**：无记忆、无跨步状态 —— 一切上下文由 Harness 放进 `state`；不做多跳因果推断。

记法：**Jev 只能缩短"判断"这一步，替代不了"看""记""做"。**

### 11.3 边界即分工：Harness 必须承担的部分

- **候选枚举与裁剪** —— 它只能在你给的选项里选；
- **digest 构造与版本化** —— 决定它"看到"什么，也决定两次判断为什么不可比；
- **问题措辞与选项顺序** —— 官方承认选项顺序会左右答案，这是 Harness 的变量，不是模型的；
- **阈值与门控** —— 决定它的答案是否被采纳（低置信 = 不判断）；
- **响应落盘与回放锚** —— 决定能不能重放（钉版本 + 缓存响应，不是"重跑一致"）；
- **评估集与指标** —— 决定它有没有上线资格。

换句话说：**Jev 的不确定性，全部由 Harness 的工程承担。**

### 11.4 调用策略

- **用**：匹配 0 或 >1 个 occurrence、谓词不可求值、多源信号冲突、页面身份未知、变化只能表达成
  文本（登录 → 我的账户）、不可逆动作前。
- **不用**：谓词可求值（更快更准）、纯视觉变化（交视觉模型）、需要描述 / 生成 / 计数（交生成模型）。

### 11.5 若 Harness 指开发侧（UniFlow / AI Coder 驱动）

- 只做**验证与归因的初筛裁判**：scenario 判定、失败分类、语义断言 —— 产出一律是 finding。
- **绝不可**把它的"通过"当作 Acceptance 四元组或 Gate 结论（本仓纪律：self-reports never
  qualify），也不可让它成为第二套 truth。
- 开发侧的真实价值：**降低 LLM 自由生成那一步的方差**，而不是"多接一个模型"。

### 11.6 上线资格（验收口径，不看延迟）

veto 精确率、漏放率、灰区覆盖率、每千步成本与 p95、回放可重放率。任一项不过 → 只允许 shadow
模式，不进默认路径。

### 11.7 与本仓现有契约的代码级对照

| 本仓既有 | 代码位置 | 与 Jev 的关系 | 缺口 |
|---|---|---|---|
| 感知双协议 | `Capability/CapabilityHub.cs`：`ISemanticPerception` / `IUiElementPerception` | Jev 只能落 Semantic 侧 | 无 |
| 有界上下文 | `Perception/SlowContracts.cs`：`EvidenceContext`（`budget=32`、`Exclusions`、`ConflictBasis` 原子） | **同构**：都禁止塞整棵树 | 本仓只有 element candidates，没有 **answer candidates** |
| 依据前置 | `Perception/TextFastBasis.cs`：`SlowTextGate`（`MissingFast` / `StaleFast` / `MisalignedFast` / `ProviderUnavailable`） | **同构**：模型只在已有依据上工作；"同一 capture/cycle 对齐"= Jev 的 state 必须与元素表同源 | 无 |
| 咨询结果 | `SlowContracts.cs`：`SlowResult`（`Proposals` + `SlowExecutionStatus` ×10 + `SlowSemanticDisposition` ×4） | Jev 输出 options + `probabilities` + `confidence`；本仓输出自由 claim | **无概率/置信度字段**；`value` 形态靠模型自由生成（`Wi-Fi` / `wi-fi` / `WLAN`），无法弃权、无法门控 |
| 期望与观测分离 | `Assurance/PostActionEffectVerification.cs`：`ExpectedEffect`（A3 空谓词集拒绝）+ `ChangeEvidence`(internal) | **同构**：期望不进 observation interpretation | 验证结果是 `IsVerified` 二元 + fail-closed，缺"不确定"这一态 |
| 只读 finding | `Capability/IntegrationContracts.cs`：`Finding`（`FindingDisposition` + `RuleVersion` + `Diagnostic`） | Jev 的检验结论应落这里 | Finding 无置信度、无偏差分类 |
| 尝试去重 | `SlowContracts.cs`：`SlowAttemptKey`（transport retry 不产生新 key） | 同构 | 绑单个 `RequiredClaim`，无多 claim 扇出 |
| 判定身份 | `Perception/FastPerception.cs`：`IVersionedFastPerceptionStrategy`（`StrategyIdentity`/`StrategyVersion`） | 同构需求：Jev 的 criteria/问题集改动等价于换模型 | Slow 请求缺"问题集版本"字段 |

**结论：缺的不是架构，是判别式接口的三件小东西** —— 候选集、概率、以及"不确定"的显式承载。

#### 可借鉴的落点（不引入 Jev 也能做，按收益排）

| 优先级 | 做法 | 收益 | 风险 |
|---|---|---|---|
| **P1** | 给 Slow 契约加 `AnswerCandidates`（闭合选项集）+ `OptionProbabilities`/`ProviderConfidence` | `value` 形态稳定；低置信可弃权（映射 `SlowSemanticDisposition.Unknown`）；结构上无法产出表外答案 | 选项上限（Jev 255）与**选项顺序敏感**：排序规则必须确定性、版本化 |
| **P2** | 一次咨询扇出多个 `RequiredClaim`（各自独立 `SlowAttemptKey`） | 减少往返（参照 jev-ultrafast：协议调用 1,092 → 101） | speculative 问题的结果必须允许"未使用即丢弃"，不得因为问了就当成证据 |
| **P3** | shadow consult：跑 provider 但不入 P2，结果只进 trace/数据集 | 任何 provider（现有模型或 Jev）都有可归因的评估基线 | 需在 trace 里区分 shadow 与 canonical，否则污染统计 |
| **P4** | 问题集/prompt 版本进 `SlowAttemptKey` 与 lineage | criteria 一改，答案分布就变，必须能归因 | 与既有四层身份（modelId/configId/pipelineRevision/deploymentId）拼装时要一致 |
| **P5** | 把 `SlowSemanticDisposition` 的四分语义搬到验证侧的**诊断** | 权威仍是 `IsVerified`（二元、fail-closed），诊断可以是三分/四分 | 绝不能让诊断越权成为 Gate 输入 |

**不要借鉴的**：把 Jev 的 `confidence` 变成 Admission/Gate 阈值（违反本仓 `PerceptionAssessment`
非权威语义与 FastPerception §19 的 `threshold ≠ truth threshold`）；把它的选项集当身份（选择不等于 identity）。

### 11.8 Slow 该不该跟着改，以及 Jev 能不能当 Slow 的一条 realization

**一、P1–P5 不是一起做，按 buyer 分档。** 本仓纪律是 buyer-first（ADR-0026），所以每一项
都要先有测量证据：

| 项 | 真实 buyer | 先要的证据 |
|---|---|---|
| **P1** 候选集 + 概率 | `value` 形态不稳定 → 同 subject 反复 `Revise` / `Conflict`，Slice 供旧值 | 统计同一 subject 的 Slow 返回 `value` 分布（现有 corpus 可算） |
| **P2** 多 claim 扇出 | 往返次数与延迟预算 | 每步 Slow 调用次数与 p95 |
| **P3** shadow | 任何新 provider 上线前的评估基线 | 接 Jev 的前置，不需要额外 buyer |
| **P4** 问题集版本 | prompt/criteria 改动导致漂移不可归因 | 是否真实发生过 |
| **P5** 验证诊断三分 | AGT-019 的"异常/不确定"表达 | 跟随该 change 的结论 |

**二、Jev 作为 Slow 的一条 realization 成立，但分派依据是 claim 的性质，不是模型品牌：**

- **需要生成一个新值**（claim 的 `value` 域开放）→ 生成式 realization（现有文本模型）。
- **在已知候选里判定**（`value` 域可枚举）→ 判别式 realization（Jev 这类）。
- 位置：`SlowTextGate` 下游（缺 Fast 前置照样不调用）、P2 之前；产物仍是
  `ObservationProposal`（选项 → 单值 claim）+ 既有 `SlowSemanticDisposition`，
  不新增第五种处置态。
- **前置是 P1**：没有 `AnswerCandidates`，判别式 realization 无处安放 —— 它没有"自由生成"
  这条腿。所以顺序是「先让契约支持候选集」→「再接第二条 realization」，不是反过来。
- profile 层：新增一个判别档（如 `slow.semantic.judgement`）属于"实现既有缝"，不是改核心；
  但它**不能并入 `slow.visual`**（Jev 看不见像素，除实验性 Box）。

**三、退出条件（诚实版）。** 若测量显示现有 Slow 的 claim `value` 已经足够稳定（subject 结构化、
取值域小），P1 的 buyer 就消失，Jev 在 Slow 里也就没有位置 —— 此时它只剩"独立 Inspector /
Finding"这一条路（§11.2 职责二）。**先测，再改契约，最后才谈接模型。**

---

## 12. 未验证 / 待实证清单

| # | 问题 | 状态 |
|---|---|---|
| 1 | `box` 在 UI 截图（而非自然图像）上的定位精度、小控件召回 | **[未验证]** 官方零数据 |
| 2 | 官方到底是否 GA 了图片输入（3 个官方页面互相矛盾） | **[未验证]** 需实测 `jev-1.14.0` |
| 3 | 官方延迟口径（150ms？70–500ms？）与 SLA | **[未验证]** 只有一句话宣传 + 媒体转述 |
| 4 | 是否可设 seed/温度；同输入是否逐字节稳定 | **[未验证]** schema 无字段；官方反证为"alias 会变+需缓存" |
| 5 | APUS `fast-browser-use` 的仓库/代码与 18s、4 次打分、MIT | **[未验证]** 仅媒体转述 |
| 6 | "纯 Jev agent 在长程任务上 1/20 vs 混合 17/20" | **[未验证]** 二手转述，无原始基准 |
| 7 | $5 白名单额度（媒体）vs "There is no free tier"（聚合站） | **[冲突]** 两边都不是一手定价页 |
| 8 | 255 选项上限是否随 1.14 提升 | **[未验证]** 1.14 文档未更新该上限 |
| 9 | 桌面（Windows/Linux）上是否有可用的一手 Jev 集成 | **[未验证]** 仅见 macOS 社区方案 |
| 10 | 页面级判别（页面归类 / 弹窗类别 / goal-stuck）在真实 App 上的准确率 | **[未验证]** 公开资料只有集成方自述，无标注数据集 |

---

## 13. 来源清单

### 一手（TypeSafe）

- 介绍：<https://docs.typesafe.ai/introduction>
- System One 概念：<https://docs.typesafe.ai/concepts/system-one>
- 原语总览：<https://docs.typesafe.ai/primitives> ｜ Choice：<https://docs.typesafe.ai/primitives/choice> ｜ **Box**：<https://docs.typesafe.ai/primitives/box>
- 图片/限制：<https://docs.typesafe.ai/images>
- 模型/定价/alias：<https://docs.typesafe.ai/models>
- API/OpenAPI：<https://docs.typesafe.ai/api/system-one> ｜ <https://api.typesafe.ai/openapi.json>
- 快速开始（SDK/curl）：<https://docs.typesafe.ai/introduction/quickstart>
- 训练/校准（RLCD）：<https://docs.typesafe.ai/introduction/machine-learning-primer>
- **失败模式（9 条）**：<https://docs.typesafe.ai/model-jaggedness/jev-1.13>
- 用例图（150ms 口径）：<https://docs.typesafe.ai/concepts/use-case-map>
- **结构恢复 cookbook**：<https://docs.typesafe.ai/cookbooks/autoformat>
- 发布公告（页面正文未能抓取，本文凡引其内容均标注"经 Latenode 转引"）：<https://typesafe.ai/blog/introducing-system-one-models-and-jev>

### 集成方一手（对"怎么用"是事实，对 Jev 内部是推断）

- **browser-use/jev-ultrafast**：<https://github.com/browser-use/jev-ultrafast> ｜
  [`model.py`](https://github.com/browser-use/jev-ultrafast/blob/main/jev_ultrafast/model.py) ｜
  [`questions.py`](https://github.com/browser-use/jev-ultrafast/blob/main/jev_ultrafast/questions.py)
- **Anchor Browser `jev` agent**：<https://docs.anchorbrowser.io/agentic-browser-control/computer-use-agents/jev>
- **jkudish/jev-browser**（DOM 元素表、240 上限、goal/stuck Noul）：<https://github.com/jkudish/jev-browser>
- **xinwang-nwpu/jev-mobile**（A11y + ADB + 视觉兜底）：<https://github.com/xinwang-nwpu/jev-mobile>
- **cnhuye/mobile-jev-ultrafast**（JEV 协议可替换性）：<https://github.com/cnhuye/mobile-jev-ultrafast>
- **voidning/jev-figma-design-plugin**（指令→多选题）：<https://github.com/voidning/jev-figma-design-plugin>
- **apus-ailab/APUS-Reflex**（可自托管的决策模型族 + TypeSafe 兼容 `/v1/systemone` 网关；非 Jev 本体）：<https://huggingface.co/apus-ailab/APUS-Reflex>

### 竞品一手

- OmniParser 论文：<https://arxiv.org/abs/2408.00203> ｜ repo：<https://github.com/microsoft/OmniParser>
- UI-TARS：<https://github.com/bytedance/UI-TARS>（1.5 / 2 数据与限制）｜ 论文：<https://arxiv.org/abs/2501.12326>
- SeeClick：<https://arxiv.org/abs/2401.10935>
- Claude computer use：<https://platform.claude.com/docs/en/agents-and-tools/tool-use/computer-use-tool>

### 媒体 / 社区（明确标注为二手）

- Latenode（对 jev-ultrafast / 官方博客的转引与批评）：<https://latenode.com/ai-trends/jev-ultrafast-browser-agent>
- 新华网（APUS 复现、200×/1/400 官方口径）：<https://app.xinhuanet.com/news/article.html?articleId=202609216ad2782938944456924e1016dd3b84fd>
- 央广网（同题，中文可读）：<https://tech.cnr.cn/techph/20260920/t20260920_527819594.shtml>
- 阿里云社区 / 腾讯云社区（演示数字汇总）：<https://developer.aliyun.com/article/1765567> ｜ <https://cloud.tencent.cn/developer/article/2749332>
- Milind S（"without screenshots" 原始帖）：<https://x.com/milindlabs/status/2100631847155994852>
- 聚合站（发布日/闭源/定价口径，均非一手）：<https://madewithjev.com/what-is-jev> ｜ <https://madewithjev.com/open-source-jev> ｜ <https://madewithjev.com/jev-pricing> ｜ <https://madewithjev.com/jev-limitations>

---

## 14. 逐题置信度

| 问题 | 置信度 | 理由 |
|---|---|---|
| 1. Jev 是什么（模型/托管/闭源/谁做的/何时/许可） | **高** | 官方 introduction/models/OpenAPI 一致；仅"发布日"依赖聚合站 |
| 2. 输入（截屏/视频/DOM/像素） | **高**（文本/JSON、图片预览态）/ **中**（图片是否 GA） | 官方三页互相矛盾，已标注 |
| 3. 输出格式与"能否当布局表示" | **高** | OpenAPI schema + 官方示例 + 真实集成代码三方一致 |
| 4. 是否真做版面解析 | **高**（否定结论） | 官方"one box per question" + 三份集成 README 的几何来自外部，证据同向 |
| 5. 语义识别能力边界 | **高** | "不生成文本"在多处一手文档重复；能力项有集成方代码佐证 |
| 6. 性能/延迟与机制 | **中** | 只有一句官方 150ms；其余为媒体/社区单任务数字；APUS 机制推测未验证 |
| 7. 集成形态（API/SDK/平台/认证/价格） | **高**（HTTP/定价）/ **中**（第三方网关、平台覆盖） | 一手 API 文档充分；平台侧靠社区仓库 |
| 8. 限制与失败模式 | **中高** | 官方 jaggedness 与集成方限制坚实；长程任务弱、白名单额度两条为二手/冲突 |
| 9. 与竞品对比表 | **中** | 每个单元格都有来源，但基准口径不同源、不可横向相加 |

# ScreenVLM 一轮评测（MLX 4bit @ M4 Metal；uni-agent slow 用例）

模型: olragon/ScreenVLM-MLX-4bit（docling ScreenVLM v2, Idefics3 0.3B）
## 71 查询 grounding（center-y 判定，同旧基准口径）

| page | truth | 命中 | 覆盖 | meanErr | medianErr | ≤35px | nScreenTag |
|---|---|---|---|---|---|---|---|
| root-top | 16 | 1 | 0.062 | 11.7 | 11.7 | 1.0 | 211 |
| root-scrolled | 24 | 3 | 0.125 | 105.1 | 156.1 | 0.333 | 138 |
| accessibility | 15 | 3 | 0.2 | 378.3 | 174.3 | 0.0 | 228 |
| display-child | 16 | 1 | 0.062 | 807.5 | 807.5 | 0.0 | 215 |

## 类型判别（71 元素 → row_title/row_subtitle/section_label；ScreenTag 类映射，如实声明）

| page | targets | 映射命中 | 映射 acc |
|---|---|---|---|
| root-top | 16 | 0 | 0.0 |
| root-scrolled | 24 | 1 | 0.042 |
| accessibility | 15 | 0 | 0.0 |
| display-child | 16 | 1 | 0.062 |

## 9 核心题（subset12；命中来自对应页 type 判型）

| page | text | truth | pred_raw | pred | hit |
|---|---|---|---|---|---|
| root-top | Settings | section_label | None | None | False |
| root-top | 38% used - 9.96 GB free | row_subtitle | None | None | False |
| display-child | Color | section_label | None | None | False |
| display-child | Colors | row_title | None | None | False |
| root-top | Search settings | row_title | search_field | None | False |
| root-top | Network & internet | row_title | None | None | False |
| root-top | Mobile, Wi‑Fi, hotspot | row_subtitle | None | None | False |
| root-top | Bluetooth, pairing | row_subtitle | None | None | False |
| display-child | Brightness | section_label | None | None | False |

## 延迟

- root-top: 12.4s
- root-scrolled: 12.3s
- accessibility: 12.3s
- display-child: 12.3s

## FSV Android 帧（OOD 观测）

### devopts-top（12.4s, 206 元素）
标签: ['text']
样本: [{"label": "text", "bbox": [54.0, 48.0, 101.52, 100.80000000000001], "text": "3.3.6"}, {"label": "text", "bbox": [43.2, 48.0, 54.0, 100.80000000000001], "text": "5"}, {"label": "text", "bbox": [101.52, 48.0, 133.92, 100.80000000000001], "text": "0.3.3"}, {"label": "text", "bbox": [43.2, 48.0, 54.0, 100.80000000000001], "text": "5"}, {"label": "text", "bbox": [133.92, 48.0, 151.20000000000002, 100.80000000000001], "text": "0.3.3"}, {"label": "text", "bbox": [43.2, 48.0, 54.0, 100.80000000000001],

## 分布内 sanity（真实网页 MDN）
- 6.9s / 2048 tokens，元素 11：logo×1, text×5, link×3, search_field×2
- 文本正确："MDN"、"for Developers, by Developers"、link=CSS/HTML/JavaScript、"Search"字段
- 结构合法 ScreenTag（<Logo><Text><Link>...）。长文有复读伪影（VLM 常见）。
→ 模型本身可用；Android 帧上的失败 = OOD 崩坏，非实现 bug。

## 结论（本一轮）
1. ScreenVLM（MLX 4bit，M4 Metal）：21.7 tok/s，~12.3s/整屏 @ 4096 tokens；0.3B 可部署。
2. **Android 原生 UI = 崩坏级 OOD**（官方 Limitations "web-centric, mobile/desktop may vary" 实锤）：
   - root-top（Settings 首页 1080×2400）被读成网站导航 "Home About Us Products Services Blog Contact Us"
   - devopts-top：206 元素全部塌缩为单一 "text" 类 → 类型/层级推断失效
   - 71 查询 grounding 覆盖 8/71（11%）；typeAcc 映射后 ≈0；9 核心题（Color/Colors）全错
3. 对 UniClaw：直接上不可用；需 Android 域微调（nanoVLM 训练管线 + ScreenTag 监督现成）。
